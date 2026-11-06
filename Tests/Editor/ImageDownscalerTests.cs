using System;
using NUnit.Framework;

namespace LeanImageLoader.Tests
{
	public sealed class ImageDownscalerTests
	{
		const int MAX_SIZE = 512;
		const int RGBA = 4;
		const int RGB = 3;

		[TestCase(512, 512)]
		[TestCase(300, 200)]
		[TestCase(1, 1)]
		public void CalcFitSize_KeepsImagesThatAlreadyFit(int width, int height)
		{
			Assert.AreEqual((width, height), ImageDownscaler.CalcFitSize(width, height, MAX_SIZE));
		}

		[TestCase(1024, 1024, 512, 512)]
		[TestCase(2000, 1000, 512, 256)]
		[TestCase(1000, 2000, 256, 512)]
		[TestCase(1920, 1080, 512, 288)]
		[TestCase(513, 300, 512, 299)]
		public void CalcFitSize_FitsLongEdgeAndKeepsRatio(int width, int height, int expectedWidth, int expectedHeight)
		{
			Assert.AreEqual((expectedWidth, expectedHeight), ImageDownscaler.CalcFitSize(width, height, MAX_SIZE));
		}

		[Test]
		public void CalcFitSize_ShortEdgeNeverBecomesZero()
		{
			Assert.AreEqual((MAX_SIZE, 1), ImageDownscaler.CalcFitSize(100000, 1, MAX_SIZE));
			Assert.AreEqual((1, MAX_SIZE), ImageDownscaler.CalcFitSize(1, 100000, MAX_SIZE));
		}

		[Test]
		public void CalcFitSize_RejectsInvalidSizes()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => ImageDownscaler.CalcFitSize(0, 10, MAX_SIZE));
			Assert.Throws<ArgumentOutOfRangeException>(() => ImageDownscaler.CalcFitSize(10, 10, 0));
		}

		[Test]
		public void Downscale_AveragesAreaWithRounding()
		{
			byte[] source =
			{
				0, 10, 100, 255,   1, 20, 100, 255,
				2, 30, 100, 0,     3, 41, 101, 0,
			};

			byte[] result = ImageDownscaler.Downscale(source, 2, 2, RGBA, 1, 1);

			// R: 6/4=1.5->2, G: 101/4=25.25->25, B: 401/4=100.25->100, A: 510/4=127.5->128
			CollectionAssert.AreEqual(new byte[] { 2, 25, 100, 128 }, result);
		}

		[Test]
		public void Downscale_PreservesRowOrder()
		{
			const int width = 4;
			const int height = 4;
			var source = new byte[width * height * RGB];

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					source[(y * width + x) * RGB] = y < height / 2 ? (byte)0 : (byte)200;
				}
			}

			byte[] result = ImageDownscaler.Downscale(source, width, height, RGB, 2, 2);

			Assert.AreEqual(0, result[0]);
			Assert.AreEqual(0, result[RGB]);
			Assert.AreEqual(200, result[2 * RGB]);
			Assert.AreEqual(200, result[3 * RGB]);
		}

		[Test]
		public void Downscale_KeepsSolidColorWithUnevenRatio()
		{
			const byte value = 123;
			var source = new byte[7 * 5];
			Array.Fill(source, value);

			byte[] result = ImageDownscaler.Downscale(source, 7, 5, 1, 3, 2);

			Assert.AreEqual(3 * 2, result.Length);
			foreach (var b in result)
			{
				Assert.AreEqual(value, b);
			}
		}

		[Test]
		public void Downscale_RejectsInvalidArguments()
		{
			var source = new byte[2 * 2 * RGBA];

			Assert.Throws<ArgumentOutOfRangeException>(() => ImageDownscaler.Downscale(source, 2, 2, RGBA, 3, 2));
			Assert.Throws<ArgumentOutOfRangeException>(() => ImageDownscaler.Downscale(source, 2, 2, 5, 1, 1));
			Assert.Throws<ArgumentException>(() => ImageDownscaler.Downscale(source, 4, 4, RGBA, 1, 1));
		}

		[Test]
		public void ExpandToColor_WidensGreyAndGreyAlpha()
		{
			CollectionAssert.AreEqual(new byte[] { 7, 7, 7, 9, 9, 9 }, ImageDownscaler.ExpandToColor(new byte[] { 7, 9 }, 1, out int greyBpp));
			Assert.AreEqual(3, greyBpp);

			CollectionAssert.AreEqual(new byte[] { 7, 7, 7, 200 }, ImageDownscaler.ExpandToColor(new byte[] { 7, 200 }, 2, out int greyAlphaBpp));
			Assert.AreEqual(4, greyAlphaBpp);
		}

		[Test]
		public void ExpandToColor_ReturnsColorBuffersAsIs()
		{
			var rgb = new byte[] { 1, 2, 3 };
			Assert.AreSame(rgb, ImageDownscaler.ExpandToColor(rgb, 3, out int bpp));
			Assert.AreEqual(3, bpp);
		}
	}
}
