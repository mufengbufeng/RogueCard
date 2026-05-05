using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Fsm;
using EF.Model;
using EF.Procedure;
using EF.UI;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests.EditMode
{
    [TestFixture]
    public class MainMenuToGameProcedureTests
    {
        [Test]
        public void StartLevelRequested_WhenMainMenuIsActive_ShouldSwitchToGameProcedure()
        {
            using var harness = new ProcedureHarness();

            harness.ProcedureManager.StartProcedure<MainMenuProcedure>();
            harness.EventHub.StartLevelRequestedEvent.Publish(new StartLevelRequestedEvent(
                MainModel.DefaultLevelIdentifier,
                MainModel.DefaultLevelDisplayName));

            Assert.That(harness.ProcedureManager.CurrentProcedure, Is.TypeOf<GameProcedure>());
        }

        [Test]
        public void StartLevelRequested_WhenSwitchingToGameProcedure_ShouldCloseMainViewAndOpenGameView()
        {
            using var harness = new ProcedureHarness();

            harness.ProcedureManager.StartProcedure<MainMenuProcedure>();
            harness.EventHub.StartLevelRequestedEvent.Publish(new StartLevelRequestedEvent(
                MainModel.DefaultLevelIdentifier,
                MainModel.DefaultLevelDisplayName));

            Assert.That(harness.UIManager.OpenedWindows, Does.Contain("MainView:MainView"));
            Assert.That(harness.UIManager.ClosedWindows, Does.Contain("MainView"));
            Assert.That(harness.UIManager.OpenedWindows, Does.Contain("GameView:GameView"));
        }

        private sealed class ProcedureHarness : System.IDisposable
        {
            public ProcedureHarness()
            {
                EventHub = new EventHub();
                UIManager = new RecordingUIManager();
                FsmManager = new FsmManager();
                ProcedureManager = new ProcedureManager();

                SetGameLogicEntryField("_eventHub", EventHub);
                SetGameLogicEntryField("_uiManager", UIManager);

                ProcedureManager.Initialize(
                    FsmManager,
                    new MainMenuProcedure(),
                    new GameProcedure());
            }

            public EventHub EventHub { get; }

            public RecordingUIManager UIManager { get; }

            public FsmManager FsmManager { get; }

            public ProcedureManager ProcedureManager { get; }

            public void Dispose()
            {
                ProcedureManager.Shutdown();
                FsmManager.Shutdown();
                EventHub.Shutdown();
                SetGameLogicEntryField("_eventHub", null);
                SetGameLogicEntryField("_uiManager", null);
            }

            private static void SetGameLogicEntryField(string fieldName, object value)
            {
                var field = typeof(GameLogicEntry).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(null, value);
            }
        }

        private sealed class RecordingUIManager : IUIManager
        {
            public List<string> OpenedWindows { get; } = new();

            public List<string> ClosedWindows { get; } = new();

            public int RegisteredWindowCount { get; private set; }

            public int ActiveWindowCount => OpenedWindows.Count - ClosedWindows.Count;

            public void RegisterWindow(UIWindowDescriptor descriptor)
            {
                RegisteredWindowCount++;
            }

            public bool UnregisterWindow(string windowName)
            {
                return true;
            }

            public bool Contains(string windowName)
            {
                return true;
            }

            public UniTask<UIWindowHandle> OpenWindowAsync(string windowName, object userData = null, CancellationToken cancellationToken = default)
            {
                OpenedWindows.Add(windowName);
                return UniTask.FromResult<UIWindowHandle>(null);
            }

            public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
                string location,
                object userData = null,
                CancellationToken cancellationToken = default)
                where TView : UIView
                where TController : UIController, new()
            {
                OpenedWindows.Add($"{typeof(TView).Name}:{location}");
                return UniTask.FromResult<UIWindowHandle>(null);
            }

            public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
                string location,
                UILayer layer,
                object userData = null,
                CancellationToken cancellationToken = default)
                where TView : UIView
                where TController : UIController, new()
            {
                OpenedWindows.Add($"{typeof(TView).Name}:{location}");
                return UniTask.FromResult<UIWindowHandle>(null);
            }

            public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
                string location,
                UILayer layer,
                bool cacheOnClose,
                bool allowMultiple,
                object userData = null,
                CancellationToken cancellationToken = default)
                where TView : UIView
                where TController : UIController, new()
            {
                OpenedWindows.Add($"{typeof(TView).Name}:{location}");
                return UniTask.FromResult<UIWindowHandle>(null);
            }

            public UniTask CloseWindowAsync(string windowName)
            {
                ClosedWindows.Add(windowName);
                return UniTask.CompletedTask;
            }

            public UniTask CloseAllAsync()
            {
                ClosedWindows.Add("*");
                return UniTask.CompletedTask;
            }

            public bool TryGetController<TController>(string windowName, out TController controller) where TController : UIController
            {
                controller = null;
                return false;
            }

            public bool TryGetView<TView>(string windowName, out TView view) where TView : UIView
            {
                view = null;
                return false;
            }

            public void RegisterLayerRoot(UILayer layer, Transform rootTransform)
            {
            }

            public void SetFallbackRoot(Transform fallbackRoot)
            {
            }

            public void Update(float elapseSeconds, float realElapseSeconds)
            {
            }

            public void Shutdown()
            {
            }
        }
    }
}
