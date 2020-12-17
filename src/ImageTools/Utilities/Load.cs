﻿using System.Drawing;
using System.IO;
using System.Reflection;
using ImageTools.ImageStorageFileFormats;

namespace ImageTools.Utilities
{
	public static class Load
	{
		public static Bitmap FromFile(string filePath)
		{
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            if (!File.Exists(filePath)) {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                filePath = Path.Combine(basePath??"", filePath);
            }
            
            if (WaveletImageFormat.IsWaveletFile(filePath))
            {
	            return WaveletImageFormat.LoadFile(filePath);
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var bmp = Image.FromStream(fs);
            
            return new Bitmap(bmp);
        }

        public static bool FileExists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            if (!File.Exists(filePath)) {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                filePath = Path.Combine(basePath??"", filePath);
            }
            return File.Exists(filePath);
        }
    }
}