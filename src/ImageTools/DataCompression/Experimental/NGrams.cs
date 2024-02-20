using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImageTools.Utilities;

namespace ImageTools.DataCompression.Experimental
{
    public class NGrams
    {
        
        /// <summary>
        /// Lowest valued byte rotation of input
        /// </summary>
        private static long MinRot(long n, int w, out int r)
        {
            r = 0;
            if (w < 2) return n;
            
            var min = (ulong)n;
            var v = (ulong)n;
            var shift = 8*(w-1);
            var mask = 0xFFffFFff_FFffFFffUL >> (64 - shift);

            for (int i = 0; i < 8; i++)
            {
                v = ((v >> shift)&0xFF) | ((v&mask) << 8);
                if (v >= min) continue;
                
                min = v;
                r = i;
            }
            return (long)min;
        }

        /// <summary>
        /// minimum-n-gram => count
        /// </summary>
        private static Dictionary<long, int> BuildHistogram(int n, IEnumerable<byte> data)
        {
            var hist = new Dictionary<long, int>(); // n-gram, count
            var filters = new[]{
                0,
                0xFF, 
                0xFFff,           // n=2, Unique n-grams = 27'370 of 65'535 (41.76%);
                0xFFffFF,         // n=3, Unique n-grams = 186'774 of 16'777'215 (1.11%);
                0xFFffFFff,       // n=4, Unique n-grams = 287'268 of 4'294'967'295 (0.01%);
                0xFFffFFffFF,     // n=5, Unique n-grams = 341'309 of 1'099'511'627'775 (0.00%);
                0xFFffFFffFFff,   // n=6, Unique n-grams = 368'768 of 281'474'976'710'655 (0.00%);    Max n-gram count = 13'603;
                0xFFffFFffFFffFF, // n=7, Unique n-grams = 394'127 of 72'057'594'037'927'935 (0.00%); Max n-gram count = 13'036;
                -1                // n=8, Unique n-grams = 395'658                                    Max n-gram count = 12'492;
            };
            long filter = filters[n];
            long nGram = 0;
            
            foreach (var b in data)
            {
                nGram = ((nGram << 8) | (b)) & filter;
                var minGram = MinRot(nGram, n, out _); // deal with rotations, by finding minimum valued rotation.
                
                if (!hist.ContainsKey(minGram)) hist.Add(minGram,0);
                var val = hist[minGram]+1;
                hist[minGram] = val;
            }
            
            return hist;
        }

        public static void TestNGramsOfData(int n, int topTake, byte[] data)
        {
            if (n is < 2 or > 8) throw new Exception("n must be 2..8");
            
            var filters = new[]{
                0,
                0xFF, 
                0xFFff,           // n=2, Unique n-grams = 27'370 of 65'535 (41.76%);
                0xFFffFF,         // n=3, Unique n-grams = 186'774 of 16'777'215 (1.11%);
                0xFFffFFff,       // n=4, Unique n-grams = 287'268 of 4'294'967'295 (0.01%);
                0xFFffFFffFF,     // n=5, Unique n-grams = 341'309 of 1'099'511'627'775 (0.00%);
                0xFFffFFffFFff,   // n=6, Unique n-grams = 368'768 of 281'474'976'710'655 (0.00%);    Max n-gram count = 13'603;
                0xFFffFFffFFffFF, // n=7, Unique n-grams = 394'127 of 72'057'594'037'927'935 (0.00%); Max n-gram count = 13'036;
                -1                // n=8, Unique n-grams = 395'658                                    Max n-gram count = 12'492;
            };
            
            // Very approximate figures -- this is not real storage, as we are multi-counting n-grams in the rough code below!
            
            long filter = filters[n];

            var hist = BuildHistogram(n, data); // n-gram, count
            var max = hist.Values.Max();

            var percent = (100.0 * hist.Count) / filter;
            var sizeTake = topTake * n;
            var sizeAll = hist.Count * n;
            Console.WriteLine($"n={n}, Unique n-grams = {hist.Count} of {filter} ({percent:0.00}%);\r\nMax n-gram count = {max};" +
                              $"\r\nSize for top {topTake}: {Bin.Human(sizeTake)};\r\nSize for all: {Bin.Human(sizeAll)};");
            
            var stack = hist.OrderByDescending(k => k.Value).ToList();

            var readOut = new StringBuilder();
            var fmt = $"X{n*2}";
            int c = 0;
            int countTop = 0;
            foreach (var kvp in stack)
            {
                readOut.AppendLine($"    #{c} -> {kvp.Key.ToString(fmt)} x {kvp.Value} ({kvp.Value/n})  \"{LongToAscii(n, kvp.Key)}\"");
                countTop += kvp.Value;
                if (c++ >= topTake) break;
            }
            
            var sizeInTop = countTop;
            var remainingSize = data.Length - sizeInTop;
            Console.WriteLine($"Count in top {topTake}: {countTop}; Represents {Bin.Human(sizeInTop)}, remains {Bin.Human(remainingSize)};\r\n");
            
            var bitsToIndex = Math.Ceiling(Math.Log(topTake, 2));
            var bitsToRotate = Math.Ceiling(Math.Log(n, 2));
            var compact = ((countTop * bitsToIndex * bitsToRotate) / 8.0) + remainingSize + topTake;
            var percentCompact = (100.0 * compact) / data.Length;
            Console.WriteLine($"Compact approx {Bin.Human((int)compact)}; n{n},t{topTake} = ({percentCompact:0.00}%) ({bitsToIndex}+{bitsToRotate} bits per index)");
            
            Console.WriteLine(readOut.ToString());
            
            
            // TODO: go through input, either output an n-gram + rotation, or a run-length and raw bytes
        }
    
        private static string LongToAscii(int n, long v)
        {
            var skip = 8 - n;
            return System.Text.Encoding.ASCII.GetString(Bin.BigEndianBytes(v).Skip(skip).ToArray());
        }
    }
}