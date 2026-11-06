using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeanImageLoader.Tests
{
	public sealed class LeanTextureLoaderTests
	{
		static readonly Color32 RED = new Color32(255, 0, 0, 255);
		static readonly Color32 BLUE = new Color32(0, 0, 255, 255);

		[Test]
		public void LoadDownscaled_ShrinksLargeImage()
		{
			byte[] png = TestImages.TwoBandPng(64, 32, RED, BLUE);

			Texture2D texture = LeanTextureLoader.LoadDownscaled(png, 16);

			try
			{
				Assert.AreEqual(16, texture.width);
				Assert.AreEqual(8, texture.height);
				Assert.IsFalse(texture.isReadable);
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void LoadDownscaled_KeepsSmallImageSize()
		{
			byte[] png = TestImages.TwoBandPng(10, 6, RED, BLUE);

			Texture2D texture = LeanTextureLoader.LoadDownscaled(png, 16);

			try
			{
				Assert.AreEqual(10, texture.width);
				Assert.AreEqual(6, texture.height);
				Assert.IsFalse(texture.isReadable);
			}
			finally
			{
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void LoadDownscaled_ThrowsInvalidDataForGarbage()
		{
			// Unity logs an error when LoadImage fails; that is expected here.
			LogAssert.ignoreFailingMessages = true;

			Assert.Throws<InvalidDataException>(() => LeanTextureLoader.LoadDownscaled(new byte[] { 1, 2, 3, 4, 5 }, 16));
		}
	}
}
