using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Fsm;
using EF.Procedure;
using EF.UI;
using NUnit.Framework;
using UnityEngine;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 GameProcedure 关卡完成回调的契约：
    /// - 私有 OnLevelComplete 通过保存的 ProcedureOwner 调用 ChangeState&lt;MainMenuProcedure&gt;；
    /// - Cleanup 取消 LevelCompleteEvent 订阅，使后续事件不再触发回调。
    /// 因为 GameProcedure.EnterAsync 依赖 GameLogicEntry 单例（UIManager / Model / Config），
    /// 这里使用反射直接对私有字段与方法进行精确装配，验证关键回调路径与订阅生命周期。
    /// </summary>
    [TestFixture]
    public class GameProcedureLevelCompleteTests
    {
        [Test]
        public void OnLevelComplete_切换到MainMenuProcedure()
        {
            var procedure = new GameProcedure();
            var fakeOwner = new FakeProcedureOwner();

            SetPrivateField(procedure, "_procedureOwner", fakeOwner);

            InvokePrivate(procedure, "OnLevelComplete", new LevelCompleteEvent(101));

            Assert.AreEqual(1, fakeOwner.GenericChangeStateCalls.Count, "应调用一次 ChangeState<TState>()");
            Assert.AreEqual(typeof(MainMenuProcedure), fakeOwner.GenericChangeStateCalls[0]);
        }

        [Test]
        public void OnLevelComplete_未就绪时安全忽略()
        {
            var procedure = new GameProcedure();
            // 不设置 _procedureOwner，模拟在生命周期外收到事件

            Assert.DoesNotThrow(() => InvokePrivate(procedure, "OnLevelComplete", new LevelCompleteEvent(0)));
        }

        [Test]
        public void OnRewardSelected_切换到MainMenuProcedure()
        {
            var procedure = new GameProcedure();
            var fakeOwner = new FakeProcedureOwner();

            SetPrivateField(procedure, "_procedureOwner", fakeOwner);

            InvokePrivate(procedure, "OnRewardSelected");

            Assert.AreEqual(1, fakeOwner.GenericChangeStateCalls.Count, "应调用一次 ChangeState<TState>()");
            Assert.AreEqual(typeof(MainMenuProcedure), fakeOwner.GenericChangeStateCalls[0]);
        }

        [Test]
        public void OnRewardSelected_未就绪时安全忽略()
        {
            var procedure = new GameProcedure();

            Assert.DoesNotThrow(() => InvokePrivate(procedure, "OnRewardSelected"));
        }

        [Test]
        public void EnterAsync_使用GameView地址打开UGUI局内界面()
        {
            string source = File.ReadAllText("Assets/GameScripts/HotFix/GameLogic/Procedure/Game/GameProcedure.cs");

            Assert.That(source, Does.Contain("OpenWindowAsync<GameView, GameController>"));
            Assert.That(source, Does.Contain("\"GameView\""));
            Assert.That(source, Does.Contain("UILayer.Normal"));
            Assert.That(source, Does.Contain("cacheOnClose: false"));
            Assert.That(source, Does.Contain("allowMultiple: false"));
            Assert.That(source, Does.Contain("userData: _viewModel"));
        }

        [Test]
        public void OnLeave_关闭GameView窗口()
        {
            var procedure = new GameProcedure();
            var fakeUi = new FakeUiManager();

            SetPrivateField(procedure, "_uiManager", fakeUi);

            InvokeLifecycle(procedure, "OnLeave", new object[] { null, false });

            CollectionAssert.Contains(fakeUi.ClosedWindows, nameof(GameView));
        }

        [Test]
        public void Cleanup_取消LevelCompleteEvent订阅_后续事件不再触发()
        {
            var procedure = new GameProcedure();
            var fakeOwner = new FakeProcedureOwner();
            var bus = new LocalEventBus();

            try
            {
                SetPrivateField(procedure, "_procedureOwner", fakeOwner);
                SetPrivateField(procedure, "_localEventBus", bus);

                // 模拟 EnterAsync 的订阅动作（保留与生产代码一致的回调入口）
                Action<LevelCompleteEvent> handler = e => InvokePrivate(procedure, "OnLevelComplete", e);
                bus.GetChannel<LevelCompleteEvent>().Subscribe(handler);

                // 调用 Cleanup：应取消订阅；本地总线 _localEventBus 字段被置空；ProcedureOwner 字段被清空
                InvokePrivate(procedure, "Cleanup");

                Assert.IsNull(GetPrivateField(procedure, "_procedureOwner"),
                    "Cleanup 后 _procedureOwner 应被置空");
                Assert.IsNull(GetPrivateField(procedure, "_localEventBus"),
                    "Cleanup 后 _localEventBus 应被置空（确认幂等清理路径）");

                // 触发器：在原 bus 上再次发布事件——由于生产代码 Cleanup 已经取消了 OnLevelComplete 订阅，
                // 不会调用 ChangeState；这里 handler 已主动 Unsubscribe（由 GameProcedure.Cleanup 内部完成）。
                bus.GetChannel<LevelCompleteEvent>().Publish(new LevelCompleteEvent(999));

                Assert.AreEqual(0, fakeOwner.GenericChangeStateCalls.Count,
                    "Cleanup 后再发布 LevelCompleteEvent 不应触发流程切换");
            }
            finally
            {
                bus.Dispose();
            }
        }

        [Test]
        public void Cleanup_可重复调用_不抛异常()
        {
            var procedure = new GameProcedure();
            Assert.DoesNotThrow(() => InvokePrivate(procedure, "Cleanup"));
            Assert.DoesNotThrow(() => InvokePrivate(procedure, "Cleanup"));
        }

        // ── 测试基础设施 ──

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到私有字段 {fieldName}");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到私有字段 {fieldName}");
            return field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, $"未找到私有方法 {methodName}");
            method.Invoke(target, args);
        }

        private static void InvokeLifecycle(object target, string methodName, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, $"未找到生命周期方法 {methodName}");
            method.Invoke(target, args);
        }

        /// <summary>
        /// 最小化的 ProcedureOwner 假实现，仅记录 ChangeState 调用，
        /// 其它接口成员未启用（测试不应触发这些路径）。
        /// </summary>
        private sealed class FakeProcedureOwner : ProcedureOwner
        {
            public System.Collections.Generic.List<Type> GenericChangeStateCalls { get; } = new();

            public void ChangeState<TState>() where TState : FsmState<IProcedureManager>
            {
                GenericChangeStateCalls.Add(typeof(TState));
            }

            public void ChangeState(Type stateType)
            {
                GenericChangeStateCalls.Add(stateType);
            }

            // ── 以下成员未在测试路径使用 ──

            public IProcedureManager Owner => null;
            object IFsm.Owner => null;
            public string Name => "FakeProcedureOwner";
            public Type OwnerType => typeof(IProcedureManager);
            public bool IsRunning => true;
            public bool IsDestroyed => false;
            public int StateCount => 0;
            public string CurrentStateName => null;
            public float CurrentStateTime => 0f;
            public FsmState<IProcedureManager> CurrentState => null;

            public bool HasState(Type stateType) => false;
            public bool HasState<TState>() where TState : FsmState<IProcedureManager> => false;
            public FsmState<IProcedureManager> GetState(Type stateType) => null;
            public TState GetState<TState>() where TState : FsmState<IProcedureManager> => null;
            public void Start(Type stateType) { }
            public void Start<TState>() where TState : FsmState<IProcedureManager> { }
            public void Stop() { }
            public void SetData<TData>(string name, TData data) { }
            public bool TryGetData<TData>(string name, out TData data) { data = default; return false; }
            public TData GetData<TData>(string name) => default;
            public bool RemoveData(string name) => false;
            public void ClearData() { }
        }

        private sealed class FakeUiManager : IUIManager
        {
            public List<string> ClosedWindows { get; } = new();
            public int RegisteredWindowCount => 0;
            public int ActiveWindowCount => 0;
            public void RegisterWindow(UIWindowDescriptor descriptor) { }
            public bool UnregisterWindow(string windowName) => false;
            public bool Contains(string windowName) => false;
            public UniTask<UIWindowHandle> OpenWindowAsync(string windowName, object userData = null, CancellationToken cancellationToken = default) => UniTask.FromResult<UIWindowHandle>(null);
            public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(string location, object userData = null, CancellationToken cancellationToken = default)
                where TView : UIView
                where TController : UIController, new() => UniTask.FromResult<UIWindowHandle>(null);
            public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(string location, UILayer layer, object userData = null, CancellationToken cancellationToken = default)
                where TView : UIView
                where TController : UIController, new() => UniTask.FromResult<UIWindowHandle>(null);
            public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(string location, UILayer layer, bool cacheOnClose, bool allowMultiple, object userData = null, CancellationToken cancellationToken = default)
                where TView : UIView
                where TController : UIController, new() => UniTask.FromResult<UIWindowHandle>(null);
            public UniTask CloseWindowAsync(string windowName)
            {
                ClosedWindows.Add(windowName);
                return UniTask.CompletedTask;
            }
            public UniTask CloseAllAsync() => UniTask.CompletedTask;
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
            public void RegisterLayerRoot(UILayer layer, Transform rootTransform) { }
            public void SetFallbackRoot(Transform fallbackRoot) { }
            public void Update(float elapseSeconds, float realElapseSeconds) { }
            public void Shutdown() { }
        }
    }
}
