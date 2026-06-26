using System;
using EF.Model;
using EF.Resource;
using EF.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameLogic.Tests
{
    /// <summary>
    /// 使用真实 GameView.prefab 验证 UGUI 局内界面的集成装配。
    /// </summary>
    [TestFixture]
    public sealed class GameViewIntegrationTests
    {
        private const string PrefabPath = "Assets/AssetRaw/UI/Game/GameView.prefab";

        private GameObject _layerRoot;
        private GameObject _viewObject;
        private ReferenceCollector _collector;
        private GameView _view;
        private GameViewModel _viewModel;

        /// <summary>
        /// 实例化真实 Prefab 并初始化 UIView 生命周期。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _layerRoot = new GameObject("LayerRoot", typeof(RectTransform));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.NotNull(prefab, $"未找到 Prefab：{PrefabPath}");

            _viewObject = Object.Instantiate(prefab);
            _viewObject.name = "GameView";
            _viewObject.transform.SetParent(_layerRoot.transform, false);
            _collector = _viewObject.GetComponent<ReferenceCollector>();
            Assert.NotNull(_collector, "GameView.prefab 必须挂载 ReferenceCollector。");
            _collector.OnAfterDeserialize();

            _view = _viewObject.GetComponent<GameView>();
            Assert.NotNull(_view, "GameView.prefab 根对象必须挂载 GameView。");
            _viewModel = new GameViewModel();
            _view.InternalInitialize(CreateRuntimeContext());
        }

        /// <summary>
        /// 释放测试创建的 View 生命周期和 GameObject。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _view?.InternalRelease();
            _viewModel?.Dispose();
            Object.DestroyImmediate(_viewObject);
            Object.DestroyImmediate(_layerRoot);
        }

        /// <summary>
        /// 战斗阶段必须显示 BattlePanel 并刷新玩家状态。
        /// </summary>
        [Test]
        public void Render_战斗阶段_显示战斗面板并刷新玩家状态()
        {
            _viewModel.Phase.Value = BattlePhase.PlayerTurn;
            _viewModel.PlayerMaxHp.Value = 100;
            _viewModel.PlayerHp.Value = 40;
            _viewModel.PlayerArmor.Value = 7;
            _viewModel.MaxEnergy.Value = 3;
            _viewModel.Energy.Value = 2;

            _view.Render(_viewModel);

            Assert.IsTrue(GetGameObject("BattlePanel").activeSelf);
            Assert.IsFalse(GetGameObject("RewardPanel").activeSelf);
            Assert.AreEqual("你的回合", GetComponent<TextMeshProUGUI>("InfoText").text);
            Assert.AreEqual("40/100", GetComponent<TextMeshProUGUI>("PlayerHpText").text);
            Assert.AreEqual(0.4f, GetComponent<Image>("PlayerHpFill").fillAmount, 0.001f);
            Assert.AreEqual("7", GetComponent<TextMeshProUGUI>("PlayerArmorText").text);
            Assert.AreEqual("2/3", GetComponent<TextMeshProUGUI>("PlayerEnergyText").text);
        }

        /// <summary>
        /// 奖励阶段必须隐藏 BattlePanel、显示 RewardPanel，并释放战斗子视图。
        /// </summary>
        [Test]
        public void Render_奖励阶段_切换到奖励面板并释放战斗面板()
        {
            _viewModel.Phase.Value = BattlePhase.PlayerTurn;
            _view.Render(_viewModel);
            Assert.IsTrue(GetGameObject("BattlePanel").activeSelf);

            _viewModel.Phase.Value = BattlePhase.Reward;
            _view.Render(_viewModel);

            Assert.IsFalse(GetGameObject("BattlePanel").activeSelf);
            Assert.IsTrue(GetGameObject("RewardPanel").activeSelf);
            Assert.IsNull(GetPrivateField("_battlePanelView"));
        }

        /// <summary>
        /// GameView.ShowCardPlayFailed 必须转发给 BattlePanel 的失败 toast。
        /// </summary>
        [Test]
        public void ShowCardPlayFailed_转发到战斗面板Toast()
        {
            _viewModel.Phase.Value = BattlePhase.PlayerTurn;
            _view.Render(_viewModel);

            _view.ShowCardPlayFailed("InsufficientEnergy");

            TextMeshProUGUI toast = GetComponent<TextMeshProUGUI>("FailToast");
            CanvasGroup group = toast.GetComponent<CanvasGroup>();
            Assert.AreEqual("能量不足", toast.text);
            Assert.AreEqual(1f, group.alpha, 0.001f);
            Assert.IsTrue(toast.gameObject.activeSelf);
        }

        /// <summary>
        /// 事件波次等待确认时必须显示 RewardPanel 并刷新标题和按钮文案。
        /// </summary>
        [Test]
        public void Render_事件波次等待确认_显示RewardPanel并刷新文案()
        {
            _viewModel.Phase.Value = BattlePhase.PlayerTurn;
            _view.Render(_viewModel);
            Assert.IsTrue(GetGameObject("BattlePanel").activeSelf);
            Assert.IsFalse(GetGameObject("RewardPanel").activeSelf);

            // 模拟进入事件波次等待确认
            _viewModel.IsAwaitingWaveConfirmation.Value = true;
            _viewModel.CurrentWaveTitle.Value = "神秘宝箱";
            _viewModel.CurrentWaveContinueText.Value = "打开宝箱";
            _view.Render(_viewModel);

            Assert.IsFalse(GetGameObject("BattlePanel").activeSelf, "事件波次应隐藏 BattlePanel");
            Assert.IsTrue(GetGameObject("RewardPanel").activeSelf, "事件波次应显示 RewardPanel");
            Assert.AreEqual("神秘宝箱", GetComponent<TextMeshProUGUI>("RewardTitleText").text);
            // 按钮文本在 RewardConfirmBtn 的子对象 Text 上
            var btnText = GetGameObject("RewardConfirmBtn").GetComponentInChildren<TextMeshProUGUI>();
            Assert.AreEqual("打开宝箱", btnText.text);
        }

        /// <summary>
        /// 事件波次确认后恢复 BattlePanel。
        /// </summary>
        [Test]
        public void Render_事件波次确认后_恢复BattlePanel()
        {
            _viewModel.IsAwaitingWaveConfirmation.Value = true;
            _viewModel.CurrentWaveTitle.Value = "宝箱";
            _viewModel.CurrentWaveContinueText.Value = "打开";
            _view.Render(_viewModel);
            Assert.IsTrue(GetGameObject("RewardPanel").activeSelf);

            // 确认后恢复战斗阶段
            _viewModel.IsAwaitingWaveConfirmation.Value = false;
            _viewModel.Phase.Value = BattlePhase.PlayerTurn;
            _view.Render(_viewModel);

            Assert.IsTrue(GetGameObject("BattlePanel").activeSelf, "确认后应恢复 BattlePanel");
            Assert.IsFalse(GetGameObject("RewardPanel").activeSelf, "确认后应隐藏 RewardPanel");
        }

        private UIRuntimeContext CreateRuntimeContext()
        {
            var manager = new UIManager(new ThrowingResourceManager(), new ModelManager());
            manager.RegisterLayerRoot(UILayer.Normal, _layerRoot.transform);
            var descriptor = new UIWindowDescriptor(
                "GameView",
                "GameView",
                typeof(GameView),
                () => new GameController(),
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false);
            return new UIRuntimeContext(manager, new ModelManager(), descriptor, _layerRoot.transform);
        }

        private GameObject GetGameObject(string key)
        {
            GameObject value = _collector.Get<GameObject>(key);
            Assert.NotNull(value, $"ReferenceCollector 缺少 GameObject：{key}");
            return value;
        }

        private T GetComponent<T>(string key) where T : Component
        {
            T value = _collector.Get<T>(key);
            Assert.NotNull(value, $"ReferenceCollector 缺少组件：{key}/{typeof(T).Name}");
            return value;
        }

        private object GetPrivateField(string name)
        {
            var field = typeof(GameView).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field, $"未找到私有字段 {name}");
            return field.GetValue(_view);
        }

        private sealed class ThrowingResourceManager : IResourceManager
        {
            public ResourceMode Mode => ResourceMode.EditorSimulate;
            public bool IsInitialized => true;
            public string DefaultPackageName => "DefaultPackage";
            public ResourceModeConfig Configuration => null;
            public Cysharp.Threading.Tasks.UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null) => Cysharp.Threading.Tasks.UniTask.CompletedTask;
            public YooAsset.ResourcePackage GetPackage(string packageName) => throw new NotSupportedException();
            public YooAsset.ResourcePackage GetDefaultPackage() => throw new NotSupportedException();
            public Cysharp.Threading.Tasks.UniTask<YooAsset.AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0) where T : Object => throw new NotSupportedException();
            public YooAsset.AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : Object => throw new NotSupportedException();
            public Cysharp.Threading.Tasks.UniTask<YooAsset.SceneHandle> LoadSceneAsync(string location, UnityEngine.SceneManagement.LoadSceneMode sceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single, UnityEngine.SceneManagement.LocalPhysicsMode physicsMode = UnityEngine.SceneManagement.LocalPhysicsMode.None, bool allowSceneActivation = true, uint priority = 0, Action<float> progress = null) => throw new NotSupportedException();
            public void UnloadScene(YooAsset.SceneHandle handle) { }
            public void Release(YooAsset.HandleBase handle) { }
            public void ReleaseAll() { }
            public void Shutdown() { }
            public void Update(float elapseSeconds, float realElapseSeconds) { }
        }
    }
}
