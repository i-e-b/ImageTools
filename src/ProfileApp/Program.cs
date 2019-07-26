using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using ImageTools;
using ImageTools.Utilities;

namespace ProfileApp
{
    class Program
    {
        static void Main()
        {
            // STEP 1: Load frames
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(basePath, "../../../ImageTools.Tests/inputs/EasyFrames");
            var frames = Directory.EnumerateFiles(filePath)
                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)?.Substring(1) ?? "0"))
                .ToArray();

            var img3d = new Image3d(frames);

            // STEP 2: Do the decomposition
            var sw = new Stopwatch();
            sw.Start();
            var targetPath = Path.Combine(basePath, "outputs/w3d_a_packed.bin");
            WaveletCompress.ReduceImage3D_ToFile(img3d, targetPath);
            sw.Stop();
            Console.WriteLine($"Compression took {sw.Elapsed}. Written to {targetPath}");

            // STEP 3: Restore original from file
            sw.Reset();
            sw.Start();
            Image3d result = WaveletCompress.RestoreImage3D_FromFile(targetPath);
            sw.Stop();
            Console.WriteLine($"Restore took {sw.Elapsed}");

            // STEP 4: output frames for inspection
            for (int z = 0; z < result.Depth; z++)
            {
                using (Bitmap f = result.ReadSlice(z)) {
                    f.SaveBmp($"./outputs/w3d_f{z}.bmp");
                }
            }
        }

    }
}
