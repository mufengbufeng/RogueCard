using System.Collections.Generic;
using System.Reflection;
using EF.UI;
using GameConfig.level;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 WaveSystem 事件波次确认流程：
    /// - 非战斗波次进入等待确认状态而非自动推进；
    /// - 确认后推进到下一波次；
    /// - 战斗波次行为不受影响。
    /// </summary>
    [TestFixture]
    public class WaveSystemEventWaveTests
    {
        [Test]
        public void 非战斗波次_进入等待确认状态()
        {
            var (waveSystem, model, bus) = CreateWaveSystemWithWaves(
                NewWave(1, WaveType.Chest, "宝箱", "发现宝箱", "打开"));

            try
            {
                // WaveSystem.StartLevel 会调用 EnterCurrentWave → 进入等待确认
                Assert.IsTrue(model.IsAwaitingWaveConfirmation, "Chest 波次应进入等待确认状态");
                Assert.AreEqual("宝箱", model.CurrentWaveTitle);
                Assert.AreEqual("发现宝箱", model.CurrentWaveDesc);
                Assert.AreEqual("打开", model.CurrentWaveContinueText);
            }
            finally
            {
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void Shop波次_也进入等待确认()
        {
            var (waveSystem, model, bus) = CreateWaveSystemWithWaves(
                NewWave(1, WaveType.Shop, "商店", "浏览商品", "继续"));

            try
            {
                Assert.IsTrue(model.IsAwaitingWaveConfirmation);
                Assert.AreEqual("商店", model.CurrentWaveTitle);
            }
            finally
            {
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void 确认后_推进到下一波次()
        {
            // 两个波次：Chest → Chest
            var chestWave = NewWave(1, WaveType.Chest, "宝箱", "发现宝箱", "打开");
            var chestWave2 = NewWave(2, WaveType.Chest, "宝箱2", "又一个宝箱", "打开");
            var (waveSystem, model, bus) = CreateWaveSystemWithWaves(chestWave, chestWave2);

            try
            {
                // 初始在第一个 Chest 波次，等待确认
                Assert.IsTrue(model.IsAwaitingWaveConfirmation);
                Assert.AreEqual("宝箱", model.CurrentWaveTitle);

                // 确认后推进到第二个 Chest 波次，同样进入等待确认
                waveSystem.ConfirmCurrentWave();

                Assert.IsTrue(model.IsAwaitingWaveConfirmation, "第二个 Chest 也应等待确认");
                Assert.AreEqual("宝箱2", model.CurrentWaveTitle);
                Assert.AreEqual(1, model.WaveIndex, "应推进到第二个波次");
            }
            finally
            {
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void 未在等待确认时_ConfirmCurrentWave不推进()
        {
            var (waveSystem, model, bus) = CreateWaveSystemWithWaves();
            SetPrivateField(waveSystem, "_levelId", 1);

            try
            {
                // 没有波次，不在等待确认
                int waveIndexBefore = model.WaveIndex;
                Assert.IsFalse(model.IsAwaitingWaveConfirmation);

                waveSystem.ConfirmCurrentWave();

                Assert.AreEqual(waveIndexBefore, model.WaveIndex, "不在等待确认时 Confirm 不应推进");
            }
            finally
            {
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void Chest之后无更多波次_确认后关卡完成()
        {
            var (waveSystem, model, bus) = CreateWaveSystemWithWaves(
                NewWave(1, WaveType.Chest, "宝箱", "发现宝箱", "打开"));

            LevelCompleteEvent? received = null;
            bus.GetChannel<LevelCompleteEvent>().Subscribe(e => received = e);

            try
            {
                Assert.IsTrue(model.IsAwaitingWaveConfirmation);

                waveSystem.ConfirmCurrentWave();

                Assert.IsTrue(model.IsLevelComplete, "确认最后一个波次后应标记关卡完成");
                Assert.IsTrue(received.HasValue, "应发布 LevelCompleteEvent");
            }
            finally
            {
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        // ── 测试基础设施 ──

        private static (WaveSystem waveSystem, GameModel model, LocalEventBus bus)
            CreateWaveSystemWithWaves(params GameConfig.level.LevelWave[] waves)
        {
            var model = new GameModel();
            var bus = new LocalEventBus();
            var waveSystem = new WaveSystem();
            waveSystem.Init(model, bus);

            var waveList = new List<GameConfig.level.LevelWave>(waves);
            SetPrivateField(waveSystem, "_waves", waveList);
            SetPrivateField(waveSystem, "_levelId", 1);
            model.SetLevel(null, waveList);
            model.SetWaveIndex(0);

            // 模拟 EnterCurrentWave：手动调用私有方法
            InvokePrivate(waveSystem, "EnterCurrentWave");

            return (waveSystem, model, bus);
        }

        /// <summary>
        /// 通过反射构造 LevelWave 实例（绕过 Luban ByteBuf 构造器）。
        /// </summary>
        private static GameConfig.level.LevelWave NewWave(
            int id,
            GameConfig.level.WaveType type,
            string title,
            string desc,
            string continueText,
            int? payloadId = null)
        {
            var wave = (GameConfig.level.LevelWave)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(GameConfig.level.LevelWave));
            SetField(wave, "Id", id);
            SetField(wave, "Order", id);
            SetField(wave, "Type", type);
            SetField(wave, "Title", title);
            SetField(wave, "Desc", desc);
            SetField(wave, "ContinueText", continueText);
            SetField(wave, "PayloadId", payloadId);
            return wave;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到私有字段 {fieldName}");
            field.SetValue(target, value);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, $"未找到私有方法 {methodName}");
            method.Invoke(target, args);
        }
    }
}
