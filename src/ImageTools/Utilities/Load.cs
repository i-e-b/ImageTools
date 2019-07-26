using System.Drawing;
using System.IO;
using System.Reflection;

namespace ImageTools.Utilities
{
	public static class Load
	{
		public static Bitmap FromFile(string filePath)
		{
            if (!File.Exists(filePath)) {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                filePath = Path.Combine(basePath, filePath);
            }
			using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
			{
				using (var bmp = Image.FromStream(fs))
				{
					return new Bitmap(bmp);
				}
			}
		}

        public static bool FileExists(string filePath)
        {
            if (!File.Exists(filePath)) {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                filePath = Path.Combine(basePath, filePath);
            }
            return File.Exists(filePath);
        }
    }
}