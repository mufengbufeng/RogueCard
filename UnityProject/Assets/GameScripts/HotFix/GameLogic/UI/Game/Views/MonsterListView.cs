using System;
using System.Collections.Generic;
using EF.Debugger;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// UGUI 怪物列表视图，按 Monsters 列表顺序渲染存活怪物项。
    /// </summary>
    public sealed class MonsterListView : IDisposable
    {
        private readonly RectTransform _container;
        private readonly IMonsterListContext _context;
        private readonly GameObject _itemTemplate;
        private readonly GameObject _buffIconTemplate;
        private readonly GameObject _intentIconTemplate;
        private readonly List<MonsterItemView> _items = new();

        private Action<IReadOnlyList<MonsterRuntime>> _onMonstersChanged;
        private Action<int> _targetClickHandler;
        private bool _disposed;

        /// <summary>
        /// 当前渲染的存活怪物项。
        /// </summary>
        public IReadOnlyList<MonsterItemView> Items => _items;

        /// <summary>
        /// 创建怪物列表视图并完成首帧刷新。
        /// </summary>
        public MonsterListView(
            RectTransform container,
            IMonsterListContext context,
            GameObject itemTemplate,
            GameObject buffIconTemplate,
            GameObject intentIconTemplate)
        {
            _container = container;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _itemTemplate = itemTemplate;
            _buffIconTemplate = buffIconTemplate;
            _intentIconTemplate = intentIconTemplate;

            if (_container == null || _itemTemplate == null)
            {
                Log.Warning("[MonsterListView] 怪物容器或模板为空，跳过渲染。");
            }

            _onMonstersChanged = _ => Refresh();
            _context.Monsters.Changed += _onMonstersChanged;
            Refresh();
        }

        /// <summary>
        /// 进入目标选择模式，所有当前存活怪物项可点击。
        /// </summary>
        public void EnterTargetMode(Action<int> onMonsterClick)
        {
            if (_disposed)
            {
                return;
            }

            _targetClickHandler = onMonsterClick;
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].SetTargetSelectable(true);
                _items[i].Clicked += OnItemClicked;
            }
        }

        /// <summary>
        /// 退出目标选择模式并移除临时点击回调。
        /// </summary>
        public void ExitTargetMode()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Clicked -= OnItemClicked;
                _items[i].SetTargetSelectable(false);
            }

            _targetClickHandler = null;
        }

        /// <summary>
        /// 释放列表和事件订阅。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ExitTargetMode();
            _context.Monsters.Changed -= _onMonstersChanged;
            ClearItems();
        }

        private void Refresh()
        {
            if (_disposed || _container == null || _itemTemplate == null)
            {
                return;
            }

            bool targetMode = _targetClickHandler != null;
            Action<int> targetHandler = _targetClickHandler;
            if (targetMode)
            {
                ExitTargetMode();
            }

            ClearItems();
            IReadOnlyList<MonsterRuntime> monsters = _context.Monsters.Value;
            if (monsters == null)
            {
                return;
            }

            int aliveCount = CountAlive(monsters);
            for (int monsterIndex = 0; monsterIndex < monsters.Count; monsterIndex++)
            {
                MonsterRuntime monster = monsters[monsterIndex];
                if (monster == null || monster.IsDead)
                {
                    continue;
                }

                GameObject itemObject = UguiViewUtil.InstantiateTemplate(_itemTemplate, _container, $"MonsterItem_{monsterIndex}");
                var itemView = new MonsterItemView(itemObject, monster, monsterIndex, aliveCount, _buffIconTemplate, _intentIconTemplate);
                _items.Add(itemView);
            }

            if (targetMode)
            {
                EnterTargetMode(targetHandler);
            }
        }

        private void ClearItems()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                GameObject root = _items[i].Root;
                _items[i].Dispose();
                UguiViewUtil.DestroyObject(root);
            }

            _items.Clear();
        }

        private void OnItemClicked(int monsterIndex)
        {
            _targetClickHandler?.Invoke(monsterIndex);
        }

        private static int CountAlive(IReadOnlyList<MonsterRuntime> monsters)
        {
            int count = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null && !monsters[i].IsDead)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
