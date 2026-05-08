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
    /// </summary>
    public class GameScreen : Screen<GameViewModel>
    {
        // 常驻区域元素（GameView.uxml 中的 player-status / info-bar）
        private Label _infoLabel;
        private VisualElement _hpBarFill;
        private Label _hpText;
        private Label _armorText;
        private VisualElement _energyBarFill;
        private Label _energyText;

        // Region
        private Region _mainRegion;
        private BattlePhase _activeRegionPhase = BattlePhase.Idle;

        // Battle 子元素（在 BattlePanel 加载完成后绑定）
        private VisualElement _monsterContainer;
        private ScrollView _cardScroll;
        private VisualElement _dropZone;
        private Button _endTurnBtn;

        // Reward 子元素
        private Button _rewardConfirmBtn;

        // 子项模板
        private VisualTreeAsset _monsterItemVta;
        private VisualTreeAsset _cardItemVta;

        // 子项追踪
        private readonly List<VisualElement> _monsterItems = new();
        private readonly List<VisualElement> _cardItems = new();

        // 拖拽状态
        private VisualElement _dragGhost;
        private int _dragCardIndex = -1;
        private bool _isDragging;

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

            _monsterContainer = content.Q("monster-container");
            _cardScroll = content.Q<ScrollView>("card-scroll");
            _dropZone = content.Q("drop-zone");
            _endTurnBtn = content.Q<Button>("end-turn-btn");

            if (_endTurnBtn != null)
            {
                _endTurnBtn.RegisterCallback<ClickEvent>(_ => ViewModel.EndTurn());
            }

            RefreshMonsters();
            RefreshCards();
            RefreshInfo();
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

        private void RefreshCards()
        {
            ClearItems(_cardItems);
            if (_cardScroll == null) return;

            var content = _cardScroll.contentContainer;
            var hand = ViewModel.Hand.Value;
            if (hand == null) return;

            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                int index = i;
                if (_cardItemVta == null) continue;

                var item = _cardItemVta.CloneTree();

                var nameLabel = item.Q<Label>("card-name");
                if (nameLabel != null) nameLabel.text = card.Config.Name;

                var costLabel = item.Q<Label>("card-cost");
                if (costLabel != null) costLabel.text = card.Config.Cost.ToString();

                item.RegisterCallback<PointerDownEvent>(evt => OnCardPointerDown(evt, index, item));
                content.Add(item);
                _cardItems.Add(item);
            }
        }

        #region 卡牌拖拽

        private void OnCardPointerDown(PointerDownEvent evt, int cardIndex, VisualElement source)
        {
            if (_isDragging) return;
            if (ViewModel.Phase.Value != BattlePhase.PlayerTurn) return;

            _dragCardIndex = cardIndex;
            _isDragging = true;

            if (_dropZone != null) _dropZone.AddToClassList("active");

            _dragGhost = CreateDragGhost(source);
            Add(_dragGhost);
            UpdateGhostPosition(evt.position);

            RegisterCallback<PointerMoveEvent>(OnDragMove);
            RegisterCallback<PointerUpEvent>(OnDragEnd);
            evt.StopPropagation();
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (!_isDragging || _dragGhost == null) return;
            UpdateGhostPosition(evt.position);
            evt.StopPropagation();
        }

        private void OnDragEnd(PointerUpEvent evt)
        {
            if (!_isDragging) return;

            UnregisterCallback<PointerMoveEvent>(OnDragMove);
            UnregisterCallback<PointerUpEvent>(OnDragEnd);

            bool dropped = _dropZone != null && _dropZone.worldBound.Contains(evt.position);
            if (dropped)
            {
                ViewModel.UseCard(_dragCardIndex);
                Log.Info($"[GameScreen] 卡牌使用：索引 {_dragCardIndex}");
            }

            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;
            _isDragging = false;
            _dragCardIndex = -1;

            if (_dropZone != null) _dropZone.RemoveFromClassList("active");
            evt.StopPropagation();
        }

        private static VisualElement CreateDragGhost(VisualElement source)
        {
            var ghost = new VisualElement();
            ghost.AddToClassList("card-ghost");

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

        private void UpdateGhostPosition(Vector2 position)
        {
            if (_dragGhost == null) return;
            _dragGhost.style.left = position.x - 75;
            _dragGhost.style.top = position.y - 115;
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
            if (_isDragging)
            {
                UnregisterCallback<PointerMoveEvent>(OnDragMove);
                UnregisterCallback<PointerUpEvent>(OnDragEnd);
                _dragGhost?.RemoveFromHierarchy();
            }

            ClearItems(_monsterItems);
            ClearItems(_cardItems);
            _mainRegion?.Clear();

            base.OnDispose();
        }
    }
}
