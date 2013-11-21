using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace ImageTools
{
	public static class Save
	{
		public static void SaveJpeg(this Bitmap src, string filePath, int quality = 95)
		{
			var p = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(p))
			{
				Directory.CreateDirectory(p);
			}
			using (var fs = new FileStream(filePath, FileMode.Create))
			{
				src.JpegStream(fs);
				fs.Close();
			}
		}

		public static void JpegStream(this Bitmap src, Stream outputStream, int quality = 95)
		{
			var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
			var parameters = new EncoderParameters(1);
			parameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

			src.Save(outputStream, encoder, parameters);
			outputStream.Flush();
		}
	}
}