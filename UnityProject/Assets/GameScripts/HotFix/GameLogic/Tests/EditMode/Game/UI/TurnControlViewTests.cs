using NUnit.Framework;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI TurnControlView 行为测试。
    /// </summary>
    [TestFixture]
    public sealed class TurnControlViewTests
    {
        private GameObject _root;
        private FakeTurnContext _context;
        private Button _endButton;
        private TextMeshProUGUI _toastText;
        private CanvasGroup _toastGroup;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("TurnRoot");
            _context = new FakeTurnContext();
            _endButton = UguiTestFactory.CreateButton("EndBtn", _root.transform);
            _toastText = UguiTestFactory.CreateText("FailToast", _root.transform);
            _toastGroup = _toastText.gameObject.AddComponent<CanvasGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// Phase 控制结束回合按钮启用，并转发点击。
        /// </summary>
        [Test]
        public void Phase控制按钮_点击转发EndTurn()
        {
            using var view = new TurnControlView(_endButton, _toastText, _toastGroup, _context);

            _context.Phase.Value = BattlePhase.MonsterTurn;
            Assert.IsFalse(_endButton.interactable);

            _context.Phase.Value = BattlePhase.PlayerTurn;
            Assert.IsTrue(_endButton.interactable);

            _endButton.onClick.Invoke();
            Assert.AreEqual(1, _context.EndTurnCallCount);
        }

        /// <summary>
        /// 出牌失败映射中文 toast。
        /// </summary>
        [Test]
        public void 出牌失败_显示中文Toast()
        {
            using var view = new TurnControlView(_endButton, _toastText, _toastGroup, _context);

            _context.NotifyCardPlayFailed("InsufficientEnergy");

            Assert.AreEqual("能量不足", _toastText.text);
            Assert.AreEqual(1f, _toastGroup.alpha, 0.001f);
        }

        /// <summary>
        /// 出牌失败 toast 按 1.2 秒版本调度隐藏，新提示不会被旧调度清掉。
        /// </summary>
        [Test]
        public void 出牌失败Toast_延迟隐藏且新失败覆盖旧失败()
        {
            long firstVersion = 0;
            long secondVersion = 0;
            int delayMs = 0;
            Action oldHide = null;
            Action newHide = null;

            using var view = new TurnControlView(
                _endButton,
                _toastText,
                _toastGroup,
                _context,
                (target, version, ms) =>
                {
                    delayMs = ms;
                    if (firstVersion == 0)
                    {
                        firstVersion = version;
                        oldHide = () => target.HideToastIfVersionMatches(version);
                    }
                    else
                    {
                        secondVersion = version;
                        newHide = () => target.HideToastIfVersionMatches(version);
                    }
                });

            _context.NotifyCardPlayFailed("InsufficientEnergy");
            _context.NotifyCardPlayFailed("InvalidTarget");

            Assert.AreEqual(1200, delayMs);
            Assert.AreNotEqual(firstVersion, secondVersion);
            Assert.AreEqual("无效目标", _toastText.text);
            Assert.AreEqual(1f, _toastGroup.alpha, 0.001f);

            oldHide?.Invoke();
            Assert.AreEqual(1f, _toastGroup.alpha, 0.001f);

            newHide?.Invoke();
            Assert.AreEqual(0f, _toastGroup.alpha, 0.001f);
        }

        /// <summary>
        /// Dispose 后不再响应上下文。
        /// </summary>
        [Test]
        public void Dispose后_不再响应()
        {
            var view = new TurnControlView(_endButton, _toastText, _toastGroup, _context);
            view.Dispose();

            _context.Phase.Value = BattlePhase.PlayerTurn;
            _context.NotifyCardPlayFailed("InvalidTarget");

            Assert.AreNotEqual("无效目标", _toastText.text);
            Assert.DoesNotThrow(() => view.Dispose());
        }
    }
}
