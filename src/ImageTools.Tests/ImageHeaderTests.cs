using NUnit.Framework;

namespace ImageTools.Tests
{
    [TestFixture]
    public class ImageHeaderTests
    {
        [Test]
        [TestCase("./inputs/glyph.png", 512, 512)]
        [TestCase("./inputs/1.jpg", 460, 333)]
        [TestCase("./inputs/twisty-road.bmp", 100, 100)]
        [TestCase("./inputs/twisty-road.gif", 100, 100)]
        public void reading_image_header_info(string path, int width, int height)
        {
            // TODO: add WebP & wavelet to the set we can read
            var size = ImageHeaders.GetDimensions(path);
            Assert.That(size.Width, Is.EqualTo(width), "Wrong width");
            Assert.That(size.Height, Is.EqualTo(height), "Wrong height");
        }
    }
}