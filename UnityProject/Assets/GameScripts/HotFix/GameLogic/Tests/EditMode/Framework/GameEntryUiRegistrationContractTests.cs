using System.IO;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// Runtime 启动入口的源码契约测试，约束热更入口读取前必须注册 IUIManager。
    /// </summary>
    [TestFixture]
    public sealed class GameEntryUiRegistrationContractTests
    {
        /// <summary>
        /// GameEntry.Awake 必须注册基于 UIManager 的 IUIManager 实例。
        /// </summary>
        [Test]
        public void Awake_源码契约_注册IUIManager()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "GameScripts", "Runtime", "GameEntry.cs");
            path = Path.GetFullPath(path);

            Assert.IsTrue(File.Exists(path), $"未找到 GameEntry.cs：{path}");
            string source = File.ReadAllText(path);

            StringAssert.Contains("ModuleSystem.Register<IUIManager>(new UIManager(_resourceManager, _modelManager))", source);
            StringAssert.Contains("using EF.UI;", source);
        }
    }
}
