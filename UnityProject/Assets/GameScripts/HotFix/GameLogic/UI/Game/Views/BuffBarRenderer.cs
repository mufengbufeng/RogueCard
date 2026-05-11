using System.Collections.Generic;
using GameConfig.card;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// Buff 状态条共享渲染器。规则保持与原 GameView.RenderBuffBar 一致：
    /// 每条 Buff 一个 .buff-icon Label，DamageDot 类型额外加 .buff-icon-dot 类，文本格式 `{Value}×{RemainingTurns}`。
    /// 玩家与怪物 buff bar 共用，确保渲染规则一致。
    /// </summary>
    public static class BuffBarRenderer
    {
        /// <summary>
        /// 把 buffs 列表渲染到 buffBar 容器中。容器先被清空；空列表只清空不再追加。
        /// </summary>
        /// <param name="buffBar">buff 状态条容器（任意 VisualElement，nullable 安全）。</param>
        /// <param name="buffs">Buff 列表，可为 null 或空。</param>
        public static void Render(VisualElement buffBar, IList<BuffRuntime> buffs)
        {
            if (buffBar == null) return;
            buffBar.Clear();
            if (buffs == null || buffs.Count == 0) return;

            foreach (var buff in buffs)
            {
                if (buff == null) continue;

                var icon = new Label
                {
                    pickingMode = PickingMode.Ignore,
                    text = $"{buff.Value}×{buff.RemainingTurns}"
                };
                icon.AddToClassList("buff-icon");

                if (buff.Kind == EffectKind.DamageDot)
                {
                    icon.AddToClassList("buff-icon-dot");
                }

                buffBar.Add(icon);
            }
        }
    }
}
