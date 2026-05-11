using System;
using System.Collections.Generic;

namespace GameLogic
{
    /// <summary>
    /// MonsterListView 暴露给 TargetSelector 的公开 API 切片。
    /// 引入此接口便于 TargetSelector 在 EditMode 测试中用 fake 替代 MonsterListView。
    /// MonsterListView 直接实现此接口（已有 EnterTargetMode/ExitTargetMode/Items 三个 public 成员）。
    /// </summary>
    public interface IMonsterTargetSurface
    {
        /// <summary>当前已渲染的怪物项列表（仅含存活怪物）。</summary>
        IReadOnlyList<MonsterItemView> Items { get; }

        /// <summary>进入 target 模式：高亮存活怪物 + 注册临时点击回调。</summary>
        void EnterTargetMode(Action<int> onMonsterClick);

        /// <summary>退出 target 模式：清类、解临时回调。</summary>
        void ExitTargetMode();
    }
}
