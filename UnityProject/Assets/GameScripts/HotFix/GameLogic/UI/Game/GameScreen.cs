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
            Dragging,
            SelectingTarget // SingleManual 卡释放在 drop-zone 后，等玩家点选具体怪物
        }

        /// <summary>拖拽态下的子模式（仅 _state == Dragging 时有效）。</summary>
        private enum DragMode
        {
            Detached,      // 中间地带：被拖卡脱离扇形，剩余卡按 N-1 紧凑排
            InsertSlot,    // hand-fan 内：留出一个空槽 + 半透明占位卡
            OverDropZone   // drop-zone 内：等同 Detached，但松手会出牌
        }

        // === 常驻区域元素（GameView.uxml 中的 player-status / info-bar）===
        private Label _infoLabel;
        private VisualElement _hpBarFill;
        private Label _hpText;
        private Label _armorText;
        private VisualElement _energyBarFill;
        private Label _energyText;
        private VisualElement _playerBuffBar;

        // === Region ===
        private Region _mainRegion;
        private BattlePhase _activeRegionPhase = BattlePhase.Idle;

        // === Battle 子元素（在 BattlePanel 加载完成后绑定）===
        private VisualElement _monsterContainer;
        private VisualElement _handFan;
        private VisualElement _previewLayer;
        private VisualElement _dropZone;
        private Button _endTurnBtn;
        private Label _failToast;
        private long _failToastVersion;
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
        private DragMode _dragMode = DragMode.Detached;
        private int _activeCardIndex = -1;   // 被拖卡在 _cardItems 中的当前视觉位置（reorder 后会变）
        private int _activeHandIndex = -1;   // 被拖卡在 ViewModel.Hand 中的位置（closure 捕获，reorder 不影响）
        private int _insertSlotIndex = -1;
        private Vector2 _pointerStartPos;
        private int _capturedPointerId = -1;
        private VisualElement _captureSource;
        private VisualElement _dragGhost;
        private VisualElement _previewClone;
        private VisualElement _insertSlotElement;
        private VisualElement _previewSource; // 当前预览态对应的源卡（用引用判断"是否同卡"，避免 reorder 后索引比较失效）

        // === SelectingTarget 子状态 ===
        private int _selectingTargetCardIndex = -1;
        private VisualElement _selectingTargetGhost;
        private readonly List<EventCallback<ClickEvent>> _monsterClickHandlers = new();
        private EventCallback<KeyDownEvent> _selectingTargetKeyHandler;
        private EventCallback<PointerDownEvent> _selectingTargetBackdropHandler;

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
            _playerBuffBar = this.Q("player-buff-bar");

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

            // 订阅出牌失败事件，显示红色 toast
            ViewModel.CardPlayFailed += OnCardPlayFailed;

            // 订阅玩家 Buff 列表变化，刷新 buff bar
            ViewModel.PlayerBuffs.Changed += _ => RefreshPlayerBuffBar();

            Log.Info("[GameScreen] 局内界面绑定完成");
        }

        /// <summary>
        /// 渲染玩家 Buff 状态条。
        /// </summary>
        private void RefreshPlayerBuffBar()
        {
            var buffs = ViewModel.PlayerBuffs.Value;
            // 把 IReadOnlyList 转为 IList 给 RenderBuffBar；这里直接传入 list 视图的 wrapper
            RenderBuffBar(_playerBuffBar, buffs as IList<BuffRuntime> ?? ToList(buffs));
        }

        private static IList<BuffRuntime> ToList(IReadOnlyList<BuffRuntime> src)
        {
            if (src == null) return null;
            var list = new List<BuffRuntime>(src.Count);
            for (int i = 0; i < src.Count; i++) list.Add(src[i]);
            return list;
        }

        /// <summary>
        /// 出牌失败 → 显示红色 toast，根据 reason 映射中文，1.2 秒后淡出。
        /// </summary>
        private void OnCardPlayFailed(string reason)
        {
            if (_failToast == null) return;

            string text = reason switch
            {
                "InsufficientEnergy" => "能量不足",
                "NotPlayerTurn" => "现在不是你的回合",
                "InvalidTarget" => "无效目标",
                "InvalidHandIndex" => "卡牌索引错误",
                _ => "出牌失败",
            };

            _failToast.text = text;
            _failToast.AddToClassList("fail-toast--visible");

            // 用版本号实现"新失败覆盖旧失败"：每次显示自增，定时器只在版本一致时才隐藏
            long ver = ++_failToastVersion;
            schedule.Execute(() =>
            {
                if (ver == _failToastVersion && _failToast != null)
                {
                    _failToast.RemoveFromClassList("fail-toast--visible");
                }
            }).StartingIn(1200);
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

            // 阶段切换强制取消 SelectingTarget（避免怪物回合期间留着选目标 UI）
            if (_state == CardInteractionState.SelectingTarget && phase != BattlePhase.PlayerTurn)
            {
                CancelSelectingTarget();
            }

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
            _failToast = content.Q<Label>("fail-toast");

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

        /// <summary>
        /// 收集指定卡的全部 CardEffect 行（按表中顺序）。供 PendingCards 意图渲染使用。
        /// </summary>
        private static List<GameConfig.card.CardEffect> ResolveCardEffects(GameConfig.card.Card card)
        {
            var result = new List<GameConfig.card.CardEffect>();
            if (card == null) return result;
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return result;

            foreach (var effect in tables.TbCardEffect.DataList)
            {
                if (effect.CardId == card.Id) result.Add(effect);
            }
            return result;
        }

        /// <summary>
        /// 计算当前存活的玩家敌方（即怪物）数量，供 SplitAcrossAll 平分伤害文本计算。
        /// </summary>
        private int CountAliveMonsters()
        {
            var monsters = ViewModel.Monsters.Value;
            if (monsters == null) return 0;
            int n = 0;
            foreach (var m in monsters)
            {
                if (m != null && !m.IsDead) n++;
            }
            return n;
        }

        /// <summary>
        /// 把一张 PendingCard 的 effects 列表渲染到 intent-container 中：每张卡一个 .intent-card，
        /// 每条 effect 一个 .intent-icon，按 EffectKind 加颜色类与文本格式。
        /// </summary>
        private void RenderIntentCard(VisualElement intentContainer, GameConfig.card.Card card, int aliveMonsterCount)
        {
            if (intentContainer == null || card == null) return;

            var effects = ResolveCardEffects(card);
            if (effects.Count == 0) return;

            var intentCard = new VisualElement();
            intentCard.AddToClassList("intent-card");
            intentCard.pickingMode = PickingMode.Ignore;

            bool isSplit = card.TargetMode == GameConfig.card.TargetMode.SplitAcrossAll;

            foreach (var effect in effects)
            {
                var icon = new Label();
                icon.AddToClassList("intent-icon");
                icon.pickingMode = PickingMode.Ignore;

                int displayValue = effect.Value;
                if (isSplit && effect.Kind == GameConfig.card.EffectKind.Damage && aliveMonsterCount > 0)
                {
                    displayValue = Math.Max(1, effect.Value / aliveMonsterCount);
                }

                switch (effect.Kind)
                {
                    case GameConfig.card.EffectKind.Damage:
                        icon.AddToClassList("intent-icon-damage");
                        icon.text = displayValue.ToString();
                        break;
                    case GameConfig.card.EffectKind.Shield:
                        icon.AddToClassList("intent-icon-shield");
                        icon.text = displayValue.ToString();
                        break;
                    case GameConfig.card.EffectKind.DamageDot:
                        icon.AddToClassList("intent-icon-dot");
                        icon.text = $"{displayValue}×{effect.Duration}";
                        break;
                    case GameConfig.card.EffectKind.EnergyGain:
                        icon.AddToClassList("intent-icon-energy");
                        icon.text = $"+{displayValue}";
                        break;
                    default:
                        icon.text = displayValue.ToString();
                        break;
                }

                intentCard.Add(icon);
            }

            intentContainer.Add(intentCard);
        }

        /// <summary>
        /// 把 actor 的 Buffs 列表渲染到 buff-bar 中：每条 buff 一个 .buff-icon，按 Kind 加颜色类。
        /// </summary>
        private static void RenderBuffBar(VisualElement buffBar, IList<BuffRuntime> buffs)
        {
            if (buffBar == null) return;
            buffBar.Clear();
            if (buffs == null || buffs.Count == 0) return;

            foreach (var buff in buffs)
            {
                if (buff == null) continue;

                var icon = new Label();
                icon.AddToClassList("buff-icon");
                icon.pickingMode = PickingMode.Ignore;

                switch (buff.Kind)
                {
                    case GameConfig.card.EffectKind.DamageDot:
                        icon.AddToClassList("buff-icon-dot");
                        break;
                }

                icon.text = $"{buff.Value}×{buff.RemainingTurns}";
                buffBar.Add(icon);
            }
        }

        private void RefreshMonsters()
        {
            ClearItems(_monsterItems);
            if (_monsterContainer == null) return;

            var monsters = ViewModel.Monsters.Value;
            if (monsters == null) return;

            int aliveCount = CountAliveMonsters();

            foreach (var monster in monsters)
            {
                if (monster.Hp <= 0) continue;
                if (_monsterItemVta == null) continue;

                var item = _monsterItemVta.CloneTree();

                // 兼容旧 intent-text 标签（清空，避免遗留文本影响新意图区显示）
                var intentLabel = item.Q<Label>("intent-text");
                if (intentLabel != null) intentLabel.text = string.Empty;

                // 新意图渲染：每张 PendingCard 一个 .intent-card，按 effects 列表展示
                var intentContainer = item.Q("intent-container");
                if (intentContainer != null && monster.PendingCards != null)
                {
                    intentContainer.Clear();
                    foreach (var card in monster.PendingCards)
                    {
                        RenderIntentCard(intentContainer, card, aliveCount);
                    }
                }

                // 怪物 buff 状态条
                var buffBar = item.Q("buff-bar");
                RenderBuffBar(buffBar, monster.Buffs);

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
            }

            // 全部加入 _cardItems 后统一按 N 张紧凑布局重排（活跃卡为 -1）+ 同步 sibling 顺序
            RecomputeHandLayout(-1, DragMode.Detached, -1);
            SyncSiblingOrder();
        }

        /// <summary>
        /// 按设计公式计算第 slotIndex 槽位（共 slotCount 槽）的扇形 transform，并写入 inline style。
        /// </summary>
        /// <param name="card">要应用 transform 的卡牌元素。</param>
        /// <param name="slotIndex">该卡占据的槽位索引（基于 slotCount 计算 centerIndex）。</param>
        /// <param name="slotCount">扇形总槽位数（拖拽 InsertSlot 模式 = N，Detached 模式 = N-1）。</param>
        /// <param name="skipSlot">空槽索引，仅作为调用方意图说明（实际跳过逻辑由调用方分配 slotIndex 实现），-1 表示无空槽。</param>
        private void ApplyFanTransform(VisualElement card, int slotIndex, int slotCount, int skipSlot = -1)
        {
            if (card == null || slotCount <= 0) return;

            float fanWidth = ResolveSize(_handFan, true, 800f);
            float fanHeight = ResolveSize(_handFan, false, 280f);

            float center = (slotCount - 1) / 2f;
            float offset = slotIndex - center;

            float spacing = slotCount > 1
                ? Mathf.Min(MaxCardSpacing, (fanWidth - CardWidth) / (slotCount - 1))
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
        /// 按当前拖拽态/子态分发槽位给所有手牌（含占位卡），重新计算扇形 transform。
        /// </summary>
        /// <param name="activeIndex">被拖卡在 _cardItems 中的索引；-1 表示无拖拽。</param>
        /// <param name="mode">当前拖拽子态（无拖拽时传 Detached，等价于全部 N 张紧凑布局）。</param>
        /// <param name="insertSlot">InsertSlot 模式下的空槽索引；其他模式忽略。</param>
        private void RecomputeHandLayout(int activeIndex, DragMode mode, int insertSlot)
        {
            int n = _cardItems.Count;
            if (n == 0) return;

            // 无拖拽：全部按 N 张紧凑布局
            if (activeIndex < 0)
            {
                for (int i = 0; i < n; i++)
                {
                    ApplyFanTransform(_cardItems[i], i, n, -1);
                }
                return;
            }

            // 拖拽中：被拖卡跳过；其他卡按子态分配槽位
            int slotCount = mode == DragMode.InsertSlot ? n : Mathf.Max(1, n - 1);
            int skipSlot = mode == DragMode.InsertSlot ? insertSlot : -1;

            int linearPos = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == activeIndex) continue;

                int slotIdx;
                if (mode == DragMode.InsertSlot)
                {
                    // 在 N 槽中跳过 insertSlot
                    slotIdx = linearPos < insertSlot ? linearPos : linearPos + 1;
                }
                else
                {
                    // Detached / OverDropZone：N-1 紧凑排
                    slotIdx = linearPos;
                }

                ApplyFanTransform(_cardItems[i], slotIdx, slotCount, skipSlot);
                linearPos++;
            }

            // InsertSlot 模式下占位卡占据 insertSlot
            if (mode == DragMode.InsertSlot && _insertSlotElement != null && insertSlot >= 0)
            {
                ApplyFanTransform(_insertSlotElement, insertSlot, slotCount, -1);
            }

            // 注意：sibling 顺序不在此处同步。每帧 PointerMove 都会调 RecomputeHandLayout，
            // 频繁 BringToFront 会扰动 hierarchy → 破坏 USS transition baseline → 回弹无动画。
            // SyncSiblingOrder 改为只在列表顺序真正变化处调用（RefreshCards 末尾、ReorderCardItems 末尾）。
        }

        /// <summary>
        /// 同步 _handFan 子项 sibling 顺序：按 _cardItems 列表顺序依次 BringToFront，
        /// 最终 sibling order = list order（c0 在底、c[N-1] 在顶），呈现"低 → 高"单向递增的层叠效果，
        /// 视觉上类似手持一叠扇形牌。
        /// 同时修复 ReorderCardItems 后 _cardItems 与 _handFan hierarchy 顺序不同步的隐患。
        /// 占位卡（仅 InsertSlot 子态存在）始终保持最上。
        /// </summary>
        private void SyncSiblingOrder()
        {
            // 按 list 顺序逐一 BringToFront：c0 → last，c1 → last（c0 退到 last-1），…，
            // c[N-1] → last（最终 z-order 最高）。结果：sibling[i] == _cardItems[i]
            foreach (var card in _cardItems)
            {
                card.BringToFront();
            }

            _insertSlotElement?.BringToFront();
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
        /// 拖拽中也会响应 resize，按当前子态分发槽位。
        /// </summary>
        private void OnHandFanGeometryChanged(GeometryChangedEvent evt)
        {
            if (_state == CardInteractionState.Dragging && _activeCardIndex >= 0)
            {
                RecomputeHandLayout(_activeCardIndex, _dragMode, _insertSlotIndex);
            }
            else
            {
                RecomputeHandLayout(-1, DragMode.Detached, -1);
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

        private void OnCardPointerDown(PointerDownEvent evt, int handIndex, VisualElement source)
        {
            // 只在玩家回合允许卡牌交互
            if (ViewModel.Phase.Value != BattlePhase.PlayerTurn) return;
            // 已有 capture 则忽略（防止多指同时按）
            if (_captureSource != null) return;

            _pointerStartPos = evt.position;
            _capturedPointerId = evt.pointerId;
            _captureSource = source;

            // closure 中的 handIndex 对应 ViewModel.Hand 中的位置（reorder 不变），用于 UseCard
            _activeHandIndex = handIndex;
            // 视觉位置实时查找：reorder 后 _cardItems 顺序变了，handIndex 不再等于视觉索引
            _activeCardIndex = _cardItems.IndexOf(source);
            if (_activeCardIndex < 0)
            {
                Log.Warning("[GameScreen] PointerDown source 不在 _cardItems 中，回退到 closure handIndex");
                _activeCardIndex = handIndex;
            }

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
                UpdateDragSubMode(evt.position);
            }

            evt.StopPropagation();
        }

        /// <summary>
        /// 按指针位置判定拖拽子态，按需切换 enter/exit 与重排。
        /// 优先级：OverDropZone &gt; InsertSlot &gt; Detached。
        /// </summary>
        private void UpdateDragSubMode(Vector2 pointerPos)
        {
            DragMode newMode = DetermineDragMode(pointerPos);
            DragMode oldMode = _dragMode;

            if (newMode == DragMode.InsertSlot)
            {
                int slot = ComputeInsertSlot(pointerPos);
                if (oldMode != DragMode.InsertSlot)
                {
                    EnterInsertSlotMode(slot);
                }
                else
                {
                    UpdateInsertSlot(slot);
                }
            }
            else
            {
                if (oldMode == DragMode.InsertSlot)
                {
                    // 离开 hand-fan 区域，回到 Detached 紧凑布局
                    ExitInsertSlotMode();
                }
                _dragMode = newMode;
            }
        }

        /// <summary>按 worldBound 命中判断当前子态。</summary>
        private DragMode DetermineDragMode(Vector2 pointerPos)
        {
            if (_dropZone != null && _dropZone.worldBound.Contains(pointerPos))
                return DragMode.OverDropZone;
            if (_handFan != null && _handFan.worldBound.Contains(pointerPos))
                return DragMode.InsertSlot;
            return DragMode.Detached;
        }

        private void OnCardPointerUp(PointerUpEvent evt)
        {
            var source = _captureSource;
            if (source == null) return;

            if (_state == CardInteractionState.Dragging)
            {
                // 按当前子态分发松手行为
                switch (_dragMode)
                {
                    case DragMode.OverDropZone:
                    {
                        // 出牌路径全程用 hand index（GetHandCardAt / UseCard / EnterSelectingTarget 都是 ViewModel.Hand 语义）
                        int handIdx = _activeHandIndex;
                        var card = GetHandCardAt(handIdx);
                        bool needsManualTarget = card != null
                            && card.Config.TargetMode == GameConfig.card.TargetMode.SingleManual;

                        if (needsManualTarget)
                        {
                            // 进入选目标态：保留 ghost 浮在 drop-zone 上方，怪物高亮可点
                            ReleaseCapture(source);
                            EnterSelectingTarget(handIdx);
                            // 注意：不调用 ExitDragging（保留 ghost）；状态切换为 SelectingTarget
                            break;
                        }

                        ExitDragging();
                        ReleaseCapture(source);
                        ViewModel.UseCard(handIdx);
                        SetState(CardInteractionState.Idle, -1);
                        break;
                    }
                    case DragMode.InsertSlot:
                    {
                        // 调整 _cardItems 顺序（仅 UI 层），用 visual index（_cardItems 当前位置）
                        int from = _activeCardIndex;
                        int to = _insertSlotIndex;
                        ReorderCardItems(from, to);
                        ExitDragging();
                        ReleaseCapture(source);
                        SetState(CardInteractionState.Idle, -1);
                        break;
                    }
                    case DragMode.Detached:
                    default:
                    {
                        // 中间地带：ghost + 其他卡协同回弹到 N 张布局
                        ReleaseCapture(source);
                        StartReboundAnimation(source);
                        break;
                    }
                }
            }
            else
            {
                // 预览路径用 hand index（EnterPreview 内 hand[idx] 取 CardRuntime）
                int handIdx = _activeHandIndex;
                ReleaseCapture(source);
                TogglePreview(handIdx, source);
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
                ExitDragging(); // 已包含占位卡销毁、可见性还原、子态字段重置
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
            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;

            // 拖拽中其他卡的 transform 变更必须立即生效（无 transition），避免跟手延迟
            // 用 inline style 而非 USS class，避免 class 切换异步导致的 transition baseline 问题
            foreach (var card in _cardItems)
            {
                SetCardTransitionDuration(card, 0f);
            }

            // 被拖卡视觉上从 hand-fan 中"消失"：用 opacity 0 而非 visibility Hidden
            // 原因：visibility 切换会触发 layout 重算，回弹时恢复 visibility 与 ghost 销毁存在 1 帧时序错位
            // opacity 0 + pickingMode Ignore 既保留 layout 一致性，回弹时也能 fade-in 与 ghost 同步过渡
            source.style.opacity = 0f;
            source.pickingMode = PickingMode.Ignore;

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

            // 剩余 N-1 张卡按 N-1 紧凑布局
            RecomputeHandLayout(_activeCardIndex, DragMode.Detached, -1);
        }

        private void ExitDragging()
        {
            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;

            DestroyInsertSlotElement();

            // 不依赖索引：reorder 后 _activeCardIndex 可能不再指向被拖卡，
            // 统一遍历所有 hand-fan 卡：恢复 opacity / pickingMode，清掉拖拽中的 inline duration 与残留类
            foreach (var card in _cardItems)
            {
                card.style.visibility = Visibility.Visible;       // 兜底：异常路径若设过 Hidden
                card.style.opacity = StyleKeyword.Null;            // 恢复 USS 默认 1.0
                card.pickingMode = PickingMode.Position;
                ClearCardTransitionDuration(card);

                // 旧版 placeholder / 历史 USS 类兜底清理
                card.RemoveFromClassList("card-item--placeholder");
                card.RemoveFromClassList("card-item--no-transition");
                card.RemoveFromClassList("card-item--rebounding");
            }

            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;
            _activeHandIndex = -1; // _activeCardIndex 由后续 SetState(Idle, -1) 重置

            if (_dropZone != null) _dropZone.RemoveFromClassList("active");
        }

        /// <summary>
        /// 获取当前手牌中指定索引的 CardRuntime，越界返回 null。
        /// </summary>
        private CardRuntime GetHandCardAt(int handIndex)
        {
            if (ViewModel == null) return null;
            var hand = ViewModel.Hand.Value;
            if (hand == null) return null;
            if (handIndex < 0 || handIndex >= hand.Count) return null;
            return hand[handIndex];
        }

        #region SelectingTarget 选目标态

        /// <summary>
        /// 进入 SelectingTarget：保留 ghost 浮在 drop-zone 上方，给所有存活怪物加 .target-selectable.active 类，
        /// 注册怪物点击 / ESC / 空白点击的取消回调。
        /// </summary>
        private void EnterSelectingTarget(int handIndex)
        {
            _selectingTargetCardIndex = handIndex;
            _selectingTargetGhost = _dragGhost;
            _dragGhost = null; // 转移所有权到 SelectingTarget 持有

            // 给每只存活怪物 item 添加可选目标类与点击回调
            int count = Math.Min(_monsterItems.Count, ViewModel.Monsters.Value?.Count ?? 0);
            _monsterClickHandlers.Clear();
            for (int i = 0; i < count; i++)
            {
                var monster = ViewModel.Monsters.Value[i];
                if (monster == null || monster.IsDead) continue;

                var item = _monsterItems[i];
                int captured = i; // 捕获索引
                item.AddToClassList("target-selectable");
                item.AddToClassList("active");

                EventCallback<ClickEvent> handler = evt =>
                {
                    OnMonsterTargetClicked(captured);
                    evt.StopPropagation();
                };
                _monsterClickHandlers.Add(handler);
                item.RegisterCallback(handler);
            }

            // 注册 ESC + 空白点击取消
            _selectingTargetKeyHandler = evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    CancelSelectingTarget();
                    evt.StopPropagation();
                }
            };
            this.RegisterCallback(_selectingTargetKeyHandler, TrickleDown.TrickleDown);
            this.focusable = true;
            this.Focus();

            _selectingTargetBackdropHandler = evt =>
            {
                // 仅响应非怪物 / 非 drop-zone 的点击为取消
                var target = evt.target as VisualElement;
                if (target == null) return;

                bool isMonsterClick = false;
                foreach (var item in _monsterItems)
                {
                    if (item != null && IsSameOrAncestor(item, target))
                    {
                        isMonsterClick = true;
                        break;
                    }
                }
                if (!isMonsterClick)
                {
                    CancelSelectingTarget();
                    evt.StopPropagation();
                }
            };
            this.RegisterCallback(_selectingTargetBackdropHandler, TrickleDown.TrickleDown);

            SetState(CardInteractionState.SelectingTarget, handIndex);
        }

        /// <summary>玩家点击某只怪物 → 调用 UseCard 后端、清理选目标态。</summary>
        private void OnMonsterTargetClicked(int monsterIndex)
        {
            int handIndex = _selectingTargetCardIndex;
            ExitSelectingTarget(destroyGhost: true);
            ViewModel.UseCard(handIndex, monsterIndex);
            SetState(CardInteractionState.Idle, -1);
        }

        /// <summary>取消选目标态：卡片回弹到原槽位，回到 Idle。</summary>
        private void CancelSelectingTarget()
        {
            if (_state != CardInteractionState.SelectingTarget) return;

            // 把 ghost 还回 _dragGhost 让 StartReboundAnimation 复用既有协同回弹
            _dragGhost = _selectingTargetGhost;
            _selectingTargetGhost = null;

            // 清掉怪物高亮 + 监听
            ClearSelectingTargetMonsterHighlights();

            UnregisterSelectingTargetCallbacks();

            // 找原被拖卡作为回弹源
            VisualElement origCard = null;
            if (_selectingTargetCardIndex >= 0 && _selectingTargetCardIndex < _cardItems.Count)
            {
                origCard = _cardItems[_selectingTargetCardIndex];
            }
            _selectingTargetCardIndex = -1;

            if (origCard != null)
            {
                StartReboundAnimation(origCard);
            }
            else
            {
                _dragGhost?.RemoveFromHierarchy();
                _dragGhost = null;
                SetState(CardInteractionState.Idle, -1);
            }
        }

        /// <summary>退出 SelectingTarget：清高亮、注销监听、按需销毁 ghost。</summary>
        private void ExitSelectingTarget(bool destroyGhost)
        {
            ClearSelectingTargetMonsterHighlights();
            UnregisterSelectingTargetCallbacks();

            if (destroyGhost)
            {
                _selectingTargetGhost?.RemoveFromHierarchy();
                _selectingTargetGhost = null;
            }

            _selectingTargetCardIndex = -1;

            if (_dropZone != null) _dropZone.RemoveFromClassList("active");
        }

        /// <summary>判断 target 是否等于 ancestor 或为其后代。</summary>
        private static bool IsSameOrAncestor(VisualElement ancestor, VisualElement target)
        {
            var current = target;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.parent;
            }
            return false;
        }

        private void ClearSelectingTargetMonsterHighlights()
        {
            for (int i = 0; i < _monsterItems.Count; i++)
            {
                var item = _monsterItems[i];
                if (item == null) continue;
                item.RemoveFromClassList("target-selectable");
                item.RemoveFromClassList("active");
                if (i < _monsterClickHandlers.Count && _monsterClickHandlers[i] != null)
                {
                    item.UnregisterCallback(_monsterClickHandlers[i]);
                }
            }
            _monsterClickHandlers.Clear();
        }

        private void UnregisterSelectingTargetCallbacks()
        {
            if (_selectingTargetKeyHandler != null)
            {
                this.UnregisterCallback(_selectingTargetKeyHandler, TrickleDown.TrickleDown);
                _selectingTargetKeyHandler = null;
            }
            if (_selectingTargetBackdropHandler != null)
            {
                this.UnregisterCallback(_selectingTargetBackdropHandler, TrickleDown.TrickleDown);
                _selectingTargetBackdropHandler = null;
            }
        }

        #endregion

        /// <summary>销毁占位卡（如果存在）。</summary>
        private void DestroyInsertSlotElement()
        {
            _insertSlotElement?.RemoveFromHierarchy();
            _insertSlotElement = null;
        }

        /// <summary>
        /// 通过 inline style 控制 .card-item transition-duration。
        /// 使用 inline style 而非 USS class 是因为 class 切换 + inline style 同帧写入会让 transition baseline 失效，
        /// 导致回弹动画首帧 rotate 错乱。inline style 立即生效，避免该 quirk。
        /// </summary>
        private static void SetCardTransitionDuration(VisualElement card, float seconds)
        {
            var list = new List<TimeValue> { new TimeValue(seconds, TimeUnit.Second) };
            card.style.transitionDuration = new StyleList<TimeValue>(list);
        }

        /// <summary>清除 inline transitionDuration，让 USS 默认值（0.15s）生效。</summary>
        private static void ClearCardTransitionDuration(VisualElement card)
        {
            card.style.transitionDuration = new StyleList<TimeValue>(StyleKeyword.Null);
        }

        /// <summary>
        /// 创建半透明占位卡，加入 hand-fan。视觉上替代被拖卡的位置。
        /// </summary>
        private VisualElement CreateInsertSlotElement(int sourceCardIndex)
        {
            if (_handFan == null || _cardItemVta == null) return null;

            var template = _cardItemVta.CloneTree();
            var slot = template.Q(className: "card-item");
            if (slot == null) return null;
            slot.RemoveFromHierarchy();
            slot.AddToClassList("card-item--insert-slot");
            SetCardTransitionDuration(slot, 0f); // 拖拽中位移无 transition
            slot.pickingMode = PickingMode.Ignore;

            // 复制源卡的文本（视觉一致）
            if (sourceCardIndex >= 0 && sourceCardIndex < _cardItems.Count)
            {
                var source = _cardItems[sourceCardIndex];
                var srcName = source.Q<Label>("card-name");
                var slotName = slot.Q<Label>("card-name");
                if (srcName != null && slotName != null) slotName.text = srcName.text;

                var srcCost = source.Q<Label>("card-cost");
                var slotCost = slot.Q<Label>("card-cost");
                if (srcCost != null && slotCost != null) slotCost.text = srcCost.text;
            }

            _handFan.Add(slot);
            return slot;
        }

        /// <summary>
        /// 按"距最近卡 + 鼠标在其左半 / 右半"算法计算插入槽位（取值范围 [0, N-1]）。
        /// 手牌只剩 1 张时返回 0。
        /// </summary>
        private int ComputeInsertSlot(Vector2 pointerPos)
        {
            int n = _cardItems.Count;
            if (n <= 1) return 0;
            if (_activeCardIndex < 0 || _activeCardIndex >= n) return 0;

            // 在剩余 N-1 张中寻找距 pointer.x 最近的卡
            float bestDist = float.MaxValue;
            int bestVisualIdx = -1; // _cardItems 中的索引
            float bestCenterX = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i == _activeCardIndex) continue;
                var rect = _cardItems[i].worldBound;
                float centerX = rect.center.x;
                float dist = Mathf.Abs(pointerPos.x - centerX);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestVisualIdx = i;
                    bestCenterX = centerX;
                }
            }

            if (bestVisualIdx < 0) return 0;

            // 鼠标在最近卡左半 → 插入到该卡之前；右半 → 插入到该卡之后
            // 注意：返回的是"在 N 槽布局中的目标槽位"，与该卡当前在 N-1 紧凑布局中的位置不同
            // 在 N 槽布局中：目标槽位 = "插入到 _cardItems[bestVisualIdx] 之前的位置在 N 槽中应处的槽"
            // 算法：将 _cardItems 中除 active 外的卡按顺序映射到候选插入点 [0..N-1]：
            //   插入到第 j 个剩余卡之前 → 槽 j（若 j < activeIndex）或槽 j+1（若 j >= activeIndex 在原序列中也跳过）
            // 简化做法：直接基于 bestVisualIdx 与左/右半计算"在 N 槽中应放的位置"
            // - 若鼠标在 best 卡左半：目标槽 = best 卡当前应在的槽位
            // - 若鼠标在 best 卡右半：目标槽 = best 卡当前应在的槽位 + 1
            // best 卡在 N 槽中的"位置"：与其在 _cardItems 中的索引一致（因为 N 槽 = 全部 N 张原位置）
            int slot = pointerPos.x < bestCenterX ? bestVisualIdx : bestVisualIdx + 1;
            return Mathf.Clamp(slot, 0, n - 1);
        }

        /// <summary>进入 InsertSlot 子态：创建占位卡 + 重排剩余卡。</summary>
        private void EnterInsertSlotMode(int insertSlot)
        {
            _dragMode = DragMode.InsertSlot;
            _insertSlotIndex = insertSlot;

            if (_insertSlotElement == null)
            {
                _insertSlotElement = CreateInsertSlotElement(_activeCardIndex);
            }

            RecomputeHandLayout(_activeCardIndex, DragMode.InsertSlot, _insertSlotIndex);
        }

        /// <summary>InsertSlot 子态下插入位置变化时更新布局（占位卡 + 其他卡）。</summary>
        private void UpdateInsertSlot(int newInsertSlot)
        {
            if (newInsertSlot == _insertSlotIndex) return;
            _insertSlotIndex = newInsertSlot;
            RecomputeHandLayout(_activeCardIndex, DragMode.InsertSlot, _insertSlotIndex);
        }

        /// <summary>退出 InsertSlot 子态：销毁占位卡 + 切回 Detached 紧凑布局。</summary>
        private void ExitInsertSlotMode()
        {
            DestroyInsertSlotElement();
            _dragMode = DragMode.Detached;
            _insertSlotIndex = -1;
            RecomputeHandLayout(_activeCardIndex, DragMode.Detached, -1);
        }

        /// <summary>松手在 hand-fan 内时调整 _cardItems 顺序（仅 UI 层，不动 ViewModel）。</summary>
        private void ReorderCardItems(int from, int to)
        {
            int n = _cardItems.Count;
            if (from < 0 || from >= n) return;
            if (to < 0) to = 0;
            if (to >= n) to = n - 1;
            if (from == to) return;

            var dragged = _cardItems[from];
            _cardItems.RemoveAt(from);
            _cardItems.Insert(to, dragged);

            // 按新顺序应用 N 张布局 + 同步 _handFan sibling 与新 list 顺序一致
            RecomputeHandLayout(-1, DragMode.Detached, -1);
            SyncSiblingOrder();
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
        /// 中间地带松手时启动协同回弹：ghost 立即销毁（避免与扇形卡牌 rotate 不一致的视觉错位），
        /// 被拖卡 opacity 立即恢复，其他卡从 N-1（或 N 留空）布局平滑动回 N 张布局。
        /// </summary>
        private void StartReboundAnimation(VisualElement origCard)
        {
            // 立即销毁 ghost：ghost 没有 rotate transform，飞回时与下方扇形卡牌的旋转对不上
            // 所以"松手即消失"是正确表现，让用户视觉上聚焦于卡牌本身的协同回弹
            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;

            // 若当前还在 InsertSlot 子态，先清掉占位 + 切回 Detached 态（不动 _cardItems 顺序）
            if (_dragMode == DragMode.InsertSlot)
            {
                DestroyInsertSlotElement();
                _dragMode = DragMode.Detached;
                _insertSlotIndex = -1;
            }

            // 被拖卡立即恢复 opacity：此时 inline transitionDuration 仍是 0s，立即生效不 fade
            // 因为 ghost 已销毁，source 必须立即可见，否则会有"卡片消失"的视觉空白
            if (_activeCardIndex >= 0 && _activeCardIndex < _cardItems.Count)
            {
                var active = _cardItems[_activeCardIndex];
                active.style.opacity = 1f;
                active.pickingMode = PickingMode.Position;
            }

            // 启用回弹过渡：其他卡从 N-1（或 N 留空）布局平滑动回 N 张布局
            foreach (var card in _cardItems)
            {
                SetCardTransitionDuration(card, 0.15f);
            }

            // 写入 N 张目标布局 → 其他卡 transition 0.15s 回原位（被拖卡 transform 与原槽一致，无 transition）
            RecomputeHandLayout(-1, DragMode.Detached, -1);

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
            // 用 source 引用判断"是否同一张卡"：reorder 后 _activeCardIndex（visual）与 cardIndex（hand）不再可比
            if (_state == CardInteractionState.Previewing && _previewSource == source)
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
            _previewSource = source;
        }

        private void ExitPreview()
        {
            _previewClone?.RemoveFromHierarchy();
            _previewClone = null;
            _previewSource = null;
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

            ExitDragging();   // 已包含占位卡销毁
            ExitPreview();
            DestroyInsertSlotElement(); // 兜底：极端路径下保险清理

            DetachHandFanGeometry();

            ClearItems(_monsterItems);
            ClearItems(_cardItems);
            _mainRegion?.Clear();

            base.OnDispose();
        }
    }
}
