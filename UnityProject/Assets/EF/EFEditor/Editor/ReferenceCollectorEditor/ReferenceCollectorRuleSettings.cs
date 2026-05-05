using System;
using System.Collections.Generic;
using UnityEngine;

namespace GT
{
    [Serializable]
    public sealed class ReferenceCollectorRule
    {
        public string suffix;
        public string componentTypeName;
        public bool enabled = true;
        public string displayName;

        /// <summary>
        /// 创建空规则，供 Unity 序列化使用。
        /// </summary>
        public ReferenceCollectorRule()
        {
        }

        /// <summary>
        /// 创建一条命名后缀到组件类型的收集规则。
        /// </summary>
        public ReferenceCollectorRule(string suffix, string componentTypeName, bool enabled, string displayName)
        {
            this.suffix = suffix;
            this.componentTypeName = componentTypeName;
            this.enabled = enabled;
            this.displayName = displayName;
        }
    }

    [CreateAssetMenu(fileName = "ReferenceCollectorRuleSettings", menuName = "EF/ReferenceCollector/Rule Settings")]
    public sealed class ReferenceCollectorRuleSettings : ScriptableObject
    {
        public List<ReferenceCollectorRule> rules = new List<ReferenceCollectorRule>();

        /// <summary>
        /// 将配置重置为项目默认收集规则。
        /// </summary>
        public void ResetToDefaultRules()
        {
            rules = ReferenceCollectorRuleDefaults.CreateDefaultRules();
        }

        /// <summary>
        /// 首次创建配置资产时写入默认规则。
        /// </summary>
        private void Reset()
        {
            ResetToDefaultRules();
        }
    }

    internal static class ReferenceCollectorRuleDefaults
    {
        /// <summary>
        /// 创建内置默认收集规则副本。
        /// </summary>
        internal static List<ReferenceCollectorRule> CreateDefaultRules()
        {
            return new List<ReferenceCollectorRule>
            {
                new ReferenceCollectorRule("Btn", "UnityEngine.UI.Button", true, "Button"),
                new ReferenceCollectorRule("Button", "UnityEngine.UI.Button", true, "Button"),
                new ReferenceCollectorRule("Text", "UnityEngine.UI.Text", true, "Text"),
                new ReferenceCollectorRule("Label", "UnityEngine.UI.Text", true, "Text"),
                new ReferenceCollectorRule("Img", "UnityEngine.UI.Image", true, "Image"),
                new ReferenceCollectorRule("Image", "UnityEngine.UI.Image", true, "Image"),
                new ReferenceCollectorRule("Slider", "UnityEngine.UI.Slider", true, "Slider"),
                new ReferenceCollectorRule("Toggle", "UnityEngine.UI.Toggle", true, "Toggle"),
                new ReferenceCollectorRule("Input", "UnityEngine.UI.InputField", true, "InputField"),
                new ReferenceCollectorRule("InputField", "UnityEngine.UI.InputField", true, "InputField"),
                new ReferenceCollectorRule("Dropdown", "UnityEngine.UI.Dropdown", true, "Dropdown"),
                new ReferenceCollectorRule("Go", "UnityEngine.GameObject", true, "GameObject"),
                new ReferenceCollectorRule("Obj", "UnityEngine.GameObject", true, "GameObject"),
                new ReferenceCollectorRule("SpriteRenderer", "UnityEngine.SpriteRenderer", true, "SpriteRenderer"),
                new ReferenceCollectorRule("TMP", "TMPro.TextMeshProUGUI", true, "TextMeshProUGUI")
            };
        }
    }
}
