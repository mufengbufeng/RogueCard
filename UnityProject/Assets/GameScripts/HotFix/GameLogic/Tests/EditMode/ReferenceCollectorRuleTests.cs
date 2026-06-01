using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        /// <summary>
        /// GameView 规范命名后缀必须能自动收集并推断正确类型。
        /// </summary>
        [Test]
        public void AutoCollect_CollectsGameViewNamingRules()
        {
            var collector = CreateCollector();
            var battlePanel = CreateChild("BattlePanel");
            var rewardPanel = CreateChild("RewardPanel");
            var playerHpFill = CreateChild("PlayerHpFill");
            var playerBuffBar = CreateChild("PlayerBuffBar");
            var dropZone = CreateChild("DropZone");
            var previewLayer = CreateChild("PreviewLayer");
            var cardSc = CreateChild("CardSc");
            var handCardTemplate = CreateChild("HandCardTemplate");
            var infoText = CreateChild("InfoText");
            playerHpFill.AddComponent<Image>();
            playerBuffBar.AddComponent<RectTransform>();
            dropZone.AddComponent<RectTransform>();
            previewLayer.AddComponent<RectTransform>();
            cardSc.AddComponent<RectTransform>();
            infoText.AddComponent<TextMeshProUGUI>();

            var count = collector.AutoCollectByNamingRules();

            Assert.AreEqual(9, count);
            Assert.AreSame(battlePanel, collector.data.Single(item => item.key == "BattlePanel").gameObject);
            Assert.AreSame(rewardPanel, collector.data.Single(item => item.key == "RewardPanel").gameObject);
            Assert.AreSame(playerHpFill.GetComponent<Image>(), collector.data.Single(item => item.key == "PlayerHpFill").gameObject);
            Assert.AreSame(playerBuffBar.GetComponent<RectTransform>(), collector.data.Single(item => item.key == "PlayerBuffBar").gameObject);
            Assert.AreSame(dropZone.GetComponent<RectTransform>(), collector.data.Single(item => item.key == "DropZone").gameObject);
            Assert.AreSame(previewLayer.GetComponent<RectTransform>(), collector.data.Single(item => item.key == "PreviewLayer").gameObject);
            Assert.AreSame(cardSc.GetComponent<RectTransform>(), collector.data.Single(item => item.key == "CardSc").gameObject);
            Assert.AreSame(handCardTemplate, collector.data.Single(item => item.key == "HandCardTemplate").gameObject);
            Assert.AreSame(infoText.GetComponent<TextMeshProUGUI>(), collector.data.Single(item => item.key == "InfoText").gameObject);
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
