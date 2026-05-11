using System;
using EF.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// Shell 层级解析测试。ScreenRegistry 在 convention-based-screen-resolution
    /// 变更中删除，相关测试由 ScreenConventionTests / NavigatorTypeResolutionTests 接管。
    /// </summary>
    [TestFixture]
    public class ShellAndRegistryTests
    {
        // ── Shell 测试 ──

        /// <summary>
        /// 构造一个含有三个层级 child 的 root，用于模拟 UIDocument 加载 Root.uxml 后的状态。
        /// </summary>
        private static VisualElement BuildRoot()
        {
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "screen-layer" });
            root.Add(new VisualElement { name = "popup-layer" });
            root.Add(new VisualElement { name = "system-layer" });
            return root;
        }

        [Test]
        public void Shell_从root解析三个层级()
        {
            var root = BuildRoot();
            var shell = new Shell(root);

            Assert.AreEqual("screen-layer", shell.ScreenLayer.name);
            Assert.AreEqual("popup-layer", shell.PopupLayer.name);
            Assert.AreEqual("system-layer", shell.SystemLayer.name);
        }

        [Test]
        public void Shell_缺少层级时抛出明确异常()
        {
            var rootMissingPopup = new VisualElement();
            rootMissingPopup.Add(new VisualElement { name = "screen-layer" });
            rootMissingPopup.Add(new VisualElement { name = "system-layer" });

            Assert.Throws<InvalidOperationException>(() => new Shell(rootMissingPopup));
        }

        [Test]
        public void Shell_root为null时抛出ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Shell(null));
        }
    }
}
