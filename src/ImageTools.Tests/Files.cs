using System;
using System.IO;
using NUnit.Framework;

namespace ImageTools.Tests
{
	[SetUpFixture]
	public class WithCleanedOutput
	{
		[OneTimeSetUp]
		public void setup()
		{
			Console.WriteLine("Deleting old output");
			Files.DeleteAllOutputs();
		}
	}

	public class Files
	{
		public const string OutputDirectory = "./outputs/";
		public static void DeleteAllOutputs()
		{
			try
			{
				foreach (var path in Directory.GetFiles(OutputDirectory, "*", SearchOption.AllDirectories))
				{
					try
					{
						File.Delete(path);
					}
					catch
					{
						Ignore();
					}
				}
			} catch
			{
				Ignore();
			}
		}

		static void Ignore() {}
	}
}