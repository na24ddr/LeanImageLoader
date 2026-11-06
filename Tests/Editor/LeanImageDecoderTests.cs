using System.IO;
using NUnit.Framework;
using StbImageSharp;
using UnityEngine;

namespace LeanImageLoader.Tests
{
	public sealed class LeanImageDecoderTests
	{
		static readonly Color32 RED = new Color32(255, 0, 0, 255);
		static readonly Color32 BLUE = new Color32(0, 0, 255, 255);

		[Test]
		public void Decode_StoresBottomRowFirstLikeUnity()
		{
			byte[] png = TestImages.TwoBandPng(4, 2, RED, BLUE);

			DecodedImage image = LeanImageDecoder.Decode(png, LeanImageDecoder.NO_LIMIT);

			Assert.AreEqual(4, image.Width);
			Assert.AreEqual(2, image.Height);
			Assert.AreEqual(TextureFormat.RGBA32, image.Format);
			// First pixel = bottom-left = red, last pixel = top-right = blue.
			CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, Slice(image.Pixels, 0, 4));
			CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, Slice(image.Pixels, image.Pixels.Length - 4, 4));
		}

		[Test]
		public void Decode_DownscalesToMaxSizeKeepingRatio()
		{
			byte[] png = TestImages.TwoBandPng(64, 32, RED, BLUE);

			DecodedImage image = LeanImageDecoder.Decode(png, 16);

			Assert.AreEqual(16, image.Width);
			Assert.AreEqual(8, image.Height);
			Assert.AreEqual(16 * 8 * 4, image.Pixels.Length);
			CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, Slice(image.Pixels, 0, 4));
			CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, Slice(image.Pixels, image.Pixels.Length - 4, 4));
		}

		[Test]
		public void Decode_MatchesUpstreamDecodeThenDownscale()
		{
			byte[] png = TestImages.TwoBandPng(50, 30, RED, BLUE);

			DecodedImage image = LeanImageDecoder.Decode(png, 20);

			StbImage.stbi_set_flip_vertically_on_load(1);
			ImageResult full = ImageResult.FromMemory(png);
			var (width, height) = ImageDownscaler.CalcFitSize(full.Width, full.Height, 20);
			byte[] expected = ImageDownscaler.Downscale(full.Data, full.Width, full.Height, (int)full.Comp, width, height);

			Assert.AreEqual(width, image.Width);
			Assert.AreEqual(height, image.Height);
			CollectionAssert.AreEqual(expected, image.Pixels);
		}

		[Test]
		public void Decode_ExpandsGreyscaleToRgb()
		{
			byte[] png = TestImages.GreyPng(3, 2, 77);

			DecodedImage image = LeanImageDecoder.Decode(png, LeanImageDecoder.NO_LIMIT);

			Assert.AreEqual(TextureFormat.RGB24, image.Format);
			Assert.AreEqual(3 * 2 * 3, image.Pixels.Length);
			foreach (byte b in image.Pixels)
			{
				Assert.AreEqual(77, b);
			}
		}

		[Test]
		public void Decode_ThrowsInvalidDataForGarbage()
		{
			Assert.Throws<InvalidDataException>(() => LeanImageDecoder.Decode(new byte[] { 1, 2, 3, 4, 5 }, 16));
		}

		[Test]
		public void TryDecode_ReturnsFalseForMissingFile()
		{
			string path = Path.Combine(Path.GetTempPath(), "LeanImageLoader_missing_" + System.Guid.NewGuid().ToString("N") + ".png");

			Assert.IsFalse(LeanImageDecoder.TryDecode(path, 16, out DecodedImage image));
			Assert.IsNull(image);
		}

		[Test]
		public void ToTexture2D_CreatesNonReadableTextureOfSameSize()
		{
			byte[] png = TestImages.TwoBandPng(8, 4, RED, BLUE);
			DecodedImage image = LeanImageDecoder.Decode(png, LeanImageDecoder.NO_LIMIT);

			Texture2D texture = image.ToTexture2D();

			try
			{
				Assert.AreEqual(8, texture.width);
				Assert.AreEqual(4, texture.height);
				Assert.AreEqual(TextureFormat.RGBA32, texture.format);
				Assert.IsFalse(texture.isReadable);
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}
		}

		static byte[] Slice(byte[] source, int start, int length)
		{
			var result = new byte[length];
			System.Array.Copy(source, start, result, 0, length);
			return result;
		}
	}
}
