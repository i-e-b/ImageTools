using System.Drawing;

namespace ImageTools
{
	public static class Load
	{
		public static Bitmap FromFile(string filePath)
		{
			return (Bitmap)Bitmap.FromFile(filePath);
		}
	}
}