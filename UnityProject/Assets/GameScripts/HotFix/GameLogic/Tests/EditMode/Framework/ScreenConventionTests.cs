using EF.UI;
using NUnit.Framework;

namespace GameLogic.Tests
{
    /// <summary>
    /// Screen 命名约定测试：验证 UxmlLocation / UssLocation 默认按
    /// `{Stem}View → {Stem}Uxml / {Stem}Uss` 推导，且子类可 override。
    /// </summary>
    [TestFixture]
    public class ScreenConventionTests
    {
        [Test]
        public void UxmlLocation_默认按StemUxml约定推导()
        {
            var screen = new ConventionView();
            Assert.AreEqual("ConventionUxml", screen.UxmlLocation);
        }

        [Test]
        public void UssLocation_默认按StemUss约定推导()
        {
            var screen = new ConventionView();
            Assert.AreEqual("ConventionUss", screen.UssLocation);
        }

        [Test]
        public void 子类可override_UxmlLocation()
        {
            var screen = new CustomLocationView();
            Assert.AreEqual("Custom/Special", screen.UxmlLocation);
        }

        [Test]
        public void 子类可override_UssLocation()
        {
            var screen = new CustomLocationView();
            Assert.AreEqual("Custom/SpecialStyles", screen.UssLocation);
        }

        [Test]
        public void Popup派生类型也走相同约定()
        {
            var popup = new ConventionPopupView();
            Assert.AreEqual("ConventionPopupUxml", popup.UxmlLocation);
            Assert.AreEqual("ConventionPopupUss", popup.UssLocation);
        }

        [Test]
        public void 类名不以View结尾时附加后缀()
        {
            // 罕见用法：类名故意不以 View 结尾，约定按"附加后缀"规则
            var screen = new MaverickScreenName();
            Assert.AreEqual("MaverickScreenNameUxml", screen.UxmlLocation);
            Assert.AreEqual("MaverickScreenNameUss", screen.UssLocation);
        }

        // ── 测试辅助类型 ──

        private sealed class TestVm : ViewModelBase { }

        private sealed class ConventionView : Screen<TestVm>
        {
            protected override void OnSetup() { }
        }

        private sealed class CustomLocationView : Screen<TestVm>
        {
            public override string UxmlLocation => "Custom/Special";
            public override string UssLocation => "Custom/SpecialStyles";
            protected override void OnSetup() { }
        }

        private sealed class ConventionPopupView : Popup<TestVm>
        {
            protected override void OnSetup() { }
        }

        private sealed class MaverickScreenName : Screen<TestVm>
        {
            protected override void OnSetup() { }
        }
    }
}
