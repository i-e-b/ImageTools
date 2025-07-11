using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

namespace ImageTools.Utilities
{
	public static class Save
	{
        public static string ToPath(string filePath) {
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(basePath, filePath);
        }

		public static void SaveJpeg(this Bitmap src, string filePath, int quality = 95)
		{
            filePath = ToPath(filePath);

			var p = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(p))
			{
				Directory.CreateDirectory(p);
			}
			using (var fs = new FileStream(filePath, FileMode.Create))
			{
				src.JpegStream(fs, quality);
				fs.Close();
			}
		}
        
        public static void SaveBmp(this Bitmap src, string filePath)
        {
            filePath = ToPath(filePath);

            var p = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(p))
            {
                Directory.CreateDirectory(p);
            }
            if (File.Exists(filePath)) File.Delete(filePath);
            src.Save(filePath, ImageFormat.Bmp);
        }

		public static void JpegStream(this Bitmap src, Stream outputStream, int quality = 95)
		{
			var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
			var parameters = new EncoderParameters(1);
			parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

			src.Save(outputStream, encoder, parameters);
			outputStream.Flush();
		}

		public static void SaveToPath(this byte[] bytes, string filePath)
		{
			filePath = ToPath(filePath);

			var p = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(p))
			{
				Directory.CreateDirectory(p);
			}
			if (File.Exists(filePath)) File.Delete(filePath);
			File.WriteAllBytes(filePath, bytes);
		}
	}
}