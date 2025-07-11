using ImageTools.ImageDataFormats;
// ReSharper disable InconsistentNaming

namespace ImageTools.ImageStorageFileFormats
{
    /// <summary>
    /// A stream container for progressive image files.
    /// </summary>
    public class VersionedInterleavedFile: InterleavedFile {
        public const string MagicMarker = "Wfimage";
        
        /// <summary>
        /// File version. There is limited support for older versions.
        /// This shouldn't ever go above 32.
        /// </summary>
        public const ushort CurrentVersion = 2;
        
        /// <summary>
        /// File version used
        /// </summary>
        public ushort Version { get; protected set; }

        /// <summary>
        /// Quantiser settings for non-color planes used to create the image.
        /// </summary>
        public double[] QuantiserSettings_Y { get; set; }

        /// <summary>
        /// Quantiser settings for color planes used to create the image.
        /// </summary>
        public double[] QuantiserSettings_C { get; set; }

        // Old quantiser settings from before they were stored with the image
        public static readonly double[] OldYQuants = { 15, 8, 5, 3, 2.0};
        public static readonly double[] OldCQuants = { 20, 10, 4.0 };

        // NOTE: not sure if it's better to compress-then-interleave or the other way around

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
        /// <param name="yQuants">Quantiser settings for non-color planes</param>
        /// <param name="cQuants">Quantiser settings for color planes</param>
        public VersionedInterleavedFile(ushort width, ushort height, ushort depth, double[] yQuants, double[] cQuants, params byte[][] planes)
            : base(width, height, depth, planes)
        {
            if (planes == null || planes.Length < 1 || planes.Length > 100) throw new Exception("Must have between 1 and 100 planes");

            QuantiserSettings_Y = yQuants;
            QuantiserSettings_C = cQuants;
        }

        /// <summary>
        /// Create an empty container for restoring buffers
        /// </summary>
        public VersionedInterleavedFile(ushort width, ushort height, ushort depth, int planeCount) : base(width, height, depth, planeCount) { }

        public override void WriteToStream(Stream output)
        {
            if (output == null) throw new Exception("Invalid output");
            WriteStreamHeaders(output);

            // now, spin through each plane IN ORDER, removing it when empty
            long i = 0;
            while (true) {
                var anything = false;

                if (Planes == null) throw new Exception("Invalid image");
                foreach (var t in Planes)
                {
                    if (t?.Length > i) {
                        anything = true;
                        output.WriteByte(t[i]);
                    }
                }
                i++;

                if (!anything) break; // all planes empty
            }
        }

        private void WriteStreamHeaders(Stream output)
        {
            foreach (var c in MagicMarker) WriteU8(c, output);
            WriteU16(CurrentVersion, output);

            // All planes must have the same base physical size
            WriteU16(Width, output);
            WriteU16(Height, output);
            WriteU16(Depth, output);

            WriteQuantiserSettings(output, QuantiserSettings_Y, QuantiserSettings_C);

            // Each plane can have a different byte size
            WriteU8(Planes.Length, output);
            for (int i = 0; i < Planes.Length; i++)
            {
                if (Planes[i] == null) throw new Exception("Invalid planar data when writing file stream headers");
                WriteU64(Planes[i].LongLength, output);
            }
        }

        public static bool IsValidFile(Stream input)
        {
            foreach (var c in MagicMarker)
            {
                if (!ReadU8(input, out var v) || v != c) return false;
            }
            if (!ReadU16(input, out var version)) return false;
            return version < 32;
        }

        /// <summary>
        /// Read from a source file. If the source is truncated, the recovery will go as far as possible
        /// </summary>
        public static VersionedInterleavedFile ReadFromVersionedStream(Stream input) {
            foreach (var c in MagicMarker)
            {
                if (!ReadU8(input, out var v) || v != c) throw new Exception("Not a valid WFI file");
            }

            ReadU16(input, out var version);

            if (version > 32) { // probably a version 1 file
                input.Seek(0, SeekOrigin.Begin);
                input.Position = 0;
                version = 1;
            }


            // All planes must have the same base physical size
            ReadU16(input, out var width);
            ReadU16(input, out var height);
            ReadU16(input, out var depth);

            // Read quantiser settings if we expect them
            var yquant = new List<double>();
            var cquant = new List<double>();
            if (version >= 2)
            {   // we should have quantiser information
                ReadQuantiserSettings(input, yquant, cquant);
            }
            if (yquant.Count < 1) yquant.AddRange(OldYQuants);
            if (cquant.Count < 1) cquant.AddRange(OldCQuants);

            // Each plane can have a different byte size
            ReadU8(input, out var planeCount);

            if (planeCount < 1 || planeCount > 6) planeCount = 3;//throw new Exception($"Plane count does not make sense (expected 1..6, got {planeCount})");
            var result = new VersionedInterleavedFile(width, height, depth, planeCount);
            if (result.Planes == null) throw new Exception("Interleaved file did not have a planes container");

            result.Version = version;
            result.QuantiserSettings_Y = yquant.ToArray();
            result.QuantiserSettings_C = cquant.ToArray();

            // allocate the buffers
            long i;
            for (i = 0; i < planeCount; i++)
            {
                var ok = ReadU64(input, out var psize);
                if (!ok || psize > 10_000_000) return result;//throw new Exception("Plane data was outside of expected bounds (this is a safety check)");
                result.Planes[i] = new byte[psize];
            }


            // Read into buffer in order. This is the exact inverse of the write
            i = 0;
            while (true) {
                var anything = false;

                for (int p = 0; p < planeCount; p++)
                {
                    if (result.Planes[p] == null) throw new Exception("Invalid planar data buffer when reading planar data");

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

        
        private static void WriteQuantiserSettings(Stream output, double[] yquant, double[] cquant)
        {
            if (yquant==null || cquant == null) {
                WriteU8(0, output);
                WriteU8(0, output);
                return;
            }
            WriteU8(yquant.Length, output);
            WriteU8(cquant.Length, output);

            for (int q = 0; q < yquant.Length; q++)
            {
                var qs = yquant[q] * 100.0;
                WriteU16((ushort)qs, output);
            }

            for (int q = 0; q < cquant.Length; q++)
            {
                var qs = cquant[q] * 100.0;
                WriteU16((ushort)qs, output);
            }
        }

        private static void ReadQuantiserSettings(Stream input, List<double> yquant, List<double> cquant)
        {
            var ok = ReadU8(input, out var yqCount);
            ok &= ReadU8(input, out var cqCount);

            if (!ok || yquant == null || cquant == null) return;

            for (int q = 0; q < yqCount; q++)
            {
                if (ReadU16(input, out var qs))
                {
                    yquant.Add(qs / 100.0);
                }
            }

            for (int q = 0; q < cqCount; q++)
            {
                if (ReadU16(input, out var qs))
                {
                    cquant.Add(qs / 100.0);
                }
            }
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