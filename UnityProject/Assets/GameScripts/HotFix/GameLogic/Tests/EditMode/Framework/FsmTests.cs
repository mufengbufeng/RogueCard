using System;
using System.Collections.Generic;
using EF.Fsm;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class FsmTests
    {
        // 测试宿主
        private class TestOwner { }

        // 状态记录器
        private class StateTracker
        {
            public List<string> Log { get; } = new();
        }

        // 测试状态 A
        private class StateA : FsmState<TestOwner>
        {
            private readonly StateTracker _tracker;

            public StateA(StateTracker tracker) { _tracker = tracker; }

            protected internal override void OnEnter(IFsm<TestOwner> fsm)
            {
                _tracker.Log.Add("A:OnEnter");
            }

            protected internal override void OnLeave(IFsm<TestOwner> fsm, bool isShutdown)
            {
                _tracker.Log.Add(isShutdown ? "A:OnLeave:shutdown" : "A:OnLeave");
            }

            protected internal override void OnUpdate(IFsm<TestOwner> fsm, float elapseSeconds, float realElapseSeconds)
            {
                _tracker.Log.Add("A:OnUpdate");
            }

            protected internal override void OnDestroy(IFsm<TestOwner> fsm)
            {
                _tracker.Log.Add("A:OnDestroy");
            }
        }

        // 测试状态 B
        private class StateB : FsmState<TestOwner>
        {
            private readonly StateTracker _tracker;

            public StateB(StateTracker tracker) { _tracker = tracker; }

            protected internal override void OnEnter(IFsm<TestOwner> fsm)
            {
                _tracker.Log.Add("B:OnEnter");
            }

            protected internal override void OnLeave(IFsm<TestOwner> fsm, bool isShutdown)
            {
                _tracker.Log.Add(isShutdown ? "B:OnLeave:shutdown" : "B:OnLeave");
            }

            protected internal override void OnUpdate(IFsm<TestOwner> fsm, float elapseSeconds, float realElapseSeconds)
            {
                _tracker.Log.Add("B:OnUpdate");
            }
        }

        private TestOwner _owner;
        private StateTracker _tracker;
        private FsmManager _manager;

        [SetUp]
        public void SetUp()
        {
            _owner = new TestOwner();
            _tracker = new StateTracker();
            _manager = new FsmManager();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Shutdown();
        }

        #region 状态转换生命周期

        [Test]
        public void Start_ShouldEnterFirstState()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            fsm.Start<StateA>();

            Assert.That(_tracker.Log, Does.Contain("A:OnEnter"));
            Assert.That(fsm.CurrentStateName, Is.EqualTo("StateA"));
        }

        [Test]
        public void ChangeState_ShouldTriggerLeaveAndEnter()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker),
                new StateB(_tracker)
            });

            fsm.Start<StateA>();
            _tracker.Log.Clear();

            fsm.ChangeState<StateB>();

            Assert.That(_tracker.Log, Is.EqualTo(new[]
            {
                "A:OnLeave",
                "B:OnEnter"
            }));
        }

        [Test]
        public void ChangeState_WhenSameState_ShouldDoNothing()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            fsm.Start<StateA>();
            _tracker.Log.Clear();

            fsm.ChangeState<StateA>();

            Assert.That(_tracker.Log, Is.Empty);
        }

        #endregion

        #region Fsm 数据存取

        [Test]
        public void SetData_GetData_ShouldStoreAndRetrieve()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            fsm.SetData("key", 42);

            Assert.That(fsm.GetData<int>("key"), Is.EqualTo(42));
        }

        [Test]
        public void GetData_WhenNotExists_ShouldThrowKeyNotFoundException()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            Assert.Throws<KeyNotFoundException>(() => fsm.GetData<int>("nonexistent"));
        }

        [Test]
        public void TryGetData_WhenExists_ShouldReturnTrue()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            fsm.SetData("key", "value");

            bool found = fsm.TryGetData<string>("key", out var data);

            Assert.That(found, Is.True);
            Assert.That(data, Is.EqualTo("value"));
        }

        [Test]
        public void TryGetData_WhenNotExists_ShouldReturnFalse()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            bool found = fsm.TryGetData<int>("nonexistent", out var data);

            Assert.That(found, Is.False);
        }

        [Test]
        public void RemoveData_ShouldRemove()
        {
            var fsm = _manager.CreateFsm("", _owner, new FsmState<TestOwner>[]
            {
                new StateA(_tracker)
            });

            fsm.SetData("key", 1);
            bool removed = fsm.RemoveData("key");

            Assert.That(removed, Is.True);
            Assert.Throws<KeyNotFoundException>(() => fsm.GetData<int>("key"));
        }

        #endregion

        #region FsmManager CreateFsm / DestroyFsm

        [Test]
        public void CreateFsm_ShouldReturnRunningFsm()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));

            Assert.That(fsm, Is.Not.Null);
            Assert.That(_manager.HasFsm<TestOwner>(), Is.True);
        }

        [Test]
        public void CreateFsm_WhenNullOwner_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _manager.CreateFsm("", null as TestOwner, new StateA(_tracker)));
        }

        [Test]
        public void CreateFsm_WhenEmptyStates_ShouldThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _manager.CreateFsm("", _owner, Array.Empty<FsmState<TestOwner>>()));
        }

        [Test]
        public void DestroyFsm_ShouldRemoveFsm()
        {
            _manager.CreateFsm("", _owner, new StateA(_tracker));

            bool result = _manager.DestroyFsm<TestOwner>();

            Assert.That(result, Is.True);
            Assert.That(_manager.HasFsm<TestOwner>(), Is.False);
        }

        [Test]
        public void DestroyFsm_ShouldCallOnLeave()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));
            fsm.Start<StateA>();
            _tracker.Log.Clear();

            _manager.DestroyFsm<TestOwner>();

            Assert.That(_tracker.Log, Does.Contain("A:OnLeave:shutdown"));
        }

        [Test]
        public void DestroyFsm_WhenNotExists_ShouldReturnFalse()
        {
            bool result = _manager.DestroyFsm<TestOwner>();

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetFsm_AfterCreate_ShouldReturnFsm()
        {
            var created = _manager.CreateFsm("", _owner, new StateA(_tracker));

            var retrieved = _manager.GetFsm<TestOwner>();

            Assert.That(retrieved, Is.SameAs(created));
        }

        #endregion

        #region Update 驱动

        [Test]
        public void Update_ShouldDriveCurrentStateOnUpdate()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));
            fsm.Start<StateA>();
            _tracker.Log.Clear();

            _manager.Update(1f, 1f);

            Assert.That(_tracker.Log, Does.Contain("A:OnUpdate"));
        }

        #endregion

        #region Shutdown 清理

        [Test]
        public void Shutdown_ShouldClearAllFsms()
        {
            _manager.CreateFsm("a", _owner, new StateA(_tracker));

            _manager.Shutdown();

            Assert.That(_manager.Count, Is.EqualTo(0));
        }

        [Test]
        public void Shutdown_WhenEmpty_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Shutdown());
        }

        #endregion

        #region Stop

        [Test]
        public void Stop_ShouldCallOnLeaveWithShutdown()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));
            fsm.Start<StateA>();
            _tracker.Log.Clear();

            fsm.Stop();

            Assert.That(_tracker.Log, Does.Contain("A:OnLeave:shutdown"));
            Assert.That(fsm.IsRunning, Is.False);
        }

        #endregion

        #region HasState / GetState

        [Test]
        public void HasState_WhenRegistered_ShouldReturnTrue()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));

            Assert.That(fsm.HasState<StateA>(), Is.True);
        }

        [Test]
        public void HasState_WhenNotRegistered_ShouldReturnFalse()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));

            Assert.That(fsm.HasState<StateB>(), Is.False);
        }

        [Test]
        public void GetState_WhenRegistered_ShouldReturnState()
        {
            var stateA = new StateA(_tracker);
            var fsm = _manager.CreateFsm("", _owner, stateA);

            var result = fsm.GetState<StateA>();

            Assert.That(result, Is.SameAs(stateA));
        }

        [Test]
        public void GetState_WhenNotRegistered_ShouldThrowKeyNotFoundException()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));

            Assert.Throws<KeyNotFoundException>(() => fsm.GetState<StateB>());
        }

        #endregion

        #region Properties

        [Test]
        public void IsRunning_AfterStart_ShouldReturnTrue()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));

            Assert.That(fsm.IsRunning, Is.False);

            fsm.Start<StateA>();

            Assert.That(fsm.IsRunning, Is.True);
        }

        [Test]
        public void StateCount_ShouldReflectRegisteredStates()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker), new StateB(_tracker));

            Assert.That(fsm.StateCount, Is.EqualTo(2));
        }

        [Test]
        public void CurrentStateTime_ShouldIncreaseWithUpdate()
        {
            var fsm = _manager.CreateFsm("", _owner, new StateA(_tracker));
            fsm.Start<StateA>();

            _manager.Update(2.5f, 2.5f);

            Assert.That(fsm.CurrentStateTime, Is.EqualTo(2.5f).Within(0.001f));
        }

        #endregion
    }
}
