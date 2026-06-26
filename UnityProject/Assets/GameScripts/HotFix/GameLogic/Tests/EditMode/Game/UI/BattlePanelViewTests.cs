using System.Reflection;
using GameConfig.card;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI BattlePanelView 协调行为测试。
    /// </summary>
    [TestFixture]
    public sealed class BattlePanelViewTests
    {
        private GameObject _root;
        private BattleUiContext _context;
        private BattlePanelBindings _bindings;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("BattlePanelRoot");
            _context = new BattleUiContext();
            _context.Monsters.Value = new[] { NewMonster("史莱姆", 10) };
            _context.SetHand(new[]
            {
                UguiTestFactory.NewCard(1, "火球", 1, TargetMode.SingleAuto),
                UguiTestFactory.NewCard(2, "斩击", 1, TargetMode.SingleManual, CardReleaseKind.Melee),
            });
            _bindings = CreateBindings();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 构造后子模块全部就绪并完成首次刷新。
        /// </summary>
        [Test]
        public void 构造_装配全部子模块()
        {
            using var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());

            Assert.IsNotNull(view.MonsterListView);
            Assert.IsNotNull(view.HandFanView);
            Assert.IsNotNull(view.TurnControlView);
            Assert.IsNotNull(view.TargetSelector);
            Assert.AreEqual(1, view.MonsterListView.Items.Count);
            Assert.AreEqual(2, view.HandFanView.CardItems.Count);
            Assert.IsFalse(view.TargetSelector.IsActive);
        }

        /// <summary>
        /// 缺失关键绑定不应阻断打开流程。
        /// </summary>
        [Test]
        public void 缺失关键绑定_不抛异常()
        {
            _bindings.HandContainer = null;
            LogAssert.Expect(LogType.Error, "[BattlePanelView] 关键绑定缺失，仍会尝试按可用绑定装配。");

            Assert.DoesNotThrow(() =>
            {
                using var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());
            });
        }

        /// <summary>
        /// AutoTarget 释放到出牌区直接调用 UseCard。
        /// </summary>
        [Test]
        public void AutoTarget_直接UseCard()
        {
            using var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());

            InvokeCardDroppedOnZone(view.HandFanView, 0, false);

            CollectionAssert.AreEqual(new[] { (0, -1) }, _context.UseCardLog);
            Assert.IsFalse(view.TargetSelector.IsActive);
        }

        /// <summary>
        /// SingleManual 释放到出牌区进入目标选择。
        /// </summary>
        [Test]
        public void SingleManual_进入目标选择()
        {
            using var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());

            InvokeCardDroppedOnZone(view.HandFanView, 1, true);

            Assert.IsTrue(view.TargetSelector.IsActive);
            Assert.IsTrue(view.MonsterListView.Items[0].IsTargetSelectable);
            Assert.IsEmpty(_context.UseCardLog);
        }

        /// <summary>
        /// Phase 离开 PlayerTurn 会取消目标选择。
        /// </summary>
        [Test]
        public void Phase离开玩家回合_取消目标选择()
        {
            using var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());
            InvokeCardDroppedOnZone(view.HandFanView, 1, true);

            _context.Phase.Value = BattlePhase.MonsterTurn;

            Assert.IsFalse(view.TargetSelector.IsActive);
            Assert.AreEqual(1, view.HandFanView.GhostReboundRequestCount);
        }

        /// <summary>
        /// Dispose 后解绑事件，后续上下文变化不再操作子模块。
        /// </summary>
        [Test]
        public void Dispose_解绑并清空子模块()
        {
            var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());

            view.Dispose();

            Assert.IsNull(view.TargetSelector);
            Assert.IsNull(view.TurnControlView);
            Assert.IsNull(view.HandFanView);
            Assert.IsNull(view.MonsterListView);
            Assert.DoesNotThrow(() => _context.Phase.Value = BattlePhase.MonsterTurn);
            Assert.DoesNotThrow(() => view.Dispose());
        }

        /// <summary>
        /// 有预览时点击结束回合按钮只关闭预览，不触发同一次结束回合。
        /// </summary>
        [Test]
        public void 预览中点击结束回合_先关闭预览并消费本次点击()
        {
            using var view = new BattlePanelView(_bindings, _context, new HandFanLayoutOptions());
            InvokeCardClicked(view.HandFanView, 0);
            Assert.IsTrue(view.HandFanView.IsPreviewing);

            SimulatePointerDown(_bindings.EndTurnButton.gameObject);
            _bindings.EndTurnButton.onClick.Invoke();

            Assert.IsFalse(view.HandFanView.IsPreviewing);
            Assert.AreEqual(0, _context.EndTurnCallCount);

            _bindings.EndTurnButton.onClick.Invoke();
            Assert.AreEqual(1, _context.EndTurnCallCount);
        }

        private BattlePanelBindings CreateBindings()
        {
            RectTransform monsterContainer = UguiTestFactory.CreateRectObject("MonsterRect", _root.transform).GetComponent<RectTransform>();
            RectTransform handContainer = UguiTestFactory.CreateRectObject("CardSc", _root.transform).GetComponent<RectTransform>();
            RectTransform dropZone = UguiTestFactory.CreateRectObject("DropZone", _root.transform).GetComponent<RectTransform>();
            RectTransform previewLayer = UguiTestFactory.CreateRectObject("PreviewLayer", _root.transform).GetComponent<RectTransform>();
            Button endButton = UguiTestFactory.CreateButton("EndBtn", _root.transform);
            TextMeshProUGUI failToast = UguiTestFactory.CreateText("FailToast", _root.transform);
            CanvasGroup failToastGroup = failToast.gameObject.AddComponent<CanvasGroup>();

            return new BattlePanelBindings
            {
                MonsterContainer = monsterContainer,
                HandContainer = handContainer,
                DropZone = dropZone,
                PreviewLayer = previewLayer,
                EndTurnButton = endButton,
                FailToast = new TextMeshProUGUIProxy { Text = failToast, Group = failToastGroup },
                CancelTargetButton = dropZone.gameObject.AddComponent<Button>(),
                HandCardTemplate = UguiTestFactory.CreateCardTemplate("HandCardTemplate", handContainer),
                MonsterItemTemplate = UguiTestFactory.CreateMonsterTemplate("MonsterItemTemplate", monsterContainer),
                BuffIconTemplate = UguiTestFactory.CreateIconTemplate("BuffIconTemplate", _root.transform),
                IntentIconTemplate = UguiTestFactory.CreateIconTemplate("IntentIconTemplate", _root.transform),
            };
        }

        private static void InvokeCardDroppedOnZone(HandFanView view, int handIdx, bool needsManualTarget)
        {
            object callbacks = typeof(HandFanView).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            callbacks.GetType().GetMethod("CardDroppedOnZone").Invoke(callbacks, new object[] { handIdx, needsManualTarget });
        }

        private static void InvokeCardClicked(HandFanView view, int handIdx)
        {
            object callbacks = typeof(HandFanView).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            callbacks.GetType().GetMethod("CardClicked").Invoke(callbacks, new object[] { handIdx });
        }

        private static void SimulatePointerDown(GameObject target)
        {
            var eventData = new PointerEventData(null)
            {
                pointerPressRaycast = new RaycastResult { gameObject = target }
            };
            foreach (IPointerDownHandler handler in target.GetComponents<IPointerDownHandler>())
            {
                handler.OnPointerDown(eventData);
            }
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
