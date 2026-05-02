using System;
using System.Collections.Generic;
using EF.Common;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class ModuleSystemTests
    {
        // 测试用接口和实现
        private interface ITestManager : IEFManager { }
        private interface IAnotherManager : IEFManager { }

        private class FakeManager : ITestManager, IAnotherManager
        {
            public int UpdateCalls { get; private set; }
            public bool IsShutdown { get; private set; }

            public void Update(float elapseSeconds, float realElapseSeconds)
            {
                UpdateCalls++;
            }

            public void Shutdown()
            {
                IsShutdown = true;
            }
        }

        private class AnotherFakeManager : IAnotherManager
        {
            public int UpdateCalls { get; private set; }
            public bool IsShutdown { get; private set; }

            public void Update(float elapseSeconds, float realElapseSeconds)
            {
                UpdateCalls++;
            }

            public void Shutdown()
            {
                IsShutdown = true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            ModuleSystem.ShutdownAll();
        }

        [TearDown]
        public void TearDown()
        {
            ModuleSystem.ShutdownAll();
        }

        #region Register

        [Test]
        public void Register_WhenValid_ShouldBeRetrievable()
        {
            var manager = new FakeManager();

            ModuleSystem.Register<ITestManager>(manager);

            var result = ModuleSystem.Get<ITestManager>();
            Assert.That(result, Is.SameAs(manager));
        }

        [Test]
        public void Register_WhenDuplicateTypeWithoutReplace_ShouldThrowInvalidOperationException()
        {
            var manager1 = new FakeManager();
            var manager2 = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager1);

            Assert.Throws<InvalidOperationException>(() =>
                ModuleSystem.Register<ITestManager>(manager2));
        }

        [Test]
        public void Register_WhenSameInstanceRegisteredTwice_ShouldNotThrow()
        {
            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            Assert.DoesNotThrow(() => ModuleSystem.Register<ITestManager>(manager));
        }

        [Test]
        public void Register_WhenDuplicateTypeWithReplace_ShouldReplaceOldInstance()
        {
            var manager1 = new FakeManager();
            var manager2 = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager1);

            ModuleSystem.Register<ITestManager>(manager2, replace: true);

            Assert.That(ModuleSystem.Get<ITestManager>(), Is.SameAs(manager2));
            Assert.That(manager1.IsShutdown, Is.True);
        }

        [Test]
        public void Register_WhenNullModule_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ModuleSystem.Register<ITestManager>(null));
        }

        [Test]
        public void Register_WithExposeConcreteType_ShouldBeRetrievableByConcreteType()
        {
            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            var result = ModuleSystem.Get<FakeManager>();
            Assert.That(result, Is.SameAs(manager));
        }

        #endregion

        #region Get / TryGet

        [Test]
        public void Get_WhenRegistered_ShouldReturnInstance()
        {
            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            Assert.That(ModuleSystem.Get<ITestManager>(), Is.SameAs(manager));
        }

        [Test]
        public void Get_WhenNotRegistered_ShouldThrowInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.Get<ITestManager>());
        }

        [Test]
        public void TryGet_WhenRegistered_ShouldReturnTrue()
        {
            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            bool found = ModuleSystem.TryGet<ITestManager>(out var result);

            Assert.That(found, Is.True);
            Assert.That(result, Is.SameAs(manager));
        }

        [Test]
        public void TryGet_WhenNotRegistered_ShouldReturnFalse()
        {
            bool found = ModuleSystem.TryGet<ITestManager>(out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        #endregion

        #region Contains

        [Test]
        public void Contains_WhenRegistered_ShouldReturnTrue()
        {
            ModuleSystem.Register<ITestManager>(new FakeManager());

            Assert.That(ModuleSystem.Contains<ITestManager>(), Is.True);
        }

        [Test]
        public void Contains_WhenNotRegistered_ShouldReturnFalse()
        {
            Assert.That(ModuleSystem.Contains<ITestManager>(), Is.False);
        }

        #endregion

        #region Unregister

        [Test]
        public void Unregister_WhenRegistered_ShouldRemoveService()
        {
            ModuleSystem.Register<ITestManager>(new FakeManager());

            ModuleSystem.Unregister<ITestManager>();

            Assert.That(ModuleSystem.Contains<ITestManager>(), Is.False);
        }

        [Test]
        public void Unregister_WhenRegistered_ShouldCallShutdown()
        {
            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            ModuleSystem.Unregister<ITestManager>();

            Assert.That(manager.IsShutdown, Is.True);
        }

        [Test]
        public void Unregister_WhenNotRegistered_ShouldReturnFalse()
        {
            bool result = ModuleSystem.Unregister<ITestManager>();

            Assert.That(result, Is.False);
        }

        [Test]
        public void Unregister_WithShutdownFalse_ShouldNotCallShutdown()
        {
            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            ModuleSystem.Unregister<ITestManager>(shutdown: false);

            Assert.That(manager.IsShutdown, Is.False);
        }

        #endregion

        #region Scope 隔离

        [Test]
        public void ShutdownScope_ShouldOnlyAffectTargetScope()
        {
            var globalManager = new FakeManager();
            var scopedManager = new AnotherFakeManager();
            ModuleSystem.Register<ITestManager>(globalManager, scope: 0);
            ModuleSystem.Register<IAnotherManager>(scopedManager, scope: 1);

            ModuleSystem.ShutdownScope(1);

            Assert.That(ModuleSystem.Contains<ITestManager>(), Is.True);
            Assert.That(ModuleSystem.Contains<IAnotherManager>(), Is.False);
            Assert.That(scopedManager.IsShutdown, Is.True);
            Assert.That(globalManager.IsShutdown, Is.False);
        }

        [Test]
        public void ShutdownScope_WhenScopeEmpty_ShouldReturnZero()
        {
            int removed = ModuleSystem.ShutdownScope(99);

            Assert.That(removed, Is.EqualTo(0));
        }

        #endregion

        #region Update

        [Test]
        public void Update_ShouldCallUpdateOnAllRegisteredModules()
        {
            var manager1 = new FakeManager();
            var manager2 = new AnotherFakeManager();
            ModuleSystem.Register<ITestManager>(manager1);
            ModuleSystem.Register<IAnotherManager>(manager2);

            ModuleSystem.Update(1f, 1f);

            Assert.That(manager1.UpdateCalls, Is.EqualTo(1));
            Assert.That(manager2.UpdateCalls, Is.EqualTo(1));
        }

        [Test]
        public void Update_WhenNoModules_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => ModuleSystem.Update(1f, 1f));
        }

        #endregion

        #region ShutdownAll

        [Test]
        public void ShutdownAll_ShouldClearEverything()
        {
            ModuleSystem.Register<ITestManager>(new FakeManager());
            ModuleSystem.Register<IAnotherManager>(new AnotherFakeManager());

            ModuleSystem.ShutdownAll();

            Assert.That(ModuleSystem.ModuleCount, Is.EqualTo(0));
            Assert.That(ModuleSystem.Contains<ITestManager>(), Is.False);
            Assert.That(ModuleSystem.Contains<IAnotherManager>(), Is.False);
        }

        [Test]
        public void ShutdownAll_ShouldCallShutdownOnAllModules()
        {
            var manager1 = new FakeManager();
            var manager2 = new AnotherFakeManager();
            ModuleSystem.Register<ITestManager>(manager1);
            ModuleSystem.Register<IAnotherManager>(manager2);

            ModuleSystem.ShutdownAll();

            Assert.That(manager1.IsShutdown, Is.True);
            Assert.That(manager2.IsShutdown, Is.True);
        }

        [Test]
        public void ShutdownAll_WhenEmpty_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => ModuleSystem.ShutdownAll());
        }

        #endregion

        #region RegisteredServiceCount / ModuleCount

        [Test]
        public void RegisteredServiceCount_ShouldReflectBindings()
        {
            Assert.That(ModuleSystem.RegisteredServiceCount, Is.EqualTo(0));

            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            // ITestManager + FakeManager（exposeConcreteType）= 2 个绑定
            Assert.That(ModuleSystem.RegisteredServiceCount, Is.EqualTo(2));
        }

        [Test]
        public void ModuleCount_ShouldReflectUniqueModules()
        {
            Assert.That(ModuleSystem.ModuleCount, Is.EqualTo(0));

            var manager = new FakeManager();
            ModuleSystem.Register<ITestManager>(manager);

            // 同一个实例注册多个接口，ModuleCount 只算 1
            Assert.That(ModuleSystem.ModuleCount, Is.EqualTo(1));
        }

        #endregion
    }
}
