using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests
{
    public sealed class ReferenceCollectorRuleTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 默认 Button 规则可收集对应组件。
        /// </summary>
        [Test]
        public void AutoCollect_UsesDefaultButtonRule()
        {
            var collector = CreateCollector();
            var child = CreateChild("StartBtn");
            var buttonType = ResolveType("UnityEngine.UI.Button");
            Assert.NotNull(buttonType, "未找到 UnityEngine.UI.Button 类型");
            var button = child.AddComponent(buttonType);

            var count = collector.AutoCollectByNamingRules();

            Assert.AreEqual(1, count);
            Assert.AreSame(button, collector.data.Single(item => item.key == "StartBtn").gameObject);
        }

        /// <summary>
        /// TMP 规则存在时可解析并收集 TextMeshProUGUI 组件。
        /// </summary>
        [Test]
        public void AutoCollect_UsesTmpRuleWhenTypeExists()
        {
            var tmpType = ResolveType("TMPro.TextMeshProUGUI");
            if (tmpType == null)
            {
                Assert.Ignore("当前工程未加载 TMPro.TextMeshProUGUI 类型");
            }

            var collector = CreateCollector();
            var child = CreateChild("TitleTMP");
            var tmp = child.AddComponent(tmpType);

            var count = collector.AutoCollectByNamingRules();

            Assert.AreEqual(1, count);
            Assert.AreSame(tmp, collector.data.Single(item => item.key == "TitleTMP").gameObject);
        }

        /// <summary>
        /// 已存在 key 时不会新增重复引用。
        /// </summary>
        [Test]
        public void AutoCollect_DoesNotDuplicateExistingKey()
        {
            var collector = CreateCollector();
            var child = CreateChild("CloseGo");
            collector.Add("CloseGo", child);

            var count = collector.AutoCollectByNamingRules();

            Assert.AreEqual(0, count);
            Assert.AreEqual(1, collector.data.Count(item => item.key == "CloseGo"));
            Assert.AreSame(child, collector.data.Single(item => item.key == "CloseGo").gameObject);
        }

        /// <summary>
        /// 匹配规则但缺少目标组件时跳过该对象。
        /// </summary>
        [Test]
        public void AutoCollect_SkipsMissingComponent()
        {
            var collector = CreateCollector();
            CreateChild("MissingBtn");

            var count = collector.AutoCollectByNamingRules();

            Assert.AreEqual(0, count);
            Assert.IsFalse(collector.data.Any(item => item.key == "MissingBtn"));
        }

        /// <summary>
        /// GameObject 规则会收集对象自身。
        /// </summary>
        [Test]
        public void AutoCollect_CollectsGameObjectRule()
        {
            var collector = CreateCollector();
            var child = CreateChild("PanelGo");

            var count = collector.AutoCollectByNamingRules();

            Assert.AreEqual(1, count);
            Assert.AreSame(child, collector.data.Single(item => item.key == "PanelGo").gameObject);
        }

        private ReferenceCollector CreateCollector()
        {
            root = new GameObject("ReferenceCollectorRoot");
            return root.AddComponent<ReferenceCollector>();
        }

        private GameObject CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private static Type ResolveType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }
    }
}
