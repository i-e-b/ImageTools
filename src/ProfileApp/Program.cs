using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using ImageTools;

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
            CDF_9_7.ReduceImage3D(img3d);
            sw.Stop();
            Console.WriteLine($"Core transform took {sw.Elapsed}");

            // STEP 3: output frames for inspection
            for (int z = 0; z < frames.Length; z++)
            {
                using (Bitmap f = img3d.ReadSlice(z)) {
                    f.SaveBmp($"./outputs/Cdf97_3d_f{z}.bmp");
                }
            }
            //Console.ReadKey();
        }

    }
}
