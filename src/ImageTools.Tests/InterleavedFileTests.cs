using System.Text;
using ImageTools.ImageDataFormats;
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
            var oneStr = "This is the first file stream. It is the only one";
            var one = Encoding.UTF8.GetBytes(oneStr);
            var ms = new MemoryStream();

            var subject = new InterleavedFile(10, 9, 8, one);
            subject.WriteToStream(ms);

            ms.Seek(0, SeekOrigin.Begin);
            var bytes = ms.ToArray();
            var result = Encoding.UTF8.GetString(bytes);

            Console.WriteLine(result);

            var recovered = InterleavedFile.ReadFromStream(ms);
            Assert.That(recovered, Is.Not.Null, "Recovered result");
            Assert.That(recovered.Width, Is.EqualTo(10), "Width");
            Assert.That(recovered.Height, Is.EqualTo(9), "Height");
            Assert.That(recovered.Depth, Is.EqualTo(8), "Depth");

            Assert.That(Encoding.UTF8.GetString(recovered.Planes[0]), Is.EqualTo(oneStr));
        }

        [Test]
        public void can_store_multiple_files () {
            var oneStr = "This is the first file stream. It's is the longest one by far enough.";
            var twoStr = "This is the second";
            var threeStr = "This is the third file stream, middle size.";

            var one = Encoding.UTF8.GetBytes(oneStr);
            var two = Encoding.UTF8.GetBytes(twoStr);
            var three = Encoding.UTF8.GetBytes(threeStr);

            var ms = new MemoryStream();

            var subject = new InterleavedFile(1000,20000,30000, one, two, three);
            subject.WriteToStream(ms);

            ms.Seek(0, SeekOrigin.Begin);
            var bytes = ms.ToArray();
            var result = Encoding.UTF8.GetString(bytes);

            Console.WriteLine(result);
            
            var recovered = InterleavedFile.ReadFromStream(ms);
            Assert.That(recovered, Is.Not.Null, "Recovered result");
            Assert.That(recovered.Width, Is.EqualTo(1000), "Width");
            Assert.That(recovered.Height, Is.EqualTo(20000), "Height");
            Assert.That(recovered.Depth, Is.EqualTo(30000), "Depth");

            Assert.That(Encoding.UTF8.GetString(recovered.Planes[0]), Is.EqualTo(oneStr));
            Assert.That(Encoding.UTF8.GetString(recovered.Planes[1]), Is.EqualTo(twoStr));
            Assert.That(Encoding.UTF8.GetString(recovered.Planes[2]), Is.EqualTo(threeStr));
        }

        [Test]
        public void truncated_files_can_be_read () {
            var oneStr = "This is the first file stream. It's is the longest one by far enough.";
            var twoStr = "This is the second";
            var threeStr = "This is the third file stream, middle size.";

            var one = Encoding.UTF8.GetBytes(oneStr);
            var two = Encoding.UTF8.GetBytes(twoStr);
            var three = Encoding.UTF8.GetBytes(threeStr);

            var original = new MemoryStream();

            var subject = new InterleavedFile(1000,20000,30000, one, two, three);
            subject.WriteToStream(original);

            original.Seek(0, SeekOrigin.Begin);
            var bytes = original.ToArray();

            var truncated = new MemoryStream(bytes, 0, bytes.Length / 2); // lose half the file

            
            var recovered = InterleavedFile.ReadFromStream(truncated);
            Assert.That(recovered, Is.Not.Null, "Recovered result");
            Assert.That(recovered.Width, Is.EqualTo(1000), "Width");
            Assert.That(recovered.Height, Is.EqualTo(20000), "Height");
            Assert.That(recovered.Depth, Is.EqualTo(30000), "Depth");

            Console.WriteLine(Encoding.UTF8.GetString(recovered.Planes[0]).Trim('\0'));
            Console.WriteLine(Encoding.UTF8.GetString(recovered.Planes[1]).Trim('\0'));
            Console.WriteLine(Encoding.UTF8.GetString(recovered.Planes[2]).Trim('\0'));
        }
    }
}