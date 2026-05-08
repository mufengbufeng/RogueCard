using Cysharp.Threading.Tasks;
using EF.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    [TestFixture]
    public class RegionTests
    {
        private VisualElement _slot;

        [SetUp]
        public void SetUp()
        {
            _slot = new VisualElement { name = "test-slot" };
        }

        [Test]
        public void Show_直接放置VisualElement()
        {
            var region = new Region(_slot);
            var content = new VisualElement { name = "content" };

            region.Show(content);

            Assert.AreEqual(1, _slot.childCount);
            Assert.AreSame(content, _slot[0]);
            Assert.AreSame(content, region.CurrentContent);
        }

        [Test]
        public void Show_连续调用替换内容()
        {
            var region = new Region(_slot);
            var content1 = new VisualElement { name = "content1" };
            var content2 = new VisualElement { name = "content2" };

            region.Show(content1);
            Assert.AreEqual(1, _slot.childCount);

            region.Show(content2);
            Assert.AreEqual(1, _slot.childCount);
            Assert.AreEqual("content2", _slot[0].name);
        }

        [Test]
        public void Clear_清空已有内容()
        {
            var region = new Region(_slot);
            region.Show(new VisualElement());

            region.Clear();

            Assert.AreEqual(0, _slot.childCount);
            Assert.IsNull(region.CurrentContent);
        }

        [Test]
        public void Clear_空Region安全()
        {
            var region = new Region(_slot);

            Assert.DoesNotThrow(() => region.Clear());
            Assert.AreEqual(0, _slot.childCount);
        }

        [Test]
        public void CurrentContent_空时返回null()
        {
            var region = new Region(_slot);
            Assert.IsNull(region.CurrentContent);
        }

        [Test]
        public void ShowAsync_无ResourceManager时不操作()
        {
            var region = new Region(_slot, resources: null);

            // ShowAsync 不应该抛异常
            region.ShowAsync("UI/NotExist").Forget();

            Assert.AreEqual(0, _slot.childCount);
        }
    }
}
