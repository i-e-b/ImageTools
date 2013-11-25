using System.Drawing;
using System.IO;

namespace ImageTools
{
	public static class Load
	{
		public static Bitmap FromFile(string filePath)
		{
			using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
			{
				using (var bmp = Image.FromStream(fs))
				{
					return new Bitmap(bmp);
				}
			}
		}
	}
}