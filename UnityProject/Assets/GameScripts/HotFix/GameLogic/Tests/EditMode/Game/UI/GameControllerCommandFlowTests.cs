using System.Collections.Generic;
using EF.Model;
using EF.Resource;
using EF.UI;
using GameConfig.card;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证 UGUI GameView/GameController 的命令转发路径。
    /// </summary>
    [TestFixture]
    public sealed class GameControllerCommandFlowTests
    {
        private GameObject _layerRoot;
        private GameObject _viewObject;
        private GameView _view;
        private ReferenceCollector _collector;
        private GameController _controller;
        private GameViewModel _viewModel;

        /// <summary>
        /// 通过真实 GameView.prefab 构造 UGUI View/Controller。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _layerRoot = new GameObject("LayerRoot", typeof(RectTransform));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetRaw/UI/Game/GameView.prefab");
            Assert.NotNull(prefab, "未找到 GameView.prefab，无法验证控制器命令流。");
            _viewObject = Object.Instantiate(prefab);
            _viewObject.name = "GameView";
            _viewObject.transform.SetParent(_layerRoot.transform, false);
            _view = _viewObject.GetComponent<GameView>();
            Assert.NotNull(_view, "GameView.prefab 根对象必须挂载 GameView。");
            _collector = _viewObject.GetComponent<ReferenceCollector>();
            Assert.NotNull(_collector, "GameView.prefab 根对象必须挂载 ReferenceCollector。");
            _collector.OnAfterDeserialize();
            _controller = new GameController();
            _viewModel = new GameViewModel();

            var manager = new UIManager(new ThrowingResourceManager(), new ModelManager());
            manager.RegisterLayerRoot(UILayer.Normal, _layerRoot.transform);
            var descriptor = new UIWindowDescriptor(
                "GameView",
                "GameView",
                typeof(GameView),
                () => _controller,
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false);
            var context = new UIRuntimeContext(manager, new ModelManager(), descriptor, _layerRoot.transform);

            _view.InternalInitialize(context);
            _controller.InternalInitialize(_view, context);
            _controller.InternalEnter(_viewModel);
        }

        /// <summary>
        /// 清理测试创建的对象。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _controller?.InternalExit();
            _controller?.InternalRelease();
            _controller?.Dispose();
            _view?.InternalRelease();
            _viewModel?.Dispose();
            Object.DestroyImmediate(_viewObject);
            Object.DestroyImmediate(_layerRoot);
        }

        /// <summary>
        /// 结束回合按钮点击必须触发 ViewModel.EndTurnRequested。
        /// </summary>
        [Test]
        public void ClickEndTurn_触发结束回合命令()
        {
            int calls = 0;
            _viewModel.EndTurnRequested += () => calls++;

            _view.NotifyEndTurnClickedForTests();

            Assert.AreEqual(1, calls);
        }

        /// <summary>
        /// 点击手牌应触发 CardUsed(handIndex,-1)。
        /// </summary>
        [Test]
        public void ClickHandCard_触发自动目标出牌命令()
        {
            var calls = new List<(int handIndex, int targetIndex)>();
            _viewModel.CardUsed += (handIndex, targetIndex) => calls.Add((handIndex, targetIndex));
            _viewModel.Hand.Value = new[] { NewRuntimeCard(1) };

            _view.NotifyHandCardClickedForTests(0);

            CollectionAssert.AreEqual(new[] { (0, -1) }, calls);
        }

        /// <summary>
        /// 选择怪物目标应触发 CardUsed(handIndex,targetIndex)。
        /// </summary>
        [Test]
        public void SelectMonsterTarget_触发手动目标出牌命令()
        {
            var calls = new List<(int handIndex, int targetIndex)>();
            _viewModel.CardUsed += (handIndex, targetIndex) => calls.Add((handIndex, targetIndex));

            _view.NotifyMonsterTargetSelectedForTests(2, 1);

            CollectionAssert.AreEqual(new[] { (2, 1) }, calls);
        }

        /// <summary>
        /// 奖励确认按钮点击必须触发 ViewModel.RewardSelected。
        /// </summary>
        [Test]
        public void ClickRewardConfirm_触发奖励确认命令()
        {
            int calls = 0;
            _viewModel.RewardSelected += () => calls++;

            Button rewardButton = _collector.Get<Button>("RewardConfirmBtn");
            Assert.NotNull(rewardButton, "GameView.prefab 必须绑定 RewardConfirmBtn。");
            rewardButton.onClick.Invoke();

            Assert.AreEqual(1, calls);
        }

        /// <summary>
        /// ViewModel 状态变化必须经 GameController 触发 GameView 刷新。
        /// </summary>
        [Test]
        public void ViewModel变化_触发GameView刷新()
        {
            _viewModel.Phase.Value = BattlePhase.PlayerTurn;
            _viewModel.PlayerMaxHp.Value = 100;
            _viewModel.PlayerHp.Value = 25;

            Assert.AreEqual("你的回合", GetText("InfoText").text);
            Assert.AreEqual("25/100", GetText("PlayerHpText").text);
        }

        /// <summary>
        /// 出牌失败事件必须经 GameController 进入 TurnControlView toast。
        /// </summary>
        [Test]
        public void 出牌失败事件_进入TurnControlToast()
        {
            _viewModel.NotifyCardPlayFailed("InvalidTarget");

            TextMeshProUGUI toast = GetText("FailToast");
            Assert.AreEqual("无效目标", toast.text);
            Assert.AreEqual(1f, toast.GetComponent<CanvasGroup>().alpha, 0.001f);
        }

        /// <summary>
        /// Controller 释放后必须解绑 View 事件，避免重复转发命令。
        /// </summary>
        [Test]
        public void Controller释放后_解绑View事件()
        {
            int calls = 0;
            _viewModel.EndTurnRequested += () => calls++;

            _controller.InternalExit();
            _controller.InternalRelease();
            _controller.Dispose();
            _controller = null;

            _view.NotifyEndTurnClickedForTests();

            Assert.AreEqual(0, calls);
        }

        private static CardRuntime NewRuntimeCard(int id)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetField(card, "Id", id);
            SetField(card, "Name", $"Card_{id}");
            SetField(card, "Cost", 1);
            SetField(card, "TargetMode", TargetMode.SingleAuto);
            return new CardRuntime { Config = card };
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        private TextMeshProUGUI GetText(string key)
        {
            TextMeshProUGUI text = _collector.Get<TextMeshProUGUI>(key);
            Assert.NotNull(text, $"ReferenceCollector 缺少文本组件：{key}");
            return text;
        }

        private sealed class ThrowingResourceManager : IResourceManager
        {
            public ResourceMode Mode => ResourceMode.EditorSimulate;
            public bool IsInitialized => true;
            public string DefaultPackageName => "DefaultPackage";
            public ResourceModeConfig Configuration => null;
            public Cysharp.Threading.Tasks.UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, System.IProgress<float> progress = null) => Cysharp.Threading.Tasks.UniTask.CompletedTask;
            public YooAsset.ResourcePackage GetPackage(string packageName) => throw new System.NotSupportedException();
            public YooAsset.ResourcePackage GetDefaultPackage() => throw new System.NotSupportedException();
            public Cysharp.Threading.Tasks.UniTask<YooAsset.AssetHandle> LoadAssetAsync<T>(string location, System.Action<float> progress = null, uint priority = 0) where T : Object => throw new System.NotSupportedException();
            public YooAsset.AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : Object => throw new System.NotSupportedException();
            public Cysharp.Threading.Tasks.UniTask<YooAsset.SceneHandle> LoadSceneAsync(string location, UnityEngine.SceneManagement.LoadSceneMode sceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single, UnityEngine.SceneManagement.LocalPhysicsMode physicsMode = UnityEngine.SceneManagement.LocalPhysicsMode.None, bool allowSceneActivation = true, uint priority = 0, System.Action<float> progress = null) => throw new System.NotSupportedException();
            public void UnloadScene(YooAsset.SceneHandle handle) { }
            public void Release(YooAsset.HandleBase handle) { }
            public void ReleaseAll() { }
            public void Shutdown() { }
            public void Update(float elapseSeconds, float realElapseSeconds) { }
        }
    }
}
