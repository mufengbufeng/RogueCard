using System;
using System.Reflection;
using EF.Model;
using EF.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests.EditMode
{
    [TestFixture]
    public class MainControllerTests
    {
        [Test]
        public void Enter_WhenGameplayModulesAreMissing_ShouldInitializeMainEntry()
        {
            using var harness = new ControllerHarness();

            Assert.That(harness.Model.IsInteractable, Is.True);
            Assert.That(harness.Model.StatusText, Is.EqualTo(MainModel.ReadyStatusText));
            Assert.That(harness.Model.DefaultLevelId, Is.EqualTo(MainModel.DefaultLevelIdentifier));
            Assert.That(harness.Model.DefaultLevelName, Is.EqualTo(MainModel.DefaultLevelDisplayName));
            Assert.That(harness.Model.DefaultLevelDescription, Is.EqualTo(MainModel.DefaultLevelSummary));
            Assert.That(harness.View._startGameBtn.interactable, Is.True);
            Assert.That(harness.View._statusText.text, Is.EqualTo(MainModel.ReadyStatusText));
            Assert.That(harness.View._levelNameText.text, Is.EqualTo(MainModel.DefaultLevelDisplayName));
            Assert.That(harness.View._levelDescriptionText.text, Is.EqualTo(MainModel.DefaultLevelSummary));
        }

        [Test]
        public void HandleStartGame_WhenGameplayIsNotConnected_ShouldPublishDefaultLevelRequest()
        {
            using var harness = new ControllerHarness();
            StartLevelRequestedEvent? receivedEvent = null;
            harness.EventHub.StartLevelRequestedEvent.Subscribe(e => receivedEvent = e);

            InvokeNonPublic(harness.Controller, "HandleStartGame");

            Assert.That(receivedEvent.HasValue, Is.True);
            Assert.That(receivedEvent.Value.LevelId, Is.EqualTo(MainModel.DefaultLevelIdentifier));
            Assert.That(receivedEvent.Value.LevelName, Is.EqualTo(MainModel.DefaultLevelDisplayName));
            Assert.That(harness.Model.StatusText, Is.EqualTo($"准备进入：{MainModel.DefaultLevelDisplayName}"));
            Assert.That(harness.View._statusText.text, Is.EqualTo($"准备进入：{MainModel.DefaultLevelDisplayName}"));
            Assert.That(harness.View._feedbackText.text, Is.EqualTo($"已发起默认关卡进入请求：{MainModel.DefaultLevelIdentifier}"));
        }

        [Test]
        public void ViewRefresh_WhenModelChanges_ShouldReflectModelState()
        {
            using var harness = new ControllerHarness();

            harness.Model.SetInteractable(false);
            harness.Model.SetStatusText("新的项目入口");
            harness.Model.SetDefaultLevelInfo("level_test", "测试关卡", "测试说明");
            InvokeNonPublic(harness.View, "InternalRefresh", null);

            Assert.That(harness.View._startGameBtn.interactable, Is.False);
            Assert.That(harness.View._statusText.text, Is.EqualTo("新的项目入口"));
            Assert.That(harness.View._levelNameText.text, Is.EqualTo("测试关卡"));
            Assert.That(harness.View._levelDescriptionText.text, Is.EqualTo("测试说明"));
        }

        private static object InvokeNonPublic(object target, string methodName, params object[] args)
        {
            Type currentType = target.GetType();
            while (currentType != null)
            {
                var method = currentType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    return method.Invoke(target, args);
                }

                currentType = currentType.BaseType;
            }

            Assert.Fail($"未找到方法: {methodName}");
            return null;
        }

        private sealed class ControllerHarness : IDisposable
        {
            private readonly GameObject _gameObject;

            public ControllerHarness()
            {
                EventHub = new EventHub();
                SetGameLogicEntryEvent(EventHub);

                _gameObject = new GameObject("MainControllerTests_MainView", typeof(RectTransform));
                View = _gameObject.AddComponent<TestMainView>();
                View._startGameBtn = CreateButton(_gameObject.transform);
                View._statusText = CreateText(_gameObject.transform, "StatusText");
                View._levelNameText = CreateText(_gameObject.transform, "LevelNameText");
                View._levelDescriptionText = CreateText(_gameObject.transform, "LevelDescriptionText");
                View._feedbackText = CreateText(_gameObject.transform, "FeedbackText");

                var modelManager = new ModelManager();
                Model = modelManager.Register<MainModel>();

                UIRuntimeContext context = CreateRuntimeContext(modelManager, _gameObject.transform);
                InvokeNonPublic(View, "InternalInitialize", context);

                Controller = new MainController();
                InvokeNonPublic(Controller, "InternalInitialize", View, context);
                InvokeNonPublic(Controller, "InternalEnter", null);
            }

            public MainController Controller { get; }

            public TestMainView View { get; }

            public MainModel Model { get; }

            public EventHub EventHub { get; }

            public void Dispose()
            {
                try
                {
                    InvokeNonPublic(Controller, "InternalExit");
                    InvokeNonPublic(Controller, "InternalRelease");
                    Controller.Dispose();
                }
                finally
                {
                    EventHub.Shutdown();
                    SetGameLogicEntryEvent(null);
                    InvokeNonPublic(View, "InternalRelease");
                    UnityEngine.Object.DestroyImmediate(_gameObject);
                }
            }

            private static Button CreateButton(Transform parent)
            {
                var buttonObject = new GameObject("StartGameBtn", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(parent, false);
                return buttonObject.GetComponent<Button>();
            }

            private static TextMeshProUGUI CreateText(Transform parent, string name)
            {
                var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(parent, false);
                return textObject.GetComponent<TextMeshProUGUI>();
            }

            private static UIRuntimeContext CreateRuntimeContext(ModelManager modelManager, Transform layerRoot)
            {
                var ctor = typeof(UIRuntimeContext).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(IUIManager),
                        typeof(ModelManager),
                        typeof(UIWindowDescriptor),
                        typeof(Transform)
                    },
                    modifiers: null);

                Assert.That(ctor, Is.Not.Null);
                return (UIRuntimeContext)ctor.Invoke(new object[] { null, modelManager, null, layerRoot });
            }

            private static void SetGameLogicEntryEvent(EventHub eventHub)
            {
                var field = typeof(GameLogicEntry).GetField("_eventHub", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(null, eventHub);
            }
        }

        private sealed class TestMainView : MainView
        {
        }
    }
}
