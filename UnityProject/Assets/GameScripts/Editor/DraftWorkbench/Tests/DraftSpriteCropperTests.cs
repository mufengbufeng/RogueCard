#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// DraftSpriteCropper 的 mask alpha bbox 与裁剪辅助逻辑测试。
    /// </summary>
    [TestFixture]
    public class DraftSpriteCropperTests
    {
        /// <summary>
        /// TryCalculateAlphaBounds 应把 bottom-left Texture2D 像素坐标转换为源图 top-left bbox。
        /// </summary>
        [Test]
        public void TryCalculateAlphaBounds_返回TopLeft坐标系Bounds()
        {
            var mask = new Texture2D(6, 5, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), 30).ToArray();
            for (int y = 1; y <= 2; y++)
            {
                for (int x = 2; x <= 4; x++)
                {
                    pixels[y * 6 + x] = new Color32(0, 0, 0, 200);
                }
            }
            mask.SetPixels32(pixels);
            mask.Apply(false, false);

            bool hasBounds = DraftSpriteCropper.TryCalculateAlphaBounds(mask, out DraftSegmentBounds bounds, 8);

            Assert.IsTrue(hasBounds);
            Assert.AreEqual(2, bounds.x);
            Assert.AreEqual(2, bounds.y);
            Assert.AreEqual(3, bounds.width);
            Assert.AreEqual(2, bounds.height);
            Object.DestroyImmediate(mask);
        }

        /// <summary>
        /// TryCalculateAlphaBounds 在 mask 为空时应返回 false。
        /// </summary>
        [Test]
        public void TryCalculateAlphaBounds_空Mask返回False()
        {
            var mask = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            mask.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 0), 16).ToArray());
            mask.Apply(false, false);

            bool hasBounds = DraftSpriteCropper.TryCalculateAlphaBounds(mask, out DraftSegmentBounds bounds, 1);

            Assert.IsFalse(hasBounds);
            Assert.IsNull(bounds);
            Object.DestroyImmediate(mask);
        }
    }
}
#endif
