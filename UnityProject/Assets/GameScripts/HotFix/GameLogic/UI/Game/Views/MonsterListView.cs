using System;
using System.Collections.Generic;
using EF.Debugger;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 怪物列表视图控制器。订阅 IMonsterListContext.Monsters，全量重建 monster-container 内的怪物项。
    /// 每只存活怪物对应一个 MonsterItemView 实例；死亡怪物不创建项。
    /// </summary>
    public class MonsterListView : IDisposable, IMonsterTargetSurface
    {
        private VisualElement _container;
        private IMonsterListContext _context;
        private VisualTreeAsset _itemTemplate;
        private readonly List<MonsterItemView> _items = new();
        private Action<IReadOnlyList<MonsterRuntime>> _onMonstersChanged;
        private bool _disposed;

        // === Target 模式（由 TargetSelector 通过 EnterTargetMode/ExitTargetMode 切换）===
        private bool _targetModeActive;
        private Action<int> _onTargetClick;
        private readonly List<EventCallback<ClickEvent>> _targetClickHandlers = new();

        /// <summary>
        /// 当前已渲染的怪物项列表（仅含 Hp > 0 的存活怪物，按 Monsters 列表顺序）。
        /// 供 GameView 选目标态等需要遍历怪物视觉元素的代码读取，禁止外部 mutate。
        /// </summary>
        public IReadOnlyList<MonsterItemView> Items => _items;

        /// <summary>
        /// 构造怪物列表视图，订阅 Monsters 变化并触发首次刷新。
        /// </summary>
        /// <param name="monsterContainer">monster-container 容器（BattlePanel.uxml）。</param>
        /// <param name="context">实现 IMonsterListContext 的切片对象（生产为 GameViewModel）。</param>
        /// <param name="monsterItemTemplate">MonsterItem.uxml 的 VisualTreeAsset；为 null 时不渲染（防御性，不抛异常）。</param>
        public MonsterListView(VisualElement monsterContainer, IMonsterListContext context, VisualTreeAsset monsterItemTemplate)
        {
            _container = monsterContainer;
            _context = context;
            _itemTemplate = monsterItemTemplate;

            if (_context == null)
            {
                Log.Warning("[MonsterListView] context 为 null，跳过订阅");
                return;
            }

            _onMonstersChanged = OnMonstersChanged;
            _context.Monsters.Changed += _onMonstersChanged;

            // 首次同步：避免首帧空白
            Refresh();
        }

        private void OnMonstersChanged(IReadOnlyList<MonsterRuntime> monsters)
        {
            if (_disposed) return;
            Refresh();
        }

        /// <summary>
        /// 全量重建怪物项列表。先 Dispose 旧 MonsterItemView 并 RemoveFromHierarchy，
        /// 再按 Monsters 列表顺序为每只 Hp > 0 的怪物 CloneTree 一份模板并装配 MonsterItemView。
        /// </summary>
        private void Refresh()
        {
            if (_container == null) return;

            // 销毁旧项
            foreach (var item in _items)
            {
                item.Root?.RemoveFromHierarchy();
                item.Dispose();
            }
            _items.Clear();

            var monsters = _context?.Monsters.Value;
            if (monsters == null || _itemTemplate == null) return;

            int aliveCount = CountAlive(monsters);

            foreach (var monster in monsters)
            {
                if (monster == null || monster.Hp <= 0) continue;

                var template = _itemTemplate.CloneTree();
                // 取真正带 .monster-item class 的内层 VisualElement，从 TemplateContainer 中分离
                var root = template.Q(className: "monster-item");
                if (root == null)
                {
                    // 兼容：若模板未使用 .monster-item 标记类，直接使用 TemplateContainer
                    root = template;
                }
                else
                {
                    root.RemoveFromHierarchy();
                }

                var view = new MonsterItemView(root, monster, aliveCount);
                _container.Add(root);
                _items.Add(view);
            }

            // 若处于 target 模式，重建后需要对新存活怪物重新应用 target-selectable 类与点击回调
            // （否则 Monsters.Changed 触发刷新后高亮丢失）
            if (_targetModeActive && _onTargetClick != null)
            {
                ApplyTargetMode(_onTargetClick);
            }
        }

        // ── Target 模式 API（供 TargetSelector 编排）──

        /// <summary>
        /// 进入 target 模式：对每只存活怪物 Root 添加 target-selectable.active CSS 类，
        /// 注册临时 ClickEvent 回调（点击时调 onMonsterClick(monsterIdx) 并 StopPropagation）。
        /// 设置 _targetModeActive=true，刷新后会重新应用，避免列表重建后高亮丢失。
        /// </summary>
        public void EnterTargetMode(Action<int> onMonsterClick)
        {
            if (_disposed) return;

            // 若上次未退出，先清掉旧状态避免重复回调
            if (_targetModeActive) ClearTargetClassesAndHandlers();

            _targetModeActive = true;
            _onTargetClick = onMonsterClick;
            ApplyTargetMode(onMonsterClick);
        }

        /// <summary>退出 target 模式：移除全部 target-selectable.active 类、解临时点击回调、清缓存。</summary>
        public void ExitTargetMode()
        {
            if (_disposed) return;
            ClearTargetClassesAndHandlers();
            _targetModeActive = false;
            _onTargetClick = null;
        }

        /// <summary>
        /// 把 target-selectable.active 类与临时 ClickEvent 回调应用到每只存活怪物。
        /// 调用方应在调用前清掉旧回调（避免重复绑定）。
        /// </summary>
        private void ApplyTargetMode(Action<int> onMonsterClick)
        {
            // 清掉残留 handlers（防御性，正常路径已 Clear）
            ClearTargetClassesAndHandlers();

            int n = _items.Count;
            for (int i = 0; i < n; i++)
            {
                var root = _items[i].Root;
                if (root == null) continue;
                root.AddToClassList("target-selectable");
                root.AddToClassList("active");

                int captured = i;
                EventCallback<ClickEvent> handler = evt =>
                {
                    onMonsterClick?.Invoke(captured);
                    evt.StopPropagation();
                };
                _targetClickHandlers.Add(handler);
                root.RegisterCallback(handler);
            }
        }

        private void ClearTargetClassesAndHandlers()
        {
            int handlerCount = _targetClickHandlers.Count;
            int itemCount = _items.Count;
            int n = Math.Min(handlerCount, itemCount);
            for (int i = 0; i < n; i++)
            {
                var root = _items[i]?.Root;
                if (root == null) continue;
                root.RemoveFromClassList("target-selectable");
                root.RemoveFromClassList("active");
                if (_targetClickHandlers[i] != null)
                {
                    root.UnregisterCallback(_targetClickHandlers[i]);
                }
            }
            // 清掉 handlerCount 之外的 items 上残留类（极端：handler 数 < 项数）
            for (int i = handlerCount; i < itemCount; i++)
            {
                var root = _items[i]?.Root;
                if (root == null) continue;
                root.RemoveFromClassList("target-selectable");
                root.RemoveFromClassList("active");
            }
            _targetClickHandlers.Clear();
        }

        /// <summary>
        /// 统计当前列表中存活怪物数量；供 SplitAcrossAll 平分伤害文本计算。
        /// </summary>
        private static int CountAlive(IReadOnlyList<MonsterRuntime> monsters)
        {
            int n = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m != null && !m.IsDead) n++;
            }
            return n;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;

            // 兜底：先退出 target 模式（解临时回调）再 Dispose 子项
            if (_targetModeActive) ExitTargetMode();

            _disposed = true;

            if (_context != null && _onMonstersChanged != null)
            {
                _context.Monsters.Changed -= _onMonstersChanged;
            }
            _onMonstersChanged = null;

            foreach (var item in _items)
            {
                item.Root?.RemoveFromHierarchy();
                item.Dispose();
            }
            _items.Clear();

            _context = null;
            _container = null;
            _itemTemplate = null;
        }
    }
}
