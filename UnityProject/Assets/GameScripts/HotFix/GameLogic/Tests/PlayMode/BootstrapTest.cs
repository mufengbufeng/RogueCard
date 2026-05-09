using System.Collections;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Resource;
using GameLogic.Tests.PlayMode.Framework;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameLogic.Tests.PlayMode
{
    /// <summary>
    /// PlayMode 测试基础设施冒烟测试：验证 <see cref="PlayModeTestBase"/> 能在干净状态下启动，
    /// 以及连续多个用例之间不会泄漏全局状态。
    /// </summary>
    public sealed class BootstrapTest : PlayModeTestBase
    {
        /// <summary>
        /// 入场前 ModuleSystem 应当干净；入场后基类应已初始化 ResourceManager（EditorSimulate）。
        /// </summary>
        [UnityTest]
        public IEnumerator Bootstrap_ModuleSystemStartsClean() => UniTask.ToCoroutine(async () =>
        {
            Assert.AreEqual(0, ModulesAtSetUpStart,
                "进入 [UnitySetUp] 前 ModuleSystem 应当为空，否则上一个测试遗留了状态");
            Assert.IsTrue(Resource.IsInitialized,
                "基类 SetUp 完成后 ResourceManager 应已完成初始化");
            Assert.AreEqual(ResourceMode.EditorSimulate, Resource.Mode,
                "PlayMode 测试基类应强制使用 EditorSimulate 模式");
            Assert.AreEqual("DefaultPackage", Resource.DefaultPackageName,
                "默认资源包名期望为 DefaultPackage");

            await UniTask.CompletedTask;
        });

        /// <summary>
        /// 与上一用例同名的隔离断言：两个用例都通过即证明 TearDown 在用例之间清理了 ModuleSystem。
        /// 用例命名顺序保证此用例在 Bootstrap_ModuleSystemStartsClean 之后运行（NUnit 默认按字母序）。
        /// </summary>
        [UnityTest]
        public IEnumerator Bootstrap_TwoConsecutiveTestsHaveNoStateLeak() => UniTask.ToCoroutine(async () =>
        {
            Assert.AreEqual(0, ModulesAtSetUpStart,
                "上一用例的 [UnityTearDown] 未把 ModuleSystem 清空，跨用例发生了状态泄漏");
            Assert.IsFalse(YooAssetsInitializedAtSetUpStart,
                "上一用例的 [UnityTearDown] 未把 YooAssets 反初始化，跨用例发生了静态状态泄漏");
            Assert.IsTrue(Resource.IsInitialized,
                "新用例的基类 SetUp 应当能重新初始化 ResourceManager");
            Assert.IsNotNull(TestRoot,
                "每个用例都应获得新的 TestRoot GameObject");
            Assert.AreEqual(0, TestRoot.transform.childCount,
                "新用例的 TestRoot 应当不含上一用例的子对象残留");

            await UniTask.CompletedTask;
        });
    }
}
