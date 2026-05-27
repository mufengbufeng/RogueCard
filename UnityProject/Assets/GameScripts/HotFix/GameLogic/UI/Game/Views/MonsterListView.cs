using System;
using System.Collections.Generic;
using EF.Debugger;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// UGUI 怪物列表视图，按 Monsters 列表顺序渲染存活怪物项。
    /// 怪物在容器 Rect 内随机非重叠摆放，同一波内位置稳定。
    /// </summary>
    public sealed class MonsterListView : IDisposable
    {
        private const float PlacementPadding = 16f;
        private const int RandomPlacementAttempts = 64;
        private static readonly Vector2 FallbackItemSize = new Vector2(200f, 200f);

        private readonly RectTransform _container;
        private readonly IMonsterListContext _context;
        private readonly GameObject _itemTemplate;
        private readonly GameObject _buffIconTemplate;
        private readonly GameObject _intentIconTemplate;
        private readonly List<MonsterItemView> _items = new();
        // 按怪物 InstanceId 缓存其在容器内的 anchoredPosition，保证同一只怪物在多次刷新后位置稳定。
        // 注意：InstanceId == 0 视为未分配（仅测试），不入缓存；PrunePositions 会主动清理。
        private readonly Dictionary<int, Vector2> _positionByInstanceId = new();
        private readonly System.Random _random = new();

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
            _positionByInstanceId.Clear();
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
                _positionByInstanceId.Clear();
                return;
            }

            int aliveCount = CountAlive(monsters);
            Vector2 itemSize = ResolveItemSize();
            Vector2 containerSize = ResolveContainerSize();
            PrunePositions(monsters);

            int placedAliveIndex = 0;
            for (int monsterIndex = 0; monsterIndex < monsters.Count; monsterIndex++)
            {
                MonsterRuntime monster = monsters[monsterIndex];
                if (monster == null || monster.IsDead)
                {
                    continue;
                }

                Vector2 anchoredPosition = EnsurePositionFor(monster, placedAliveIndex, itemSize, containerSize);
                placedAliveIndex++;

                GameObject itemObject = UguiViewUtil.InstantiateTemplate(_itemTemplate, _container, $"MonsterItem_{monsterIndex}");
                ApplyPlacement(itemObject, itemSize, anchoredPosition);

                var itemView = new MonsterItemView(itemObject, monster, monsterIndex, aliveCount, _buffIconTemplate, _intentIconTemplate);
                _items.Add(itemView);
            }

            if (targetMode)
            {
                EnterTargetMode(targetHandler);
            }
        }

        private Vector2 EnsurePositionFor(MonsterRuntime monster, int placedAliveIndex, Vector2 itemSize, Vector2 containerSize)
        {
            int instanceId = monster.InstanceId;
            if (instanceId > 0 && _positionByInstanceId.TryGetValue(instanceId, out Vector2 cached))
            {
                return cached;
            }

            Vector2 chosen = TryRandomPosition(itemSize, containerSize, out bool found);
            if (!found)
            {
                chosen = FallbackGridPosition(placedAliveIndex, itemSize, containerSize);
            }

            // InstanceId == 0 视为未分配，不缓存（仅可能出现在测试代码场景），下一帧会重新随机。
            if (instanceId > 0)
            {
                _positionByInstanceId[instanceId] = chosen;
            }
            return chosen;
        }

        private Vector2 TryRandomPosition(Vector2 itemSize, Vector2 containerSize, out bool found)
        {
            found = false;
            float halfW = itemSize.x * 0.5f;
            float halfH = itemSize.y * 0.5f;
            float xMin = -containerSize.x * 0.5f + halfW + PlacementPadding;
            float xMax = containerSize.x * 0.5f - halfW - PlacementPadding;
            float yMin = -containerSize.y * 0.5f + halfH + PlacementPadding;
            float yMax = containerSize.y * 0.5f - halfH - PlacementPadding;

            if (xMax < xMin || yMax < yMin)
            {
                return Vector2.zero;
            }

            float minSpacingX = itemSize.x + PlacementPadding;
            float minSpacingY = itemSize.y + PlacementPadding;

            for (int attempt = 0; attempt < RandomPlacementAttempts; attempt++)
            {
                float x = Mathf.Lerp(xMin, xMax, (float)_random.NextDouble());
                float y = Mathf.Lerp(yMin, yMax, (float)_random.NextDouble());
                Vector2 candidate = new Vector2(x, y);

                if (!OverlapsExisting(candidate, minSpacingX, minSpacingY))
                {
                    found = true;
                    return candidate;
                }
            }

            return Vector2.zero;
        }

        private bool OverlapsExisting(Vector2 candidate, float minSpacingX, float minSpacingY)
        {
            foreach (Vector2 existing in _positionByInstanceId.Values)
            {
                if (Mathf.Abs(existing.x - candidate.x) < minSpacingX &&
                    Mathf.Abs(existing.y - candidate.y) < minSpacingY)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector2 FallbackGridPosition(int placedAliveIndex, Vector2 itemSize, Vector2 containerSize)
        {
            float spacingX = itemSize.x + PlacementPadding;
            float spacingY = itemSize.y + PlacementPadding;
            int cols = Mathf.Max(1, Mathf.FloorToInt(containerSize.x / spacingX));
            int col = placedAliveIndex % cols;
            int row = placedAliveIndex / cols;

            float startX = -((cols - 1) * spacingX) * 0.5f;
            float startY = containerSize.y * 0.5f - itemSize.y * 0.5f - PlacementPadding;
            return new Vector2(startX + col * spacingX, startY - row * spacingY);
        }

        private void PrunePositions(IReadOnlyList<MonsterRuntime> monsters)
        {
            if (monsters == null || monsters.Count == 0)
            {
                _positionByInstanceId.Clear();
                return;
            }

            // 收集当前快照中存活且 InstanceId > 0 的怪物身份集合，缓存中其余 key 全部移除。
            // 这样换波（新 InstanceId）/死亡（IsDead == true）/离场都会自动回收旧位置。
            HashSet<int> aliveIds = null;
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterRuntime m = monsters[i];
                if (m == null || m.IsDead || m.InstanceId <= 0) continue;
                (aliveIds ??= new HashSet<int>()).Add(m.InstanceId);
            }

            if (aliveIds == null)
            {
                _positionByInstanceId.Clear();
                return;
            }

            List<int> toRemove = null;
            foreach (int key in _positionByInstanceId.Keys)
            {
                if (!aliveIds.Contains(key))
                {
                    (toRemove ??= new List<int>()).Add(key);
                }
            }

            if (toRemove != null)
            {
                foreach (int key in toRemove)
                {
                    _positionByInstanceId.Remove(key);
                }
            }
        }

        private static void ApplyPlacement(GameObject itemObject, Vector2 itemSize, Vector2 anchoredPosition)
        {
            if (itemObject == null)
            {
                return;
            }

            var rt = itemObject.GetComponent<RectTransform>();
            if (rt == null)
            {
                return;
            }

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = itemSize;
            rt.anchoredPosition = anchoredPosition;
        }

        private Vector2 ResolveItemSize()
        {
            if (_itemTemplate == null)
            {
                return FallbackItemSize;
            }

            var rt = _itemTemplate.GetComponent<RectTransform>();
            if (rt == null)
            {
                return FallbackItemSize;
            }

            Vector2 size = rt.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = rt.sizeDelta;
            }
            if (size.x <= 0f || size.y <= 0f)
            {
                return FallbackItemSize;
            }
            return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        }

        private Vector2 ResolveContainerSize()
        {
            Vector2 size = _container.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = _container.sizeDelta;
            }
            return new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
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
