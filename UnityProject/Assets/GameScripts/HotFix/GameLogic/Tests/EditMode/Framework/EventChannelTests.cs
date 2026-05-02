using System;
using System.Collections.Generic;
using EF.Event;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class EventChannelTests
    {
        // 测试用事件类型
        private struct TestEvent
        {
            public int Value;
        }

        private EventChannel<TestEvent> _channel;

        [SetUp]
        public void SetUp()
        {
            _channel = new EventChannel<TestEvent>();
        }

        #region Subscribe

        [Test]
        public void Subscribe_WhenValidHandler_ShouldIncreaseHandlerCount()
        {
            Action<TestEvent> handler = e => { };

            _channel.Subscribe(handler);

            Assert.That(_channel.HandlerCount, Is.EqualTo(1));
            Assert.That(_channel.GetHandlerNames(), Has.Length.EqualTo(1));
        }

        [Test]
        public void Subscribe_WhenNullHandler_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _channel.Subscribe(null));
        }

        [Test]
        public void Subscribe_WhenDuplicateHandler_ShouldNotDuplicate()
        {
            Action<TestEvent> handler = e => { };

            _channel.Subscribe(handler);
            _channel.Subscribe(handler);

            Assert.That(_channel.HandlerCount, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_WhenMultipleDifferentHandlers_ShouldAllRegistered()
        {
            Action<TestEvent> handler1 = e => { };
            Action<TestEvent> handler2 = e => { };
            Action<TestEvent> handler3 = e => { };

            _channel.Subscribe(handler1);
            _channel.Subscribe(handler2);
            _channel.Subscribe(handler3);

            Assert.That(_channel.HandlerCount, Is.EqualTo(3));
        }

        #endregion

        #region Unsubscribe

        [Test]
        public void Unsubscribe_WhenRegisteredHandler_ShouldDecreaseHandlerCount()
        {
            var received = new List<TestEvent>();
            Action<TestEvent> handler = e => received.Add(e);
            _channel.Subscribe(handler);

            _channel.Unsubscribe(handler);

            Assert.That(_channel.HandlerCount, Is.EqualTo(0));
            _channel.Publish(new TestEvent { Value = 1 });
            Assert.That(received.Count, Is.EqualTo(0));
        }

        [Test]
        public void Unsubscribe_WhenUnknownHandler_ShouldNotThrow()
        {
            Action<TestEvent> handler = e => { };

            Assert.DoesNotThrow(() => _channel.Unsubscribe(handler));
            Assert.That(_channel.HandlerCount, Is.EqualTo(0));
        }

        [Test]
        public void Unsubscribe_WhenNullHandler_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _channel.Unsubscribe(null));
        }

        #endregion

        #region Publish

        [Test]
        public void Publish_WhenSingleHandler_ShouldInvokeOnce()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            _channel.Publish(new TestEvent { Value = 42 });

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].Value, Is.EqualTo(42));
        }

        [Test]
        public void Publish_WhenMultipleHandlers_ShouldInvokeInOrder()
        {
            var order = new List<int>();
            _channel.Subscribe(e => order.Add(1));
            _channel.Subscribe(e => order.Add(2));
            _channel.Subscribe(e => order.Add(3));

            _channel.Publish(new TestEvent());

            Assert.That(order, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Publish_WhenNoHandlers_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => _channel.Publish(new TestEvent()));
        }

        [Test]
        public void Publish_WhenHandlerSubscribesDuringDispatch_ShouldNotFireInCurrentRound()
        {
            var calls = new List<int>();
            Action<TestEvent> handlerD = e => calls.Add(4);

            _channel.Subscribe(e =>
            {
                calls.Add(1);
                _channel.Subscribe(handlerD);
            });
            _channel.Subscribe(e => calls.Add(2));

            _channel.Publish(new TestEvent());

            // handlerD 不应在本轮触发
            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));

            // 下一轮应触发 handlerD
            _channel.Publish(new TestEvent());
            Assert.That(calls, Is.EqualTo(new[] { 1, 2, 1, 2, 4 }));
        }

        [Test]
        public void Publish_WhenHandlerUnsubscribesDuringDispatch_ShouldNotCrash()
        {
            var calls = new List<int>();
            Action<TestEvent> handlerB = e => calls.Add(2);

            _channel.Subscribe(e =>
            {
                calls.Add(1);
                _channel.Unsubscribe(handlerB);
            });
            _channel.Subscribe(handlerB);
            _channel.Subscribe(e => calls.Add(3));

            Assert.DoesNotThrow(() => _channel.Publish(new TestEvent()));
            // 三个 handler 都注册了，但 A 可能在 B 前面被调用，此时 B 被移除
            // 具体行为取决于实现，但不应崩溃
            Assert.That(calls.Count, Is.GreaterThanOrEqualTo(1));
        }

        #endregion

        #region Enqueue / Flush

        [Test]
        public void Enqueue_ShouldIncreasePendingCount()
        {
            _channel.Enqueue(new TestEvent { Value = 1 });

            Assert.That(_channel.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void Enqueue_ShouldNotInvokeHandlers()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            _channel.Enqueue(new TestEvent { Value = 1 });

            Assert.That(received.Count, Is.EqualTo(0));
        }

        [Test]
        public void Enqueue_WhenExceedsInitialCapacity_ShouldExpandAndNotLoseEvents()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            // 默认环形缓冲区容量 16，入队 20 个
            for (int i = 0; i < 20; i++)
            {
                _channel.Enqueue(new TestEvent { Value = i });
            }

            _channel.Flush();

            Assert.That(received.Count, Is.EqualTo(20));
            for (int i = 0; i < 20; i++)
            {
                Assert.That(received[i].Value, Is.EqualTo(i));
            }
        }

        [Test]
        public void Flush_ShouldDispatchAllPendingAndClear()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            _channel.Enqueue(new TestEvent { Value = 1 });
            _channel.Enqueue(new TestEvent { Value = 2 });
            _channel.Enqueue(new TestEvent { Value = 3 });

            _channel.Flush();

            Assert.That(received.Count, Is.EqualTo(3));
            Assert.That(_channel.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Flush_WhenNoPendingEvents_ShouldNotInvokeHandlers()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            _channel.Flush();

            Assert.That(received.Count, Is.EqualTo(0));
        }

        [Test]
        public void Flush_WhenCalledTwice_ShouldOnlyDispatchNewEvents()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            _channel.Enqueue(new TestEvent { Value = 1 });
            _channel.Flush();
            _channel.Flush(); // 第二次没有新事件

            Assert.That(received.Count, Is.EqualTo(1));
        }

        #endregion

        #region Clear

        [Test]
        public void Clear_ShouldRemoveAllHandlersAndPendingEvents()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));
            _channel.Enqueue(new TestEvent { Value = 1 });
            _channel.Enqueue(new TestEvent { Value = 2 });

            _channel.Clear();

            Assert.That(_channel.HandlerCount, Is.EqualTo(0));
            Assert.That(_channel.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Clear_WhenPublishAfterClear_ShouldNotInvokeOldHandlers()
        {
            var received = new List<TestEvent>();
            _channel.Subscribe(e => received.Add(e));

            _channel.Clear();
            _channel.Publish(new TestEvent { Value = 1 });
            _channel.Flush();

            Assert.That(received.Count, Is.EqualTo(0));
        }

        #endregion

        #region EventName

        [Test]
        public void EventName_ShouldReturnTypeName()
        {
            Assert.That(_channel.EventName, Is.EqualTo("TestEvent"));
        }

        #endregion
    }
}
