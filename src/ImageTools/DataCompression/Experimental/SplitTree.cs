using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImageTools.ImageDataFormats;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// Turns a sequence of bytes into a sequence of bit fields
    /// </summary>
    public class SplitTree
    {
        public static void Compress(byte[] src, Stream encoded)
        {
            // We start with a lower value range, and a length.
            // We then have a bit field that is that length,
            // each bit represents a byte in the output; a value
            // of 0 means the byte is in the lower range, and a
            // value of 1 means the byte is above the range.
            // 
            // We repeat this structure until the range is a single
            // value, at which point the bytes are known.
            //
            // This is a variation of https://en.wikipedia.org/wiki/Wavelet_Tree
            
            var diag = new StringBuilder();
            
            var output = new BitwiseStreamWrapper(encoded, 0);
            var bracketStack = new Stack<Bracket>();
            
            // Write total length to output (as Fibonacci number)
            DataEncoding.FibonacciEncodeOne((uint)src.Length, output);
            diag.Append($"! {src.Length} ");
            
            bracketStack.Push(new Bracket(0, 255, src));
            
            int splitBitsWritten = 0;
            
            int safetyStop = 0;
            // Write the median, then try to move lower. If nothing, go back up and do next higher.
            while (true)
            {
                if (bracketStack.Count < 1){ Console.WriteLine("STACK DEAD"); break; }
                if (bracketStack.Peek()!.Empty()){ Console.WriteLine("DEAD END"); break; }

                if (safetyStop++ > 100) { Console.WriteLine("OVERRUN"); break; }

                // Get the range
                var range = bracketStack.Peek();
                if (range is null)
                {
                    Console.WriteLine("NULL");
                    break;
                }

                Console.WriteLine(range.ToString());
                
                // Write the split point
                output.WriteByteUnaligned((byte)range.Split);
                diag.Append($"[ {range.Split} ]-> ");
                var expectedLower = 0;
                var expectedUpper = 0;
                var writtenThisTime = 0;
                
                // Output bits for data that is in the range
                // with bit showing side of the split
                for (int i = 0; i < src.Length; i++)
                {
                    var v = src[i];
                    if (range.Excludes(v))
                    {
                       // diag.Append('x');
                        continue;
                    }

                    var isLower = v <= range.Split;
                    output.WriteBit(isLower);
                    diag.Append(isLower ? '1' : '0');
                    
                    if (isLower) expectedLower++;
                    else expectedUpper++;
                    writtenThisTime++;
                    splitBitsWritten++;
                }
                
                diag.Append($"; <-({writtenThisTime}) | ({expectedLower}/{expectedUpper})-> ");
                
                var nextLower = new Bracket(range.Lower, range.Split, src);
                if (nextLower.SplitCount > 0) // Try to split on lower side
                {
                    Console.WriteLine("LEFT");
                    bracketStack.Push(nextLower);
                }
                else // otherwise try to switch to right side
                {
                    bracketStack.Pop();
                    if (bracketStack.Count < 1) { Console.WriteLine("EMPTY"); break; }

                    var prev = bracketStack.Peek();
                    if (prev is not null && range.Split < prev.Upper)
                    {
                        Console.WriteLine("RIGHT");
                        var next = new Bracket(prev.Split, prev.Upper, src);
                        bracketStack.Pop(); // lower side is complete
                        bracketStack.Push(next);
                    }
                    else // nothing left
                    {
                        Console.WriteLine("END");
                        break;
                    }
                }
            }

            output.Flush();
            Console.WriteLine($"\r\nSplit bits = {splitBitsWritten} ({splitBitsWritten/8}b)\r\n{diag}");
        }
    }

    public class Bracket
    {
        public int Upper;
        public int Lower;
        public int Split;
        public int SplitCount;

        public Bracket(int lower, int split, int upper)
        {
            Upper = upper;
            Lower = lower;
            Split = split;
        }

        public Bracket(int lower, int upper, byte[] src)
        {
            var histogram = new int[256];
            for (int i = 0; i < src.Length; i++)
            {
                var s = src[i];
                if (s < lower || s > upper) continue;
                histogram[src[i]]++;
            }
            
            int modeIdx = 0;
            //int minV = int.MaxValue;
            //int maxV = int.MinValue;
            //int minIdx = -1;
            //int maxIdx = -1;
            int count = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                var v = histogram[i];
                if (v <= 0) continue;

                //if (v < minV) { minV = v; minIdx = i; }
                //if (v > maxV) { maxV = v; maxIdx = i; }

                if (i == lower || i == upper || v <= count) continue;
                modeIdx = i;
                count = v;
            }
            Lower = lower;
            Upper = upper;
            Split = modeIdx;
            SplitCount = count;
        }

        public override string ToString()
        {
            return $"[{Lower} | {Split} ({SplitCount}) | {Upper}]";
        }

        public bool Excludes(int value)
        {
            return value < Lower || value > Upper;
        }

        public bool Empty()
        {
            return Lower >= Upper;
        }
    }
}