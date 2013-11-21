using NUnit.Framework;

namespace ImageTools.Tests
{
	[TestFixture]
	[Description("Most of these tests produce images as output." +
				 "You should open these and check that they are satisfactory.")]
	public class DownscalingImages : WithCleanedOutput
	{
		[Test]
		public void dummy_green_test ()
		{
			Assert.Pass();
		}
	}
}
