using System;
using System.Collections.Generic;
using EF.Entity;
using EF.Feature;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests.EditMode.Framework
{
    /// <summary>
    /// FeatureContainer 是 internal 且依赖 IEntity（需要 GameObject）。
    /// 这里通过创建带 GameObject 的 FakeEntity 来测试 FeatureContainer。
    /// </summary>
    [TestFixture]
    public class FeatureTests
    {
        // 简单的测试 Feature
        public class SimpleFeature : FeatureBase
        {
            public bool OnInitCalled { get; private set; }
            public bool OnDestroyCalled { get; private set; }
            public bool OnEnableCalled { get; private set; }
            public bool OnDisableCalled { get; private set; }
            public int OnUpdateCalls { get; private set; }
            public object ReceivedSetupData { get; private set; }

            public override void OnInit()
            {
                OnInitCalled = true;
                base.OnInit();
            }

            public override void OnDestroy()
            {
                OnDestroyCalled = true;
                base.OnDestroy();
            }

            public override void OnEnable()
            {
                OnEnableCalled = true;
            }

            public override void OnDisable()
            {
                OnDisableCalled = true;
            }

            public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
            {
                OnUpdateCalls++;
            }

            public override void OnSetup(object data)
            {
                ReceivedSetupData = data;
            }
        }

        // 不允许多实例
        public class SingletonFeature : FeatureBase { }

        // 允许多实例
        [AllowMultiple]
        public class MultiFeature : FeatureBase { }

        // 依赖 SingletonFeature
        [RequireFeature(typeof(SingletonFeature))]
        public class DependentFeature : FeatureBase { }

        // FakeEntity 实现 IEntity 以支持 FeatureContainer 测试
        private class FakeEntity : IEntity
        {
            public GameObject Handle { get; private set; }
            public int Id { get; private set; }
            public string EntityAssetName { get; private set; }
            public IEntityGroup EntityGroup { get; private set; }
            public IFeatureContainer Features { get; private set; }

            public FakeEntity(int id, string assetName)
            {
                Id = id;
                EntityAssetName = assetName;
                Handle = new GameObject($"FakeEntity_{id}");
                Features = new FeatureContainer(this);
            }

            public void Destroy()
            {
                if (Handle != null)
                {
                    UnityEngine.Object.DestroyImmediate(Handle);
                }
            }

            public void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData) { }
            public void OnRecycle() { }
            public void OnShow(object userData) { }
            public void OnHide(bool isShutdown, object userData) { }
            public void OnAttached(IEntity childEntity, object userData) { }
            public void OnDetached(IEntity childEntity, object userData) { }
            public void OnAttachTo(IEntity parentEntity, object userData) { }
            public void OnDetachFrom(IEntity parentEntity, object userData) { }
            public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
        }

        private FakeEntity _entity;

        [SetUp]
        public void SetUp()
        {
            _entity = new FakeEntity(1, "TestEntity");
        }

        [TearDown]
        public void TearDown()
        {
            _entity.Destroy();
        }

        #region AddFeature / GetFeature / HasFeature

        [Test]
        public void AddFeature_ShouldCreateAndInitialize()
        {
            var feature = _entity.Features.AddFeature<SimpleFeature>();

            Assert.That(feature, Is.Not.Null);
            Assert.That(feature.OnInitCalled, Is.True);
            Assert.That(feature.IsInitialized, Is.True);
        }

        [Test]
        public void AddFeature_WithSetupData_ShouldReceiveData()
        {
            var feature = _entity.Features.AddFeature<SimpleFeature>("setup_data");

            Assert.That(feature.ReceivedSetupData, Is.EqualTo("setup_data"));
        }

        [Test]
        public void GetFeature_AfterAdd_ShouldReturnSameInstance()
        {
            var added = _entity.Features.AddFeature<SimpleFeature>();

            var retrieved = _entity.Features.GetFeature<SimpleFeature>();

            Assert.That(retrieved, Is.SameAs(added));
        }

        [Test]
        public void HasFeature_AfterAdd_ShouldReturnTrue()
        {
            _entity.Features.AddFeature<SimpleFeature>();

            Assert.That(_entity.Features.HasFeature<SimpleFeature>(), Is.True);
        }

        [Test]
        public void HasFeature_WhenNotAdded_ShouldReturnFalse()
        {
            Assert.That(_entity.Features.HasFeature<SimpleFeature>(), Is.False);
        }

        [Test]
        public void FeatureCount_ShouldReflectAddedFeatures()
        {
            Assert.That(_entity.Features.FeatureCount, Is.EqualTo(0));

            _entity.Features.AddFeature<SimpleFeature>();
            Assert.That(_entity.Features.FeatureCount, Is.EqualTo(1));
        }

        #endregion

        #region AllowMultiple

        [Test]
        public void AddFeature_WhenNoMultipleAttribute_ShouldThrowOnDuplicate()
        {
            _entity.Features.AddFeature<SingletonFeature>();

            Assert.Throws<InvalidOperationException>(() =>
                _entity.Features.AddFeature<SingletonFeature>());
        }

        [Test]
        public void AddFeature_WhenAllowMultiple_ShouldAllowDuplicates()
        {
            var f1 = _entity.Features.AddFeature<MultiFeature>();
            var f2 = _entity.Features.AddFeature<MultiFeature>();

            Assert.That(f1, Is.Not.SameAs(f2));
            var all = _entity.Features.GetFeatures<MultiFeature>();
            Assert.That(all.Length, Is.EqualTo(2));
        }

        #endregion

        #region RequireFeature

        [Test]
        public void AddFeature_WhenDependencyExists_ShouldSucceed()
        {
            _entity.Features.AddFeature<SingletonFeature>();

            Assert.DoesNotThrow(() => _entity.Features.AddFeature<DependentFeature>());
        }

        [Test]
        public void AddFeature_WhenDependencyMissing_ShouldThrowInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _entity.Features.AddFeature<DependentFeature>());
        }

        #endregion

        #region RemoveFeature

        [Test]
        public void RemoveFeature_ShouldCallOnDestroy()
        {
            var feature = _entity.Features.AddFeature<SimpleFeature>();

            _entity.Features.RemoveFeature<SimpleFeature>();

            Assert.That(feature.OnDestroyCalled, Is.True);
            Assert.That(_entity.Features.HasFeature<SimpleFeature>(), Is.False);
        }

        [Test]
        public void RemoveFeature_WhenNotExists_ShouldReturnFalse()
        {
            bool result = _entity.Features.RemoveFeature<SimpleFeature>();

            Assert.That(result, Is.False);
        }

        #endregion

        #region SetFeatureEnabled

        [Test]
        public void SetFeatureEnabled_WhenDisabling_ShouldCallOnDisable()
        {
            var feature = _entity.Features.AddFeature<SimpleFeature>();

            _entity.Features.SetFeatureEnabled<SimpleFeature>(false);

            Assert.That(feature.Enabled, Is.False);
            Assert.That(feature.OnDisableCalled, Is.True);
        }

        [Test]
        public void SetFeatureEnabled_WhenEnabling_ShouldCallOnEnable()
        {
            var feature = _entity.Features.AddFeature<SimpleFeature>();
            feature.Enabled = false;

            _entity.Features.SetFeatureEnabled<SimpleFeature>(true);

            Assert.That(feature.Enabled, Is.True);
            Assert.That(feature.OnEnableCalled, Is.True);
        }

        [Test]
        public void HasFeature_WhenDisabled_ShouldStillReturnTrue()
        {
            _entity.Features.AddFeature<SimpleFeature>();

            _entity.Features.SetFeatureEnabled<SimpleFeature>(false);

            Assert.That(_entity.Features.HasFeature<SimpleFeature>(), Is.True);
        }

        #endregion

        #region Update

        [Test]
        public void Update_ShouldCallOnEnabledFeaturesOnly()
        {
            var feature = _entity.Features.AddFeature<SimpleFeature>();
            var container = (FeatureContainer)_entity.Features;

            container.Update(1f, 1f);
            Assert.That(feature.OnUpdateCalls, Is.EqualTo(1));

            _entity.Features.SetFeatureEnabled<SimpleFeature>(false);
            container.Update(1f, 1f);
            Assert.That(feature.OnUpdateCalls, Is.EqualTo(1)); // 不应增加
        }

        #endregion

        #region GetAllFeatures

        [Test]
        public void GetAllFeatures_ShouldReturnAllAdded()
        {
            _entity.Features.AddFeature<SimpleFeature>();
            _entity.Features.AddFeature<SingletonFeature>();

            var all = _entity.Features.GetAllFeatures();

            Assert.That(all.Count, Is.EqualTo(2));
        }

        #endregion
    }
}
