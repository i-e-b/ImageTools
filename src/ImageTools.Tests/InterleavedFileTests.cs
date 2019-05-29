using NUnit.Framework;

namespace ImageTools.Tests
{
    /// <summary>
    /// A container for multiple-plane images/videos
    /// that can be truncated without losing entire planes.
    /// <para></para>
    /// The plan is to store minimal meta data, and then interleave bytes.
    /// Planes can be different sizes, so once a plane runs out, the remaining
    /// ones should be packed tighter.
    /// </summary>
    [TestFixture]
    public class InterleavedFileTests {
        [Test]
        public void can_store_single_file () {
            Assert.Fail("Not yet implemented");
        }

        [Test]
        public void can_store_multiple_files () {
            Assert.Fail("Not yet implemented");
        }

        [Test]
        public void truncated_files_can_be_read () {
            Assert.Fail("Not yet implemented");
        }
    }
}