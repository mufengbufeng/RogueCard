using EF.Model;
using EF.UI;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// 主菜单 UGUI Controller 命令流测试。
    /// </summary>
    [TestFixture]
    public sealed class MainControllerCommandFlowTests
    {
        private GameObject _viewObject;
        private MainView _view;
        private MainController _controller;
        private EventHub _eventHub;

        /// <summary>
        /// 构造 MainView / MainController，并替换 GameLogicEntry 事件总线。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _viewObject = new GameObject("MainViewTests", typeof(RectTransform), typeof(MainView));
            _view = _viewObject.GetComponent<MainView>();
            _controller = new MainController();
            _eventHub = new EventHub();

            GameLogicEntry.SetEventHubForTests(_eventHub);

            var descriptor = UIWindowDescriptor.Create<MainView, MainController>(
                "MainView",
                "MainView",
                UILayer.Normal,
                cacheOnClose: true,
                allowMultiple: false);
            var context = new UIRuntimeContext(null, new ModelManager(), descriptor, _viewObject.transform);

            _view.InternalInitialize(context);
            _controller.InternalInitialize(_view, context);
            _controller.InternalEnter(null);
        }

        /// <summary>
        /// 清理测试对象和静态入口状态。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _controller?.InternalExit();
            _controller?.InternalRelease();
            _controller?.Dispose();
            Object.DestroyImmediate(_viewObject);
            GameLogicEntry.SetEventHubForTests(null);
        }

        /// <summary>
        /// 点击开始按钮时必须发布开始关卡事件。
        /// </summary>
        [Test]
        public void ClickStart_发布StartLevelRequestedEvent()
        {
            StartLevelRequestedEvent? received = null;
            _eventHub.StartLevelRequestedEvent.Subscribe(evt => received = evt);

            _view.NotifyStartGameRequestedForTests();

            Assert.IsTrue(received.HasValue, "应发布 StartLevelRequestedEvent");
            Assert.AreEqual(MainModel.FallbackLevelId, received.Value.LevelId);
        }
    }
}
