using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LeanImageLoader
{
	/// <summary>
	/// Loads one image straight into a downscaled, non-readable <see cref="Texture2D"/> on the main thread.
	///
	/// Decoding uses Unity's native <c>ImageConversion.LoadImage</c> (PNG / JPEG only), which is fast for
	/// one-off loads. To load many images without stalling the main thread, use
	/// <see cref="BatchImageLoader"/> instead.
	/// </summary>
	public static class LeanTextureLoader
	{
		const int RGB_BYTES_PER_PIXEL = 3;
		const int RGBA_BYTES_PER_PIXEL = 4;

		/// <summary>
		/// Loads a PNG / JPEG file and downscales it so the long edge fits <paramref name="maxSize"/>.
		/// Must be called on the main thread.
		/// </summary>
		/// <param name="path">Image file path.</param>
		/// <param name="maxSize">Upper bound for the long edge.</param>
		/// <param name="linear">Whether the texture holds linear (non-color) data.</param>
		/// <returns>A non-readable texture. The caller owns it and should Destroy it when done.</returns>
		/// <exception cref="IOException">The file could not be read.</exception>
		/// <exception cref="InvalidDataException">The data is not a PNG / JPEG image.</exception>
		public static Texture2D LoadDownscaled(string path, int maxSize, bool linear = false)
		{
			return LoadDownscaled(File.ReadAllBytes(path), maxSize, linear);
		}

		/// <summary>
		/// Loads PNG / JPEG bytes and downscales them so the long edge fits <paramref name="maxSize"/>.
		/// Must be called on the main thread.
		/// </summary>
		/// <param name="data">Encoded PNG / JPEG bytes.</param>
		/// <param name="maxSize">Upper bound for the long edge.</param>
		/// <param name="linear">Whether the texture holds linear (non-color) data.</param>
		/// <returns>A non-readable texture. The caller owns it and should Destroy it when done.</returns>
		/// <exception cref="InvalidDataException">The data is not a PNG / JPEG image.</exception>
		public static Texture2D LoadDownscaled(byte[] data, int maxSize, bool linear = false)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			if (maxSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be at least 1.");
			}

			var texture = new Texture2D(1, 1, TextureFormat.RGB24, false, linear)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
			};

			// Keep it readable for now: the pixels are read back when downscaling.
			if (!texture.LoadImage(data, false))
			{
				ObjectDisposal.Destroy(texture);
				throw new InvalidDataException("Failed to decode image: not a PNG / JPEG.");
			}

			var (width, height) = ImageDownscaler.CalcFitSize(texture.width, texture.height, maxSize);

			if (width == texture.width && height == texture.height)
			{
				// Already small enough. Just drop the CPU-side copy.
				texture.Apply(false, true);
				return texture;
			}

			byte[] pixels;
			TextureFormat format;

			try
			{
				if (texture.format == TextureFormat.RGB24 || texture.format == TextureFormat.RGBA32)
				{
					// JPEG loads as RGB24 and PNG as RGBA32. Reading the native buffer directly avoids
					// allocating a full-size managed array.
					format = texture.format;
					int bytesPerPixel = format == TextureFormat.RGB24 ? RGB_BYTES_PER_PIXEL : RGBA_BYTES_PER_PIXEL;
					var raw = texture.GetRawTextureData<byte>();
					pixels = ImageDownscaler.Downscale(raw.AsReadOnlySpan(), texture.width, texture.height, bytesPerPixel, width, height);
				}
				else
				{
					// Other layouts (e.g. 16-bit PNG) are normalized to RGBA32.
					format = TextureFormat.RGBA32;
					Color32[] colors = texture.GetPixels32();
					pixels = ImageDownscaler.Downscale(MemoryMarshal.AsBytes(colors.AsSpan()), texture.width, texture.height, RGBA_BYTES_PER_PIXEL, width, height);
				}
			}
			finally
			{
				ObjectDisposal.Destroy(texture);
			}

			return new DecodedImage(width, height, format == TextureFormat.RGB24 ? RGB_BYTES_PER_PIXEL : RGBA_BYTES_PER_PIXEL, pixels)
				.ToTexture2D(linear);
		}
	}
}
