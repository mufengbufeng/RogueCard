using System.Collections.Generic;
using System.Reflection;
using EF.UI;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Game
{
    /// <summary>
    /// 验证 WaveSystem 在所有波次完成时的事件契约：
    /// - 在本地事件总线上发布 <see cref="LevelCompleteEvent"/>；
    /// - 不再向全局 <c>StartLevelRequestedEvent</c> 复用发布（避免被 MainMenuProcedure 误判为"再来一关"）。
    /// 通过 <see cref="BattleEndedEvent"/>(IsVictory=true) 触发 WaveSystem.AdvanceToNextWave 的"无后续波次"分支。
    /// </summary>
    [TestFixture]
    public class WaveSystemLevelCompleteTests
    {
        [Test]
        public void 所有波次完成_本地总线发布LevelCompleteEvent()
        {
            var (waveSystem, model, bus) = CreateWaveSystem(levelId: 42);
            try
            {
                LevelCompleteEvent? received = null;
                bus.GetChannel<LevelCompleteEvent>().Subscribe(e => received = e);

                bus.GetChannel<BattleEndedEvent>().Publish(new BattleEndedEvent(true));

                Assert.IsTrue(received.HasValue, "应在本地总线发布 LevelCompleteEvent");
                Assert.AreEqual(42, received.Value.LevelId);
                Assert.IsTrue(model.IsLevelComplete, "GameModel.IsLevelComplete 应被置为 true");
            }
            finally
            {
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        [Test]
        public void 所有波次完成_不发布全局StartLevelRequestedEvent()
        {
            // 临时注入全局 EventHub，验证 WaveSystem 不会再向其 StartLevelRequestedEvent 复用发布。
            // 旧实现会发布；新实现已移除该路径。
            var (waveSystem, _, bus) = CreateWaveSystem(levelId: 7);
            var originalHub = GetGameLogicEntryEventHub();
            var injectedHub = new EventHub();
            SetGameLogicEntryEventHub(injectedHub);

            try
            {
                int globalCount = 0;
                injectedHub.StartLevelRequestedEvent.Subscribe(_ => globalCount++);

                bus.GetChannel<BattleEndedEvent>().Publish(new BattleEndedEvent(true));

                Assert.AreEqual(0, globalCount,
                    "关卡完成不应再向全局 StartLevelRequestedEvent 发布事件");
            }
            finally
            {
                SetGameLogicEntryEventHub(originalHub);
                waveSystem.Dispose();
                bus.Dispose();
            }
        }

        // ── 测试基础设施 ──

        private static (WaveSystem waveSystem, GameModel model, LocalEventBus bus) CreateWaveSystem(int levelId)
        {
            var model = new GameModel();
            var bus = new LocalEventBus();
            var waveSystem = new WaveSystem();
            waveSystem.Init(model, bus);

            // 通过反射构造 AdvanceToNextWave 的 "无后续波次" 分支前置状态：
            // _waves 为空列表 + WaveIndex 已经到末尾（0 + 1 >= 0 == true）→ 直接走"关卡完成"分支。
            SetPrivateField(waveSystem, "_waves", new List<GameConfig.level.LevelWave>());
            SetPrivateField(waveSystem, "_levelId", levelId);
            return (waveSystem, model, bus);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到私有字段 {fieldName}");
            field.SetValue(target, value);
        }

        private static EventHub GetGameLogicEntryEventHub()
        {
            var field = typeof(GameLogicEntry).GetField(
                "_eventHub",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, "未找到 GameLogicEntry._eventHub 私有静态字段");
            return field.GetValue(null) as EventHub;
        }

        private static void SetGameLogicEntryEventHub(EventHub hub)
        {
            var field = typeof(GameLogicEntry).GetField(
                "_eventHub",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, "未找到 GameLogicEntry._eventHub 私有静态字段");
            field.SetValue(null, hub);
        }
    }
}
