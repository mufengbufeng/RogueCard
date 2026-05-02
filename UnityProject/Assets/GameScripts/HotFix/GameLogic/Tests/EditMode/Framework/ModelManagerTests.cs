using System;
using System.Collections.Generic;
using EF.Model;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class ModelManagerTests
    {
        // 测试用数据接口
        public interface ITestData
        {
            int Value { get; }
        }

        // 测试用 Model
        public class TestModel : ModelBase<ITestData>
        {
            private int _value = 42;

            protected override ITestData CreateData()
            {
                return new TestData { Value = _value };
            }

            public void SetValue(int value)
            {
                _value = value;
            }
        }

        private class TestData : ITestData
        {
            public int Value { get; set; }
        }

        // 另一个测试 Model
        public interface IAnotherData
        {
            string Name { get; }
        }

        public class AnotherModel : ModelBase<IAnotherData>
        {
            protected override IAnotherData CreateData()
            {
                return new AnotherData { Name = "test" };
            }
        }

        private class AnotherData : IAnotherData
        {
            public string Name { get; set; }
        }

        private ModelManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = new ModelManager();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Shutdown();
        }

        #region Register / GetModel

        [Test]
        public void Register_WhenAutoCreated_ShouldBeRetrievable()
        {
            _manager.Register<TestModel>();

            var model = _manager.GetModel<TestModel>();

            Assert.That(model, Is.Not.Null);
        }

        [Test]
        public void Register_WhenInstance_ShouldBeRetrievable()
        {
            var model = new TestModel();
            _manager.Register(model);

            var retrieved = _manager.GetModel<TestModel>();

            Assert.That(retrieved, Is.SameAs(model));
        }

        [Test]
        public void Register_WhenDuplicate_ShouldThrowInvalidOperationException()
        {
            _manager.Register<TestModel>();

            Assert.Throws<InvalidOperationException>(() => _manager.Register<TestModel>());
        }

        [Test]
        public void Register_WithNullModel_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.Register<TestModel>(null));
        }

        [Test]
        public void GetModel_WhenNotRegistered_ShouldThrowKeyNotFoundException()
        {
            Assert.Throws<KeyNotFoundException>(() => _manager.GetModel<TestModel>());
        }

        #endregion

        #region Get<TData>

        [Test]
        public void Get_WhenRegistered_ShouldReturnDataInterface()
        {
            _manager.Register<TestModel>();

            var data = _manager.Get<ITestData>();

            Assert.That(data, Is.Not.Null);
            Assert.That(data.Value, Is.EqualTo(42));
        }

        [Test]
        public void Get_WhenNotRegistered_ShouldThrowKeyNotFoundException()
        {
            Assert.Throws<KeyNotFoundException>(() => _manager.Get<ITestData>());
        }

        [Test]
        public void Register_WithDataType_ShouldReturnData()
        {
            var data = _manager.Register<TestModel, ITestData>();

            Assert.That(data, Is.Not.Null);
            Assert.That(data.Value, Is.EqualTo(42));
        }

        #endregion

        #region TryGet

        [Test]
        public void TryGet_WhenRegistered_ShouldReturnTrue()
        {
            _manager.Register<TestModel>();

            bool found = _manager.TryGet(out ITestData data);

            Assert.That(found, Is.True);
            Assert.That(data, Is.Not.Null);
        }

        [Test]
        public void TryGet_WhenNotRegistered_ShouldReturnFalse()
        {
            bool found = _manager.TryGet(out ITestData data);

            Assert.That(found, Is.False);
            Assert.That(data, Is.Null);
        }

        #endregion

        #region HasModel

        [Test]
        public void HasModel_WhenRegistered_ShouldReturnTrue()
        {
            _manager.Register<TestModel>();

            Assert.That(_manager.HasModel<TestModel>(), Is.True);
        }

        [Test]
        public void HasModel_WhenNotRegistered_ShouldReturnFalse()
        {
            Assert.That(_manager.HasModel<TestModel>(), Is.False);
        }

        #endregion

        #region Unregister

        [Test]
        public void Unregister_WhenRegistered_ShouldReturnTrue()
        {
            _manager.Register<TestModel>();

            bool result = _manager.Unregister<TestModel>();

            Assert.That(result, Is.True);
            Assert.That(_manager.HasModel<TestModel>(), Is.False);
        }

        [Test]
        public void Unregister_WhenNotRegistered_ShouldReturnFalse()
        {
            bool result = _manager.Unregister<TestModel>();

            Assert.That(result, Is.False);
        }

        #endregion

        #region ModelCount

        [Test]
        public void ModelCount_ShouldReflectRegisteredModels()
        {
            Assert.That(_manager.ModelCount, Is.EqualTo(0));

            _manager.Register<TestModel>();
            Assert.That(_manager.ModelCount, Is.EqualTo(1));

            _manager.Register<AnotherModel>();
            Assert.That(_manager.ModelCount, Is.EqualTo(2));

            _manager.Unregister<TestModel>();
            Assert.That(_manager.ModelCount, Is.EqualTo(1));
        }

        #endregion

        #region Shutdown

        [Test]
        public void Shutdown_ShouldClearAllModels()
        {
            _manager.Register<TestModel>();
            _manager.Register<AnotherModel>();

            _manager.Shutdown();

            Assert.That(_manager.ModelCount, Is.EqualTo(0));
        }

        [Test]
        public void Shutdown_WhenEmpty_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Shutdown());
        }

        #endregion

        #region Update

        [Test]
        public void Update_WhenModelsRegistered_ShouldNotThrow()
        {
            _manager.Register<TestModel>();

            Assert.DoesNotThrow(() => _manager.Update(1f, 1f));
        }

        [Test]
        public void Update_WhenEmpty_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Update(1f, 1f));
        }

        #endregion

        #region TryGetModel

        [Test]
        public void TryGetModel_WhenNotRegistered_ShouldAutoCreate()
        {
            var model = _manager.TryGetModel<TestModel>();

            Assert.That(model, Is.Not.Null);
            Assert.That(_manager.HasModel<TestModel>(), Is.True);
        }

        [Test]
        public void TryGetModel_WhenAlreadyRegistered_ShouldReturnExisting()
        {
            var original = new TestModel();
            _manager.Register(original);

            var retrieved = _manager.TryGetModel<TestModel>();

            Assert.That(retrieved, Is.SameAs(original));
        }

        #endregion

        #region UnregisterByData

        [Test]
        public void UnregisterByData_WhenRegistered_ShouldRemoveModel()
        {
            _manager.Register<TestModel>();

            bool result = _manager.UnregisterByData<ITestData>();

            Assert.That(result, Is.True);
            Assert.That(_manager.HasModel<TestModel>(), Is.False);
        }

        [Test]
        public void UnregisterByData_WhenNotRegistered_ShouldReturnFalse()
        {
            bool result = _manager.UnregisterByData<ITestData>();

            Assert.That(result, Is.False);
        }

        #endregion
    }
}
