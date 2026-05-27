using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证 MonsterSystem 为每只新生成怪物分配的 InstanceId 计数器行为：
    /// 单调递增、跨多次分配累计、每场战斗 ResetForNewBattle 后复位为 1。
    /// 直接调用 internal AssignNextInstanceId，绕开 SpawnBatch 对 GameLogicEntry.Config 的静态依赖
    /// （遵循本仓库 EditMode 测试规约：不依赖配置表静态实例）。
    /// </summary>
    [TestFixture]
    public sealed class MonsterSystemInstanceIdTests
    {
        [Test]
        public void AssignNextInstanceId_StartsAtOne_AndIncrementsMonotonically()
        {
            var system = new MonsterSystem();

            int a = system.AssignNextInstanceId();
            int b = system.AssignNextInstanceId();
            int c = system.AssignNextInstanceId();

            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
            Assert.AreEqual(3, c);
        }

        [Test]
        public void AssignNextInstanceId_ContinuesAcrossMultipleCallSites()
        {
            // 模拟多次 SpawnBatch（每次产出多只怪物）共享同一计数器
            var system = new MonsterSystem();

            int[] firstBatch = { system.AssignNextInstanceId(), system.AssignNextInstanceId() };
            int[] secondBatch = { system.AssignNextInstanceId(), system.AssignNextInstanceId() };

            CollectionAssert.AreEqual(new[] { 1, 2 }, firstBatch);
            CollectionAssert.AreEqual(new[] { 3, 4 }, secondBatch);
        }

        [Test]
        public void ResetForNewBattle_RestartsCounterAtOne()
        {
            var system = new MonsterSystem();
            for (int i = 0; i < 7; i++)
            {
                system.AssignNextInstanceId(); // 第一场战斗共分配了 7 只
            }

            system.ResetForNewBattle();
            int first = system.AssignNextInstanceId();

            Assert.AreEqual(1, first, "新一场战斗的首次分配应回到 1");
        }
    }
}
