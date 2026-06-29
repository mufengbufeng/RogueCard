using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 GameModel / GameViewModel 事件波次展示状态的设置、清除和同步。
    /// </summary>
    [TestFixture]
    public class EventWaveStateTests
    {
        // ── GameModel ──

        [Test]
        public void SetEventWaveState_设置标题描述和继续文案()
        {
            var model = new GameModel();

            model.SetEventWaveState("宝箱", "发现了一个宝箱！", "打开");

            Assert.AreEqual("宝箱", model.CurrentWaveTitle);
            Assert.AreEqual("发现了一个宝箱！", model.CurrentWaveDesc);
            Assert.AreEqual("打开", model.CurrentWaveContinueText);
            Assert.IsTrue(model.IsAwaitingWaveConfirmation);
        }

        [Test]
        public void SetEventWaveState_空继续文案使用默认值()
        {
            var model = new GameModel();

            model.SetEventWaveState("商店", "浏览商品", null);

            Assert.AreEqual("继续", model.CurrentWaveContinueText);
        }

        [Test]
        public void ClearEventWaveState_重置所有事件波次状态()
        {
            var model = new GameModel();
            model.SetEventWaveState("宝箱", "发现宝箱", "打开");

            model.ClearEventWaveState();

            Assert.IsNull(model.CurrentWaveTitle);
            Assert.IsNull(model.CurrentWaveDesc);
            Assert.IsNull(model.CurrentWaveContinueText);
            Assert.IsFalse(model.IsAwaitingWaveConfirmation);
        }

        [Test]
        public void SetLevel_自动清除事件波次状态()
        {
            var model = new GameModel();
            model.SetEventWaveState("宝箱", "发现宝箱", "打开");

            // SetLevel 内部调用 ClearEventWaveState
            model.SetLevel(null, new System.Collections.Generic.List<GameConfig.level.LevelWave>());

            Assert.IsFalse(model.IsAwaitingWaveConfirmation);
            Assert.IsNull(model.CurrentWaveTitle);
        }

        // ── GameViewModel 同步 ──

        [Test]
        public void ViewModel_镜像事件波次状态()
        {
            var model = new GameModel();
            var vm = new GameViewModel();
            vm.BindModel(model);

            model.SetEventWaveState("宝箱", "发现宝箱", "打开");

            Assert.IsTrue(vm.IsAwaitingWaveConfirmation.Value);
            Assert.AreEqual("宝箱", vm.CurrentWaveTitle.Value);
            Assert.AreEqual("发现宝箱", vm.CurrentWaveDesc.Value);
            Assert.AreEqual("打开", vm.CurrentWaveContinueText.Value);

            vm.Dispose();
        }

        [Test]
        public void ViewModel_清除后重置响应式属性()
        {
            var model = new GameModel();
            var vm = new GameViewModel();
            vm.BindModel(model);

            model.SetEventWaveState("宝箱", "发现宝箱", "打开");
            model.ClearEventWaveState();

            Assert.IsFalse(vm.IsAwaitingWaveConfirmation.Value);
            Assert.AreEqual(string.Empty, vm.CurrentWaveTitle.Value);
            Assert.AreEqual(string.Empty, vm.CurrentWaveDesc.Value);
            Assert.AreEqual(string.Empty, vm.CurrentWaveContinueText.Value);

            vm.Dispose();
        }

        [Test]
        public void ViewModel_SyncAll_包含事件波次状态()
        {
            var model = new GameModel();
            model.SetEventWaveState("商店", "浏览商品", "继续");

            // SyncAll 在 BindModel 时调用
            var vm = new GameViewModel();
            vm.BindModel(model);

            Assert.IsTrue(vm.IsAwaitingWaveConfirmation.Value);
            Assert.AreEqual("商店", vm.CurrentWaveTitle.Value);

            vm.Dispose();
        }

        // ── IGameModelData 只读接口 ──

        [Test]
        public void IGameModelData_暴露事件波次状态()
        {
            var model = new GameModel();
            model.SetEventWaveState("宝箱", "发现宝箱", "打开");

            IGameModelData data = model.GetData();
            Assert.IsTrue(data.IsAwaitingWaveConfirmation);
            Assert.AreEqual("宝箱", data.CurrentWaveTitle);
            Assert.AreEqual("发现宝箱", data.CurrentWaveDesc);
            Assert.AreEqual("打开", data.CurrentWaveContinueText);
        }

        [Test]
        public void IGameModelData_默认无等待确认()
        {
            var model = new GameModel();
            IGameModelData data = model.GetData();

            Assert.IsFalse(data.IsAwaitingWaveConfirmation);
            Assert.IsNull(data.CurrentWaveTitle);
        }
    }
}
