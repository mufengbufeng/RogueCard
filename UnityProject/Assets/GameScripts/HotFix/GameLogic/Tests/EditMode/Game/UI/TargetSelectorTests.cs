using System.Collections.Generic;
using GameConfig.card;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI TargetSelector 行为测试。
    /// </summary>
    [TestFixture]
    public sealed class TargetSelectorTests
    {
        private GameObject _root;
        private BattleUiContext _context;
        private MonsterListView _monsterListView;
        private HandFanView _handFanView;
        private Button _cancelButton;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("TargetSelectorRoot");
            _context = new BattleUiContext();
            _context.Monsters.Value = new[]
            {
                NewMonster("史莱姆", 10),
                NewMonster("蝙蝠", 8),
            };
            _context.SetHand(new[] { UguiTestFactory.NewCard(1, "斩击", 1, TargetMode.SingleManual, CardReleaseKind.Melee) });

            RectTransform monsterContainer = UguiTestFactory.CreateRectObject("MonsterRect", _root.transform).GetComponent<RectTransform>();
            GameObject monsterTemplate = UguiTestFactory.CreateMonsterTemplate("MonsterItemTemplate", monsterContainer);
            GameObject buffTemplate = UguiTestFactory.CreateIconTemplate("BuffIconTemplate", _root.transform);
            GameObject intentTemplate = UguiTestFactory.CreateIconTemplate("IntentIconTemplate", _root.transform);
            _monsterListView = new MonsterListView(monsterContainer, _context, monsterTemplate, buffTemplate, intentTemplate);

            RectTransform handContainer = UguiTestFactory.CreateRectObject("CardSc", _root.transform).GetComponent<RectTransform>();
            RectTransform dropZone = UguiTestFactory.CreateRectObject("DropZone", _root.transform).GetComponent<RectTransform>();
            RectTransform previewLayer = UguiTestFactory.CreateRectObject("PreviewLayer", _root.transform).GetComponent<RectTransform>();
            GameObject cardTemplate = UguiTestFactory.CreateCardTemplate("HandCardTemplate", handContainer);
            _handFanView = new HandFanView(handContainer, dropZone, previewLayer, _context, cardTemplate, new HandFanLayoutOptions());
            _cancelButton = UguiTestFactory.CreateButton("Cancel", _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _handFanView.Dispose();
            _monsterListView.Dispose();
            Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 进入目标选择会高亮存活怪物。
        /// </summary>
        [Test]
        public void Enter_高亮怪物()
        {
            using var selector = new TargetSelector(_monsterListView, _handFanView, _context, _cancelButton);

            selector.Enter(0);

            Assert.IsTrue(selector.IsActive);
            Assert.IsTrue(_monsterListView.Items[0].IsTargetSelectable);
            Assert.IsTrue(_monsterListView.Items[1].IsTargetSelectable);
        }

        /// <summary>
        /// 点击怪物会调用 UseCardOnMonster 并清理 ghost。
        /// </summary>
        [Test]
        public void 点击怪物_确认目标并清理Ghost()
        {
            using var selector = new TargetSelector(_monsterListView, _handFanView, _context, _cancelButton);
            selector.Enter(0);

            _monsterListView.Items[1].Root.GetComponent<Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { (0, 1) }, _context.UseCardOnMonsterLog);
            Assert.AreEqual(1, _handFanView.GhostCleanupRequestCount);
            Assert.IsFalse(selector.IsActive);
        }

        /// <summary>
        /// 取消目标选择会请求 ghost 回弹。
        /// </summary>
        [Test]
        public void Cancel_请求Ghost回弹()
        {
            using var selector = new TargetSelector(_monsterListView, _handFanView, _context, _cancelButton);
            selector.Enter(0);

            _cancelButton.onClick.Invoke();

            Assert.AreEqual(1, _handFanView.GhostReboundRequestCount);
            Assert.IsFalse(selector.IsActive);
        }

        /// <summary>
        /// Phase 离开 PlayerTurn 会强制取消。
        /// </summary>
        [Test]
        public void Phase离开玩家回合_取消目标选择()
        {
            using var selector = new TargetSelector(_monsterListView, _handFanView, _context, _cancelButton);
            selector.Enter(0);

            _context.Phase.Value = BattlePhase.MonsterTurn;

            Assert.AreEqual(1, _handFanView.GhostReboundRequestCount);
            Assert.IsFalse(selector.IsActive);
        }

        private static MonsterRuntime NewMonster(string name, int hp)
        {
            return new MonsterRuntime
            {
                Config = UguiTestFactory.NewMonsterConfig(1, name),
                Hp = hp,
                MaxHp = hp,
            };
        }
    }
}
