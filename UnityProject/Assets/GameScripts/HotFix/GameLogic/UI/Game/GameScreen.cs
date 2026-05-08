using System;
using System.Collections.Generic;
using EF.Debugger;
using EF.UI;
using GameConfig.monster;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 局内界面 Screen。绑定 GameViewModel 的 ReactiveProperty，
    /// 使用 Region 在 Battle/Reward 之间切换。
    /// 手牌区使用扇形布局 + 三态交互（点击预览 / 悬停抬升 / 拖拽出牌）。
    /// </summary>
    public class GameScreen : Screen<GameViewModel>
    {
        // === 交互参数（集中常量，便于调参）===
        private const float DragThreshold = 10f;        // 越过此位移才视为拖拽
        private const float MaxCardSpacing = 120f;      // 相邻卡水平间距上限
        private const float RotatePerStep = 3f;         // 每张卡相对中心旋转角度（度）
        private const float TranslateYCoeff = 3.5f;     // 抛物线下沉系数
        private const float CardWidth = 150f;
        private const float CardHeight = 230f;
        private const float HandFanBottomPadding = 20f; // 卡牌底边距 hand-fan 底部留白
        private const long ReboundDurationMs = 160;     // 回弹动画时长（略大于 USS transition 0.15s）

        /// <summary>手牌交互状态。</summary>
        private enum CardInteractionState
        {
            Idle,
            Hovering,
            Previewing,
            Dragging
        }

        // === 常驻区域元素（GameView.uxml 中的 player-status / info-bar）===
        private Label _infoLabel;
        private VisualElement _hpBarFill;
        private Label _hpText;
        private Label _armorText;
        private VisualElement _energyBarFill;
        private Label _energyText;

        // === Region ===
        private Region _mainRegion;
        private BattlePhase _activeRegionPhase = BattlePhase.Idle;

        // === Battle 子元素（在 BattlePanel 加载完成后绑定）===
        private VisualElement _monsterContainer;
        private VisualElement _handFan;
        private VisualElement _previewLayer;
        private VisualElement _dropZone;
        private Button _endTurnBtn;
        private EventCallback<GeometryChangedEvent> _handFanGeometryHandler;

        // === Reward 子元素 ===
        private Button _rewardConfirmBtn;

        // === 子项模板 ===
        private VisualTreeAsset _monsterItemVta;
        private VisualTreeAsset _cardItemVta;

        // === 子项追踪 ===
        private readonly List<VisualElement> _monsterItems = new();
        private readonly List<VisualElement> _cardItems = new();

        // === 交互状态机 ===
        private CardInteractionState _state = CardInteractionState.Idle;
        private int _activeCardIndex = -1;
        private Vector2 _pointerStartPos;
        private int _capturedPointerId = -1;
        private VisualElement _captureSource;
        private VisualElement _dragGhost;
        private VisualElement _previewClone;

        /// <inheritdoc />
        protected override void OnSetup()
        {
            // 常驻区域
            _infoLabel = this.Q<Label>("info-text");
            _hpBarFill = this.Q("hp-bar-fill");
            _hpText = this.Q<Label>("hp-text");
            _armorText = this.Q<Label>("armor-text");
            _energyBarFill = this.Q("energy-bar-fill");
            _energyText = this.Q<Label>("energy-text");

            // Region（从 UXML 中名为 main-region 的插槽）
            var slot = this.Q("main-region");
            if (slot == null)
            {
                Log.Error("[GameScreen] GameView.uxml 缺少 name=\"main-region\" 容器");
                return;
            }
            _mainRegion = new Region(slot, GameLogicEntry.Resource);

            // 数据绑定（合并 Phase 订阅：先 RefreshInfo 再切换 Region）
            ViewModel.Energy.Changed += _ => RefreshInfo();
            ViewModel.MaxEnergy.Changed += _ => RefreshInfo();
            ViewModel.PlayerHp.Changed += _ => RefreshInfo();
            ViewModel.PlayerMaxHp.Changed += _ => RefreshInfo();
            ViewModel.PlayerArmor.Changed += _ => RefreshInfo();
            ViewModel.IsLevelComplete.Changed += _ => RefreshInfo();
            ViewModel.IsPlayerDead.Changed += _ => RefreshInfo();

            ViewModel.Phase.Changed += OnPhaseChanged;

            ViewModel.Monsters.Changed += _ =>
            {
                LoadItemTemplates();
                RefreshMonsters();
                RefreshInfo();
            };

            ViewModel.Hand.Changed += _ =>
            {
                LoadItemTemplates();
                RefreshCards();
            };

            Log.Info("[GameScreen] 局内界面绑定完成");
        }

        /// <inheritdoc />
        public override void OnShow()
        {
            LoadItemTemplates();
            RefreshInfo();
            // 根据当前 Phase 同步加载对应 Region 内容
            OnPhaseChanged(ViewModel.Phase.Value);
            Log.Info("[GameScreen] 局内界面已显示");
        }

        private async void OnPhaseChanged(BattlePhase phase)
        {
            RefreshInfo();
            if (_mainRegion == null) return;

            BattlePhase target = MapPhaseToRegion(phase);
            if (target == _activeRegionPhase) return;

            _activeRegionPhase = target;

            try
            {
                switch (target)
                {
                    case BattlePhase.PlayerTurn:
                        await _mainRegion.ShowAsync("BattlePanel");
                        BindBattleContent();
                        break;
                    case BattlePhase.Reward:
                        await _mainRegion.ShowAsync("RewardPanel");
                        BindRewardContent();
                        break;
                    default:
                        // Idle 等阶段保持当前内容
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[GameScreen] 切换 Region 失败：{e.Message}");
            }
        }

        /// <summary>
        /// 把详细 Phase 映射到 Region 视图：战斗中各阶段都用 BattlePanel；Reward 用 RewardPanel。
        /// </summary>
        private static BattlePhase MapPhaseToRegion(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.Reward => BattlePhase.Reward,
                BattlePhase.Prepare => BattlePhase.PlayerTurn,
                BattlePhase.PlayerTurn => BattlePhase.PlayerTurn,
                BattlePhase.MonsterTurn => BattlePhase.PlayerTurn,
                BattlePhase.Check => BattlePhase.PlayerTurn,
                _ => BattlePhase.Idle
            };
        }

        private void BindBattleContent()
        {
            var content = _mainRegion.CurrentContent;
            if (content == null) return;

            // 先解绑旧 hand-fan 的 GeometryChangedEvent，避免内容切换后旧引用泄漏
            DetachHandFanGeometry();

            // 防御性检测：如果 BattlePanel.uxml 还存在旧版的 card-scroll（ScrollView），
            // 通常说明 Unity 没有重新导入新版 UXML（Play 模式下文件改动需要重启 Play 才生效）。
            // 直接禁用旧 ScrollView 的内置 scroll，避免它抢走 PointerDown 让整个界面上移。
            var legacyScroll = content.Q<ScrollView>("card-scroll");
            if (legacyScroll != null)
            {
                Log.Error("[GameScreen] 检测到旧版 BattlePanel.uxml（仍有 card-scroll ScrollView）。" +
                          "请：① 停止 Play 模式 ② 在 Project 面板对 BattlePanel.uxml 右键 Reimport ③ 重新进入 Play。" +
                          "已临时隐藏旧 ScrollView 以阻止其干扰拖拽。");
                legacyScroll.style.display = DisplayStyle.None;
                legacyScroll.pickingMode = PickingMode.Ignore;
            }

            _monsterContainer = content.Q("monster-container");
            _handFan = content.Q("hand-fan");
            _previewLayer = content.Q("preview-layer");
            _dropZone = content.Q("drop-zone");
            _endTurnBtn = content.Q<Button>("end-turn-btn");

            if (_handFan == null) Log.Error("[GameScreen] BattlePanel.uxml 缺少 name=\"hand-fan\" 容器（同样建议 Reimport BattlePanel.uxml）");
            if (_previewLayer == null) Log.Error("[GameScreen] BattlePanel.uxml 缺少 name=\"preview-layer\" 容器（同样建议 Reimport BattlePanel.uxml）");

            if (_handFan != null)
            {
                _handFanGeometryHandler = OnHandFanGeometryChanged;
                _handFan.RegisterCallback(_handFanGeometryHandler);
            }

            if (_endTurnBtn != null)
            {
                _endTurnBtn.RegisterCallback<ClickEvent>(_ => ViewModel.EndTurn());
            }

            RefreshMonsters();
            RefreshCards();
            RefreshInfo();
        }

        private void DetachHandFanGeometry()
        {
            if (_handFan != null && _handFanGeometryHandler != null)
            {
                _handFan.UnregisterCallback(_handFanGeometryHandler);
            }
            _handFanGeometryHandler = null;
        }

        private void BindRewardContent()
        {
            var content = _mainRegion.CurrentContent;
            if (content == null) return;

            _rewardConfirmBtn = content.Q<Button>("reward-confirm-btn");
            if (_rewardConfirmBtn != null)
            {
                _rewardConfirmBtn.RegisterCallback<ClickEvent>(_ => ViewModel.SelectReward());
            }
        }

        private void LoadItemTemplates()
        {
            if (_monsterItemVta != null) return;
            try
            {
                var rm = GameLogicEntry.Resource;
                if (rm == null) return;
                _monsterItemVta = LoadTemplate(rm, "MonsterItem");
                _cardItemVta = LoadTemplate(rm, "CardItem");
            }
            catch (Exception e)
            {
                Log.Warning($"[GameScreen] 加载模板失败：{e.Message}");
            }
        }

        private static VisualTreeAsset LoadTemplate(EF.Resource.IResourceManager rm, string location)
        {
            try
            {
                var handle = rm.LoadAssetSync<VisualTreeAsset>(location);
                return handle.AssetObject as VisualTreeAsset;
            }
            catch (Exception e)
            {
                Log.Warning($"[GameScreen] 加载模板失败 {location}：{e.Message}");
                return null;
            }
        }

        private void RefreshMonsters()
        {
            ClearItems(_monsterItems);
            if (_monsterContainer == null) return;

            var monsters = ViewModel.Monsters.Value;
            if (monsters == null) return;

            foreach (var monster in monsters)
            {
                if (monster.Hp <= 0) continue;
                if (_monsterItemVta == null) continue;

                var item = _monsterItemVta.CloneTree();

                var intentLabel = item.Q<Label>("intent-text");
                if (intentLabel != null && monster.CurrentIntent != null)
                {
                    string intentText = monster.CurrentIntent.IntentType switch
                    {
                        MonsterIntentType.Attack => $"攻击 {monster.CurrentIntent.Value}",
                        MonsterIntentType.Defend => $"防御 {monster.CurrentIntent.Value}",
                        _ => monster.CurrentIntent.IntentType.ToString()
                    };
                    intentLabel.text = intentText;
                    intentLabel.RemoveFromClassList("monster-intent-attack");
                    intentLabel.RemoveFromClassList("monster-intent-defend");
                    intentLabel.AddToClassList(
                        monster.CurrentIntent.IntentType == MonsterIntentType.Attack
                            ? "monster-intent-attack"
                            : "monster-intent-defend");
                }

                var nameLabel = item.Q<Label>("name-text");
                if (nameLabel != null) nameLabel.text = monster.Config.Name;

                var hpBar = item.Q("hp-bar");
                if (hpBar != null)
                {
                    float hpPercent = monster.MaxHp > 0 ? (float)monster.Hp / monster.MaxHp : 0f;
                    hpBar.style.width = new StyleLength(new Length(hpPercent * 100, LengthUnit.Percent));
                }

                var hpText = item.Q<Label>("hp-text");
                if (hpText != null)
                {
                    string hpStr = $"HP:{monster.Hp}/{monster.MaxHp}";
                    if (monster.Armor > 0) hpStr += $" 护甲:{monster.Armor}";
                    hpText.text = hpStr;
                }

                _monsterContainer.Add(item);
                _monsterItems.Add(item);
            }
        }

        #region 手牌扇形布局

        private void RefreshCards()
        {
            // 手牌变更时强制清掉残留交互态，避免悬空 ghost / 预览克隆
            if (_state == CardInteractionState.Dragging)
            {
                ExitDragging();
                ReleaseCaptureIfAny();
                SetState(CardInteractionState.Idle, -1);
            }
            if (_state == CardInteractionState.Previewing)
            {
                ExitPreview();
                SetState(CardInteractionState.Idle, -1);
            }

            ClearItems(_cardItems);
            if (_handFan == null) return;

            var hand = ViewModel.Hand.Value;
            if (hand == null) return;

            int total = hand.Count;
            for (int i = 0; i < total; i++)
            {
                if (_cardItemVta == null) continue;

                var template = _cardItemVta.CloneTree();
                // 取真正带 .card-item class 的内层 VisualElement，从 TemplateContainer 中分离
                var item = template.Q(className: "card-item");
                if (item == null) continue;
                item.RemoveFromHierarchy();

                var card = hand[i];
                int index = i;

                var nameLabel = item.Q<Label>("card-name");
                if (nameLabel != null) nameLabel.text = card.Config.Name;

                var costLabel = item.Q<Label>("card-cost");
                if (costLabel != null) costLabel.text = card.Config.Cost.ToString();

                item.RegisterCallback<PointerDownEvent>(evt => OnCardPointerDown(evt, index, item));
                item.RegisterCallback<PointerEnterEvent>(_ => OnCardPointerEnter(item));
                item.RegisterCallback<PointerLeaveEvent>(_ => OnCardPointerLeave(item));

                _handFan.Add(item);
                _cardItems.Add(item);

                ApplyFanTransform(item, i, total);
            }
        }

        /// <summary>
        /// 按设计公式计算第 index 张（共 total 张）的扇形 transform，并写入 inline style。
        /// </summary>
        private void ApplyFanTransform(VisualElement card, int index, int total)
        {
            if (card == null || total <= 0) return;

            float fanWidth = ResolveSize(_handFan, true, 800f);
            float fanHeight = ResolveSize(_handFan, false, 280f);

            float center = (total - 1) / 2f;
            float offset = index - center;

            float spacing = total > 1
                ? Mathf.Min(MaxCardSpacing, (fanWidth - CardWidth) / (total - 1))
                : 0f;

            float left = fanWidth / 2f + offset * spacing - CardWidth / 2f;
            float baseTop = Mathf.Max(0f, fanHeight - CardHeight - HandFanBottomPadding);

            float translateY = offset * offset * TranslateYCoeff;
            float rotateDeg = offset * RotatePerStep;

            card.style.left = left;
            card.style.top = baseTop;
            card.style.translate = new StyleTranslate(new Translate(0, translateY, 0));
            card.style.rotate = new StyleRotate(new Rotate(new Angle(rotateDeg, AngleUnit.Degree)));
        }

        /// <summary>
        /// 取容器尺寸，resolvedStyle 未就绪时退化到 layout 宽高，再退化到默认值。
        /// </summary>
        private static float ResolveSize(VisualElement element, bool width, float fallback)
        {
            if (element == null) return fallback;
            float resolved = width ? element.resolvedStyle.width : element.resolvedStyle.height;
            if (resolved > 0) return resolved;
            float layoutVal = width ? element.layout.width : element.layout.height;
            if (layoutVal > 0) return layoutVal;
            return fallback;
        }

        /// <summary>
        /// hand-fan 几何变化（首次 layout 完成或 resize）时重排所有卡牌。
        /// </summary>
        private void OnHandFanGeometryChanged(GeometryChangedEvent evt)
        {
            int total = _cardItems.Count;
            for (int i = 0; i < total; i++)
            {
                ApplyFanTransform(_cardItems[i], i, total);
            }
        }

        #endregion

        #region 交互状态机

        private void SetState(CardInteractionState newState, int cardIndex)
        {
            if (_state == newState && _activeCardIndex == cardIndex) return;
            var oldState = _state;
            _state = newState;
            _activeCardIndex = cardIndex;
            Log.Info($"[GameScreen] CardInteraction {oldState} → {newState} (index={cardIndex})");
        }

        private void OnCardPointerDown(PointerDownEvent evt, int cardIndex, VisualElement source)
        {
            // 只在玩家回合允许卡牌交互
            if (ViewModel.Phase.Value != BattlePhase.PlayerTurn) return;
            // 已有 capture 则忽略（防止多指同时按）
            if (_captureSource != null) return;

            _pointerStartPos = evt.position;
            _capturedPointerId = evt.pointerId;
            _captureSource = source;
            _activeCardIndex = cardIndex;

            source.CapturePointer(evt.pointerId);
            source.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
            source.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
            source.RegisterCallback<PointerCaptureOutEvent>(OnCardPointerCaptureOut);

            evt.StopPropagation();
        }

        private void OnCardPointerMove(PointerMoveEvent evt)
        {
            if (_captureSource == null) return;

            if (_state != CardInteractionState.Dragging)
            {
                if (Vector2.Distance(evt.position, _pointerStartPos) > DragThreshold)
                {
                    EnterDragging(_activeCardIndex, _captureSource, evt.position);
                    SetState(CardInteractionState.Dragging, _activeCardIndex);
                }
            }
            else
            {
                UpdateGhostPosition(evt.position);
            }

            evt.StopPropagation();
        }

        private void OnCardPointerUp(PointerUpEvent evt)
        {
            var source = _captureSource;
            if (source == null) return;

            if (_state == CardInteractionState.Dragging)
            {
                bool insideDrop = _dropZone != null && _dropZone.worldBound.Contains(evt.position);
                if (insideDrop)
                {
                    int idx = _activeCardIndex;
                    ExitDragging();
                    ReleaseCapture(source);
                    ViewModel.UseCard(idx);
                    SetState(CardInteractionState.Idle, -1);
                }
                else
                {
                    // 释放捕获后启动回弹动画（动画期间无需再处理 pointer）
                    ReleaseCapture(source);
                    StartReboundAnimation(source);
                }
            }
            else
            {
                int idx = _activeCardIndex;
                ReleaseCapture(source);
                TogglePreview(idx, source);
            }

            evt.StopPropagation();
        }

        private void OnCardPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            var source = _captureSource;
            if (source == null) return;

            Log.Warning("[GameScreen] PointerCapture 中途丢失，强制重置交互态");

            if (_state == CardInteractionState.Dragging)
            {
                ExitDragging();
            }
            // 已经丢了 capture，不再调用 ReleasePointer，但要解注册事件
            source.UnregisterCallback<PointerMoveEvent>(OnCardPointerMove);
            source.UnregisterCallback<PointerUpEvent>(OnCardPointerUp);
            source.UnregisterCallback<PointerCaptureOutEvent>(OnCardPointerCaptureOut);
            _captureSource = null;
            _capturedPointerId = -1;

            SetState(CardInteractionState.Idle, -1);
        }

        private void ReleaseCapture(VisualElement source)
        {
            if (source == null) return;
            if (_capturedPointerId >= 0 && source.HasPointerCapture(_capturedPointerId))
            {
                source.ReleasePointer(_capturedPointerId);
            }
            source.UnregisterCallback<PointerMoveEvent>(OnCardPointerMove);
            source.UnregisterCallback<PointerUpEvent>(OnCardPointerUp);
            source.UnregisterCallback<PointerCaptureOutEvent>(OnCardPointerCaptureOut);
            _captureSource = null;
            _capturedPointerId = -1;
        }

        private void ReleaseCaptureIfAny()
        {
            if (_captureSource != null) ReleaseCapture(_captureSource);
        }

        #endregion

        #region 拖拽

        private void EnterDragging(int cardIndex, VisualElement source, Vector2 pointerPos)
        {
            // 互斥：进入拖拽强制清掉预览态与所有 hover
            ExitPreview();
            ClearAllHoverState();

            _activeCardIndex = cardIndex;

            source.AddToClassList("card-item--placeholder");

            _dragGhost = CreateDragGhost(source);
            // 关键：在 Add 之前先写 left/top，避免 UI Toolkit 用默认值做 layout pass。
            _dragGhost.style.left = pointerPos.x - CardWidth / 2f;
            _dragGhost.style.top = pointerPos.y - CardHeight / 2f;

            // 加到 preview-layer：BattlePanel 内的 absolute 0/0/0/0 浮层，
            //  ① USS 通过 BattlePanel.uxml 的 <Style> 引入，自然继承
            //  ② 自身 absolute 不参与 flex，加子元素不会触发 BattlePanel/GameScreen 的 flex 重排
            //  ③ 与 panel root 同坐标系（沿 hierarchy 都是 absolute fill 容器），evt.position 直接可用
            VisualElement ghostHost = _previewLayer;
            if (ghostHost == null)
            {
                Log.Warning("[GameScreen] _previewLayer 缺失，回退到 GameScreen 作为 ghost 容器");
                ghostHost = this;
            }
            ghostHost.Add(_dragGhost);

            if (_dropZone != null) _dropZone.AddToClassList("active");
        }

        private void ExitDragging()
        {
            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;

            foreach (var card in _cardItems)
            {
                card.RemoveFromClassList("card-item--placeholder");
            }

            if (_dropZone != null) _dropZone.RemoveFromClassList("active");
        }

        private VisualElement CreateDragGhost(VisualElement source)
        {
            var ghost = new VisualElement();
            ghost.AddToClassList("card-ghost");
            ghost.pickingMode = PickingMode.Ignore;

            var nameLabel = source.Q<Label>("card-name");
            if (nameLabel != null)
            {
                var ghostLabel = new Label(nameLabel.text);
                ghostLabel.AddToClassList("card-name");
                ghost.Add(ghostLabel);
            }

            var costLabel = source.Q<Label>("card-cost");
            if (costLabel != null)
            {
                var ghostCost = new Label(costLabel.text);
                ghostCost.AddToClassList("card-cost");
                ghost.Add(ghostCost);
            }

            return ghost;
        }

        private void UpdateGhostPosition(Vector2 panelPosition)
        {
            if (_dragGhost == null) return;
            _dragGhost.style.left = panelPosition.x - CardWidth / 2f;
            _dragGhost.style.top = panelPosition.y - CardHeight / 2f;
        }

        /// <summary>
        /// 拖拽释放在 drop-zone 外时，让 ghost 通过 USS transition 平滑回弹到原卡位置后销毁。
        /// </summary>
        private void StartReboundAnimation(VisualElement origCard)
        {
            if (_dragGhost == null || origCard == null)
            {
                ExitDragging();
                SetState(CardInteractionState.Idle, -1);
                return;
            }

            // 启用 transition 类后再写 left/top，触发 0.15s 平滑回弹
            _dragGhost.AddToClassList("card-ghost--rebounding");

            // ghost 实际挂在 _previewLayer（或回退 this）下，用 ghost.parent 的本地坐标系做转换
            var targetWorld = origCard.worldBound.center;
            var ghostParent = _dragGhost.hierarchy.parent ?? (VisualElement)this;
            var targetLocal = ghostParent.WorldToLocal(targetWorld);
            _dragGhost.style.left = targetLocal.x - CardWidth / 2f;
            _dragGhost.style.top = targetLocal.y - CardHeight / 2f;

            schedule.Execute(() =>
            {
                ExitDragging();
                SetState(CardInteractionState.Idle, -1);
                Log.Info("[GameScreen] 卡牌拖拽回弹完成");
            }).StartingIn(ReboundDurationMs);
        }

        #endregion

        #region 预览

        private void TogglePreview(int cardIndex, VisualElement source)
        {
            if (_state == CardInteractionState.Previewing && _activeCardIndex == cardIndex)
            {
                ExitPreview();
                SetState(CardInteractionState.Idle, -1);
                return;
            }

            ExitPreview();
            EnterPreview(cardIndex, source);
            SetState(CardInteractionState.Previewing, cardIndex);
        }

        private void EnterPreview(int cardIndex, VisualElement source)
        {
            if (_previewLayer == null || _cardItemVta == null || source == null) return;
            ClearAllHoverState();

            var hand = ViewModel.Hand.Value;
            if (hand == null || cardIndex < 0 || cardIndex >= hand.Count) return;
            var card = hand[cardIndex];

            var template = _cardItemVta.CloneTree();
            var clone = template.Q(className: "card-item");
            if (clone == null) return;
            clone.RemoveFromHierarchy();
            clone.AddToClassList("card-item--preview");
            clone.pickingMode = PickingMode.Ignore;

            var nameLabel = clone.Q<Label>("card-name");
            if (nameLabel != null) nameLabel.text = card.Config.Name;
            var costLabel = clone.Q<Label>("card-cost");
            if (costLabel != null) costLabel.text = card.Config.Cost.ToString();

            // 锚点：原卡未旋转 layout 顶部中心 → 转换到 preview-layer 局部坐标
            // 这样克隆卡（transform-origin: 50% 100%）放大 1.6× 时，
            // 视觉上像从原卡顶部"长大"出来。
            if (_handFan != null)
            {
                var sourceTopCenterInHandFan = new Vector2(source.layout.center.x, source.layout.yMin);
                var worldPos = _handFan.LocalToWorld(sourceTopCenterInHandFan);
                var localInPreview = _previewLayer.WorldToLocal(worldPos);
                clone.style.left = localInPreview.x - CardWidth / 2f;
                clone.style.top = localInPreview.y - CardHeight;
            }

            _previewLayer.Add(clone);
            _previewClone = clone;
        }

        private void ExitPreview()
        {
            _previewClone?.RemoveFromHierarchy();
            _previewClone = null;
        }

        #endregion

        #region 悬停

        private void OnCardPointerEnter(VisualElement source)
        {
            if (_state != CardInteractionState.Idle) return;
            source.AddToClassList("card-item--hovering");
        }

        private void OnCardPointerLeave(VisualElement source)
        {
            source.RemoveFromClassList("card-item--hovering");
        }

        private void ClearAllHoverState()
        {
            foreach (var card in _cardItems)
            {
                card.RemoveFromClassList("card-item--hovering");
            }
        }

        #endregion

        private void RefreshInfo()
        {
            var phase = ViewModel.Phase.Value;
            bool levelComplete = ViewModel.IsLevelComplete.Value;
            bool playerDead = ViewModel.IsPlayerDead.Value;
            int hp = ViewModel.PlayerHp.Value;
            int maxHp = ViewModel.PlayerMaxHp.Value;
            int armor = ViewModel.PlayerArmor.Value;
            int energy = ViewModel.Energy.Value;
            int maxEnergy = ViewModel.MaxEnergy.Value;

            if (_infoLabel != null)
            {
                string phaseLabel = phase switch
                {
                    BattlePhase.Prepare => "准备阶段",
                    BattlePhase.PlayerTurn => "你的回合",
                    BattlePhase.MonsterTurn => "怪物回合",
                    BattlePhase.Check => "判定中",
                    BattlePhase.Reward => "选择奖励",
                    BattlePhase.Idle => "等待中",
                    _ => phase.ToString()
                };

                _infoLabel.text = levelComplete ? "关卡完成！" : playerDead ? "玩家死亡" : phaseLabel;
            }

            if (_hpBarFill != null && maxHp > 0)
            {
                float hpPercent = (float)hp / maxHp * 100;
                _hpBarFill.style.width = new StyleLength(new Length(hpPercent, LengthUnit.Percent));
            }

            if (_hpText != null) _hpText.text = $"{hp}/{maxHp}";
            if (_armorText != null) _armorText.text = armor > 0 ? armor.ToString() : "0";

            if (_energyBarFill != null && maxEnergy > 0)
            {
                float energyPercent = (float)energy / maxEnergy * 100;
                _energyBarFill.style.width = new StyleLength(new Length(energyPercent, LengthUnit.Percent));
            }

            if (_energyText != null) _energyText.text = $"{energy}/{maxEnergy}";
            if (_endTurnBtn != null) _endTurnBtn.SetEnabled(phase == BattlePhase.PlayerTurn);
        }

        private static void ClearItems(List<VisualElement> items)
        {
            foreach (var item in items) item.RemoveFromHierarchy();
            items.Clear();
        }

        /// <inheritdoc />
        public override void OnDispose()
        {
            // 释放可能仍持有的指针捕获与回调
            if (_captureSource != null)
            {
                ReleaseCapture(_captureSource);
            }

            ExitDragging();
            ExitPreview();

            DetachHandFanGeometry();

            ClearItems(_monsterItems);
            ClearItems(_cardItems);
            _mainRegion?.Clear();

            base.OnDispose();
        }
    }
}
