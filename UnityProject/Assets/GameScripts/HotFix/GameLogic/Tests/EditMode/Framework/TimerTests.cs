using System;
using System.Collections.Generic;
using EF.Timer;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class TimerTaskTests
    {
        // TimerTask 是 internal，需要通过 TimerManager 间接测试，
        // 或者利用 InternalsVisibleTo。这里直接测试 TimerManager。

        // 由于 TimerTask 和 TimerTaskCollection 都是 internal，
        // 测试通过 TimerManager 公共 API 覆盖。
    }

    [TestFixture]
    public class TimerManagerTests
    {
        private TimerManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = new TimerManager();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Shutdown();
        }

        #region ScheduleOnce

        [Test]
        public void ScheduleOnce_ShouldReturnValidTimerId()
        {
            int id = _manager.ScheduleOnce(1f, () => { });

            Assert.That(id, Is.GreaterThan(0));
            Assert.That(_manager.Exists(id), Is.True);
        }

        [Test]
        public void ScheduleOnce_WhenTimeArrives_ShouldFireCallback()
        {
            int callCount = 0;
            _manager.ScheduleOnce(5f, () => callCount++);

            // 未到时间
            _manager.Update(3f, 3f);
            Assert.That(callCount, Is.EqualTo(0));

            // 到时间
            _manager.Update(2f, 2f);
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void ScheduleOnce_WhenNotEnoughTime_ShouldNotFire()
        {
            int callCount = 0;
            _manager.ScheduleOnce(10f, () => callCount++);

            _manager.Update(5f, 5f);
            _manager.Update(4f, 4f);

            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void ScheduleOnce_AfterFiring_ShouldBeRemoved()
        {
            int id = _manager.ScheduleOnce(1f, () => { });

            _manager.Update(2f, 2f);

            Assert.That(_manager.Exists(id), Is.False);
        }

        [Test]
        public void ScheduleOnce_WhenNullCallback_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.ScheduleOnce(1f, null));
        }

        [Test]
        public void ScheduleOnce_WithUserData_ShouldFireWithCorrectData()
        {
            string received = null;
            _manager.ScheduleOnce(1f, (string data) => received = data, "hello");

            _manager.Update(2f, 2f);

            Assert.That(received, Is.EqualTo("hello"));
        }

        #endregion

        #region ScheduleLoop

        [Test]
        public void ScheduleLoop_ShouldFireMultipleTimes()
        {
            int callCount = 0;
            // 首次延迟 1s，间隔 2s
            _manager.ScheduleLoop(1f, 2f, () => callCount++);

            // 第 1 次触发：t=1
            _manager.Update(1f, 1f);
            Assert.That(callCount, Is.EqualTo(1));

            // 第 2 次触发：t=3
            _manager.Update(2f, 2f);
            Assert.That(callCount, Is.EqualTo(2));

            // 第 3 次触发：t=5
            _manager.Update(2f, 2f);
            Assert.That(callCount, Is.EqualTo(3));
        }

        [Test]
        public void ScheduleLoop_WithUserData_ShouldFireWithCorrectData()
        {
            var received = new List<int>();
            _manager.ScheduleLoop(1f, 2f, (int data) => received.Add(data), 42);

            _manager.Update(1f, 1f);
            _manager.Update(2f, 2f);

            Assert.That(received.Count, Is.EqualTo(2));
            Assert.That(received[0], Is.EqualTo(42));
            Assert.That(received[1], Is.EqualTo(42));
        }

        #endregion

        #region Cancel

        [Test]
        public void Cancel_WhenTimerExists_ShouldReturnTrue()
        {
            int id = _manager.ScheduleOnce(5f, () => { });

            bool result = _manager.Cancel(id);

            Assert.That(result, Is.True);
            Assert.That(_manager.Exists(id), Is.False);
        }

        [Test]
        public void Cancel_WhenTimerNotExists_ShouldReturnFalse()
        {
            bool result = _manager.Cancel(999);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Cancel_WhenTimerCancelled_ShouldNotFire()
        {
            int callCount = 0;
            int id = _manager.ScheduleOnce(5f, () => callCount++);

            _manager.Cancel(id);
            _manager.Update(10f, 10f);

            Assert.That(callCount, Is.EqualTo(0));
        }

        #endregion

        #region TimerId 唯一性

        [Test]
        public void ScheduleOnce_ShouldGenerateUniqueIds()
        {
            var ids = new HashSet<int>();
            for (int i = 0; i < 100; i++)
            {
                int id = _manager.ScheduleOnce(100f + i, () => { });
                Assert.That(ids.Add(id), Is.True, $"重复 ID: {id}");
            }
        }

        #endregion

        #region Clear

        [Test]
        public void Clear_ShouldRemoveAllTimers()
        {
            _manager.ScheduleOnce(1f, () => { });
            _manager.ScheduleOnce(2f, () => { });
            _manager.ScheduleLoop(1f, 1f, () => { });

            _manager.Clear();

            Assert.That(_manager.TimerCount, Is.EqualTo(0));
        }

        [Test]
        public void Clear_WhenTimersCleared_ShouldNotFire()
        {
            int callCount = 0;
            _manager.ScheduleOnce(1f, () => callCount++);

            _manager.Clear();
            _manager.Update(10f, 10f);

            Assert.That(callCount, Is.EqualTo(0));
        }

        #endregion

        #region Exists

        [Test]
        public void Exists_WhenTimerActive_ShouldReturnTrue()
        {
            int id = _manager.ScheduleOnce(5f, () => { });

            Assert.That(_manager.Exists(id), Is.True);
        }

        [Test]
        public void Exists_WhenTimerNotCreated_ShouldReturnFalse()
        {
            Assert.That(_manager.Exists(12345), Is.False);
        }

        #endregion

        #region TimerCount

        [Test]
        public void TimerCount_ShouldReflectActiveTimers()
        {
            Assert.That(_manager.TimerCount, Is.EqualTo(0));

            _manager.ScheduleOnce(1f, () => { });
            Assert.That(_manager.TimerCount, Is.EqualTo(1));

            _manager.ScheduleOnce(2f, () => { });
            Assert.That(_manager.TimerCount, Is.EqualTo(2));
        }

        #endregion

        #region Shutdown

        [Test]
        public void Shutdown_ShouldClearEverything()
        {
            _manager.ScheduleOnce(1f, () => { });

            _manager.Shutdown();

            Assert.That(_manager.TimerCount, Is.EqualTo(0));
        }

        #endregion

        #region ScheduleOnce 延迟为 0

        [Test]
        public void ScheduleOnce_WhenZeroDelay_ShouldFireOnNextUpdate()
        {
            int callCount = 0;
            _manager.ScheduleOnce(0f, () => callCount++);

            _manager.Update(0f, 0f);

            Assert.That(callCount, Is.EqualTo(1));
        }

        #endregion

        #region 负延迟参数

        [Test]
        public void ScheduleOnce_WhenNegativeDelay_ShouldThrowArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _manager.ScheduleOnce(-1f, () => { }));
        }

        #endregion
    }
}
