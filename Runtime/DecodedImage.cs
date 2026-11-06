using System;
using UnityEngine;

namespace LeanImageLoader
{
	/// <summary>
	/// Decoded, already downscaled pixels in RGB24 or RGBA32 layout.
	/// Rows are stored bottom-up, matching Unity's texture layout.
	///
	/// Creating an instance does not touch Unity APIs, so it can be produced on a worker thread and
	/// turned into a texture later with <see cref="ToTexture2D"/> on the main thread.
	/// </summary>
	public sealed class DecodedImage
	{
		/// <summary>Width in pixels.</summary>
		public int Width { get; }

		/// <summary>Height in pixels.</summary>
		public int Height { get; }

		/// <summary>Bytes per pixel: 3 (RGB) or 4 (RGBA).</summary>
		public int BytesPerPixel { get; }

		/// <summary>Tightly packed pixel rows, bottom row first.</summary>
		public byte[] Pixels { get; }

		/// <summary>Texture format that matches <see cref="Pixels"/>.</summary>
		public TextureFormat Format => BytesPerPixel == 4 ? TextureFormat.RGBA32 : TextureFormat.RGB24;

		/// <summary>
		/// Wraps a pixel buffer.
		/// </summary>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		/// <param name="bytesPerPixel">3 (RGB) or 4 (RGBA).</param>
		/// <param name="pixels">Tightly packed pixel rows, bottom row first.</param>
		public DecodedImage(int width, int height, int bytesPerPixel, byte[] pixels)
		{
			if (width <= 0 || height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(width), "Image size must be at least 1.");
			}

			if (bytesPerPixel != 3 && bytesPerPixel != 4)
			{
				throw new ArgumentOutOfRangeException(nameof(bytesPerPixel), "bytesPerPixel must be 3 or 4.");
			}

			if (pixels == null)
			{
				throw new ArgumentNullException(nameof(pixels));
			}

			if (pixels.Length != width * height * bytesPerPixel)
			{
				throw new ArgumentException("pixels.Length must equal width * height * bytesPerPixel.", nameof(pixels));
			}

			Width = width;
			Height = height;
			BytesPerPixel = bytesPerPixel;
			Pixels = pixels;
		}

		/// <summary>
		/// Uploads the pixels to a new texture. Must be called on the main thread.
		/// The texture keeps no CPU-side copy (it is not readable), so only GPU memory is used.
		/// </summary>
		/// <param name="linear">Whether the texture holds linear (non-color) data.</param>
		/// <returns>A new texture. The caller owns it and should Destroy it when done.</returns>
		public Texture2D ToTexture2D(bool linear = false)
		{
			var texture = new Texture2D(Width, Height, Format, false, linear)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
			};

			texture.LoadRawTextureData(Pixels);
			texture.Apply(false, true);
			return texture;
		}
	}
}
