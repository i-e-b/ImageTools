using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageTools.GeneralTypes;

namespace ImageTools
{
    /// <summary>
    /// Reads image headers to extract basic information without decoding the entire file
    /// </summary>
    /// <remarks>Based on https://stackoverflow.com/a/112711</remarks>
    public static class ImageHeaders
    {
        private const string ErrorMessage = "Did not recognize image format";

        private static readonly Dictionary<byte[], Func<BinaryReader, Size2D>> _imageFormatDecoders = new Dictionary<byte[], Func<BinaryReader, Size2D>>()
        {
            {new byte[] {0x42, 0x4D}, DecodeBitmap},
            {new byte[] {0x47, 0x49, 0x46, 0x38, 0x37, 0x61}, DecodeGif},
            {new byte[] {0x47, 0x49, 0x46, 0x38, 0x39, 0x61}, DecodeGif},
            {new byte[] {0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A}, DecodePng},
            {new byte[] {0xff, 0xd8}, DecodeJfif},
            { new byte[] { 0x52, 0x49, 0x46, 0x46 }, DecodeWebP } // works only for 'lossy' format
        };

        /// <summary>
        /// Gets the dimensions of an image.
        /// </summary>
        /// <param name="path">The path of the image to get the dimensions of.</param>
        /// <returns>The dimensions of the specified image.</returns>
        /// <exception cref="ArgumentException">The image was of an unrecognized format.</exception>
        public static Size2D GetDimensions(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return new Size2D(0, 0);
            using var binaryReader = new BinaryReader(File.OpenRead(path));
            try
            {
                return GetDimensions(binaryReader);
            }
            catch (ArgumentException e)
            {
                if (e.Message?.StartsWith(ErrorMessage) == true) throw new ArgumentException(ErrorMessage, nameof(path), e);
                throw;
            }
        }

        /// <summary>
        /// Gets the dimensions of an image.
        /// </summary>
        /// <param name="binaryReader">The raw image data</param>
        /// <returns>The dimensions of the specified image.</returns>
        /// <exception cref="ArgumentException">The image was of an unrecognized format.</exception>    
        public static Size2D GetDimensions(BinaryReader binaryReader)
        {
            if (binaryReader == null) return new Size2D(0, 0);
            var maxMagicBytesLength = _imageFormatDecoders?.Keys.OrderByDescending(x => x!.Length).FirstOrDefault()?.Length;
            if (maxMagicBytesLength == null) return new Size2D(0, 0);

            var len = maxMagicBytesLength.Value;
            var magicBytes = new byte[len];

            for (var i = 0; i < len; i += 1)
            {
                magicBytes[i] = binaryReader.ReadByte();

                foreach (var kvPair in _imageFormatDecoders.Where(kvPair => magicBytes.StartsWith(kvPair.Key)))
                {
                    return kvPair.Value!(binaryReader);
                }
            }

            throw new ArgumentException(ErrorMessage, nameof(binaryReader));
        }

        private static bool StartsWith(this IReadOnlyList<byte> thisBytes, IReadOnlyList<byte> thatBytes)
        {
            if (thisBytes == null) return false;
            if (thatBytes == null) return false;
            for (var i = 0; i < thatBytes.Count; i += 1)
            {
                if (thisBytes[i] != thatBytes[i]) return false;
            }

            return true;
        }

        private static short ReadLittleEndianInt16(this BinaryReader binaryReader)
        {
            var bytes = new byte[sizeof(short)];
            for (var i = 0; i < sizeof(short); i += 1)
            {
                bytes[sizeof(short) - 1 - i] = binaryReader!.ReadByte();
            }

            return BitConverter.ToInt16(bytes, 0);
        }

        private static int ReadLittleEndianInt32(this BinaryReader binaryReader)
        {
            var bytes = new byte[sizeof(int)];
            for (var i = 0; i < sizeof(int); i += 1)
            {
                bytes[sizeof(int) - 1 - i] = binaryReader!.ReadByte();
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        private static Size2D DecodeBitmap(BinaryReader binaryReader)
        {
            binaryReader!.ReadBytes(16);
            var width = binaryReader.ReadInt32();
            var height = binaryReader.ReadInt32();
            return new Size2D(width, height);
        }

        private static Size2D DecodeGif(BinaryReader binaryReader)
        {
            int width = binaryReader!.ReadInt16();
            int height = binaryReader.ReadInt16();
            return new Size2D(width, height);
        }

        private static Size2D DecodePng(BinaryReader binaryReader)
        {
            binaryReader!.ReadBytes(8);
            var width = binaryReader.ReadLittleEndianInt32();
            var height = binaryReader.ReadLittleEndianInt32();
            return new Size2D(width, height);
        }

        private static Size2D DecodeJfif(BinaryReader binaryReader)
        {
            while (binaryReader!.ReadByte() == 0xff)
            {
                var marker = binaryReader.ReadByte();
                var chunkLength = binaryReader.ReadLittleEndianInt16();

                if (marker == 0xc0)
                {
                    binaryReader.ReadByte();

                    int height = binaryReader.ReadLittleEndianInt16();
                    int width = binaryReader.ReadLittleEndianInt16();
                    return new Size2D(width, height);
                }

                binaryReader.ReadBytes(chunkLength - 2);
            }

            throw new ArgumentException(ErrorMessage);
        }
        
        private static Size2D DecodeWebP(BinaryReader binaryReader)
        {
            binaryReader!.ReadUInt32(); // Size
            binaryReader.ReadBytes(15); // WEBP, VP8 + more
            binaryReader.ReadBytes(3); // SYNC

            var width = binaryReader.ReadUInt16() & 0b00_11111111111111; // 14 bits width
            var height = binaryReader.ReadUInt16() & 0b00_11111111111111; // 14 bits height

            return new Size2D(width, height);
        }
    }
}