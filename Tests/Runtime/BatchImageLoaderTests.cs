using System;
using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeanImageLoader.Tests
{
	public sealed class BatchImageLoaderTests
	{
		string _directory;

		[SetUp]
		public void SetUp()
		{
			_directory = Path.Combine(Application.temporaryCachePath, "LeanImageLoaderTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_directory);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(_directory))
			{
				Directory.Delete(_directory, true);
			}
		}

		[UnityTest]
		public IEnumerator LoadAsync_KeepsOrderAndReturnsNullForFailures()
		{
			string big = WritePng("big.png", 64, 32);
			string small = WritePng("small.png", 8, 8);
			string missing = Path.Combine(_directory, "missing.png");
			string broken = Path.Combine(_directory, "broken.png");
			File.WriteAllBytes(broken, new byte[] { 1, 2, 3 });

			var paths = new[] { big, null, missing, small, broken };
			int lastFinished = -1;
			int lastTotal = -1;

			var options = new BatchLoadOptions { MaxSize = 16, MaxWorkers = 2, MaxPending = 2 };
			var awaiter = BatchImageLoader.LoadAsync(paths, options, (finished, total) =>
			{
				lastFinished = finished;
				lastTotal = total;
			}).GetAwaiter();

			while (!awaiter.IsCompleted)
			{
				yield return null;
			}

			Texture2D[] textures = awaiter.GetResult();

			try
			{
				Assert.AreEqual(paths.Length, textures.Length);
				Assert.AreEqual(16, textures[0].width);
				Assert.AreEqual(8, textures[0].height);
				Assert.IsNull(textures[1]);
				Assert.IsNull(textures[2]);
				Assert.AreEqual(8, textures[3].width);
				Assert.AreEqual(8, textures[3].height);
				Assert.IsNull(textures[4]);
				Assert.AreEqual(paths.Length, lastFinished);
				Assert.AreEqual(paths.Length, lastTotal);
			}
			finally
			{
				foreach (var texture in textures)
				{
					UnityEngine.Object.Destroy(texture);
				}
			}
		}

		[UnityTest]
		public IEnumerator LoadAsync_LoadsManyImagesWithSmallPendingLimit()
		{
			const int count = 40;
			var paths = new string[count];

			for (int i = 0; i < count; i++)
			{
				paths[i] = WritePng($"image{i}.png", 32 + i, 20);
			}

			var awaiter = BatchImageLoader.LoadAsync(paths, new BatchLoadOptions { MaxSize = 24, MaxPending = 1 }).GetAwaiter();

			while (!awaiter.IsCompleted)
			{
				yield return null;
			}

			Texture2D[] textures = awaiter.GetResult();

			try
			{
				for (int i = 0; i < count; i++)
				{
					Assert.IsNotNull(textures[i], $"index {i}");
					Assert.AreEqual(ImageDownscaler.CalcFitSize(32 + i, 20, 24), (textures[i].width, textures[i].height), $"index {i}");
				}
			}
			finally
			{
				foreach (var texture in textures)
				{
					UnityEngine.Object.Destroy(texture);
				}
			}
		}

		[UnityTest]
		public IEnumerator LoadAsync_ThrowsWhenCancelled()
		{
			var paths = new[] { WritePng("a.png", 8, 8) };
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			var awaiter = BatchImageLoader.LoadAsync(paths, null, null, cancellation.Token).GetAwaiter();

			while (!awaiter.IsCompleted)
			{
				yield return null;
			}

			Assert.Throws<OperationCanceledException>(() => awaiter.GetResult());
		}

		string WritePng(string name, int width, int height)
		{
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

			try
			{
				var colors = new Color32[width * height];
				for (int i = 0; i < colors.Length; i++)
				{
					colors[i] = new Color32((byte)i, 128, 64, 255);
				}

				texture.SetPixels32(colors);
				texture.Apply();

				string path = Path.Combine(_directory, name);
				File.WriteAllBytes(path, texture.EncodeToPNG());
				return path;
			}
			finally
			{
				UnityEngine.Object.Destroy(texture);
			}
		}
	}
}
