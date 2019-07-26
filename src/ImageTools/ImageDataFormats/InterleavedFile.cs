using System;
using System.IO;

namespace ImageTools.ImageDataFormats
{
    /// <summary>
    /// A stream container for progressive image files.
    /// </summary>
    public class InterleavedFile {
        public ushort Width { get; }

        public ushort Height { get; }

        public ushort Depth { get; }

        public byte[][] Planes { get; }

        // NOTE: it's better to compress the streams and then interleave, not the other way around

        // Format:
        // [xSize:uint_16], [ySize:uint_16], [zSize:uint_16],
        // [PlaneCount: uint_8] x { [byteSize:uint_64] }
        // [Plane0,byte0:uint_8] ... [PlaneN,byte0], [Plane0,byte1:uint_8]...

        /// <summary>
        /// Create a file from buffers
        /// </summary>
        /// <param name="width">Size in X dimension</param>
        /// <param name="height">Size in Y dimension</param>
        /// <param name="depth">Size in Z dimension. For 2D images, this should be 1</param>
        /// <param name="planes">byte buffers for each image plane</param>
        public InterleavedFile(ushort width, ushort height, ushort depth, params byte[][] planes)
        {
            if (planes == null || planes.Length < 1 || planes.Length > 100) throw new Exception("Must have between 1 and 100 planes");

            Width = width;
            Height = height;
            Depth = depth;
            Planes = planes;
        }

        /// <summary>
        /// Create an empty container for restoring buffers
        /// </summary>
        public InterleavedFile(ushort width, ushort height, ushort depth, int planeCount)
        {
            Width = width;
            Height = height;
            Depth = depth;
            Planes = new byte[planeCount][];
        }

        public void WriteToStream(Stream output)
        {
            WriteStreamHeaders(output);

            // now, spin through each plane IN ORDER, removing it when empty
            // this is a slow byte-wise method. TODO: optimise.
            long i = 0;
            while (true) {
                var anything = false;

                for (int p = 0; p < Planes.Length; p++)
                {
                    if (Planes[p].Length > i) {
                        anything = true;
                        output.WriteByte(Planes[p][i]);
                    }
                }
                i++;

                if (!anything) break; // all planes empty
            }
        }

        private void WriteStreamHeaders(Stream output)
        {
            // All planes must have the same base physical size
            WriteU16(Width, output);
            WriteU16(Height, output);
            WriteU16(Depth, output);

            // Each plane can have a different byte size
            WriteU8(Planes.Length, output);
            for (int i = 0; i < Planes.Length; i++)
            {
                WriteU64(Planes[i].LongLength, output);
            }
        }


        /// <summary>
        /// Read from a source file. If the source is trucated, the recovery will go as far as possible
        /// </summary>
        public static InterleavedFile ReadFromStream(Stream input) {

            // All planes must have the same base physical size
            ReadU16(input, out var width);
            ReadU16(input, out var height);
            ReadU16(input, out var depth);

            // Each plane can have a different byte size
            ReadU8(input, out var planesLength);

            var result = new InterleavedFile(width, height, depth, planesLength);
            long i;

            // allocate the buffers TODO: sanity check
            for (i = 0; i < planesLength; i++)
            {
                ReadU64(input, out var psize);
                result.Planes[i] = new byte[psize];
            }


            // Read into buffer in order. This is the exact inverse of the write
            i = 0;
            while (true) {
                var anything = false;

                for (int p = 0; p < planesLength; p++)
                {
                    if (result.Planes[p].Length > i) {
                        var value = input.ReadByte();
                        if (value < 0) break; // truncated?

                        anything = true;
                        result.Planes[p][i] = (byte)value;
                    }
                }
                i++;

                if (!anything) break; // all planes full
            }

            return result;
        }
        
        private static bool ReadU8(Stream rs, out int value) {
            value = rs.ReadByte();
            return value >= 0;
        }

        private static void WriteU8(int value, Stream ws) {
            ws?.WriteByte((byte)value);
        }
        
        private static bool ReadU16(Stream rs, out ushort value) {
            value = 0;
            var hi = rs.ReadByte();
            var lo = rs.ReadByte();
            if (hi < 0 || lo < 0) return false;

            value = (ushort)((hi << 8) | (lo));
            return true;
        }

        private static void WriteU16(ushort value, Stream ws) {
            byte hi = (byte)((value >> 8) & 0xff);
            byte lo = (byte)((value     ) & 0xff);
            ws?.WriteByte(hi);
            ws?.WriteByte(lo);
        }

        private static bool ReadU64(Stream rs, out ulong value)
        {
            value = 0;
            for (int i = 56; i >= 0; i -= 8)
            {
                var b = (long)rs.ReadByte();
                if (b < 0) return false;
                value |= (ulong)(b << i);
            }
            return true;
        }

        private static void WriteU64(long srcvalue, Stream ws)
        {
            ulong value = (ulong)srcvalue;
            for (int i = 56; i >= 0; i -= 8)
            {
                byte b = (byte)((value >> i) & 0xff);
                ws?.WriteByte(b);
            }
        }
    }
}