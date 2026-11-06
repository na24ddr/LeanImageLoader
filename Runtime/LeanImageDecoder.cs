using System;
using System.IO;
using StbImageSharp;

namespace LeanImageLoader
{
	/// <summary>
	/// Decodes PNG / JPEG / BMP / TGA / PSD / GIF (first frame) / HDR images and downscales them
	/// without ever holding a full-size managed copy of the pixels.
	///
	/// The decoder writes into native memory; the pixels are downscaled straight from there and the
	/// native buffer is freed immediately. Only the downscaled result is allocated on the managed heap,
	/// which matters on IL2CPP/Mono where the managed heap does not shrink once it has grown.
	///
	/// Uses no Unity API, so every method is safe to call from worker threads.
	/// </summary>
	public static class LeanImageDecoder
	{
		/// <summary>Pass as maxSize to keep the original resolution.</summary>
		public const int NO_LIMIT = int.MaxValue;

		/// <summary>
		/// Decodes an image file and downscales it so the long edge fits <paramref name="maxSize"/>.
		/// </summary>
		/// <param name="path">Image file path.</param>
		/// <param name="maxSize">Upper bound for the long edge, or <see cref="NO_LIMIT"/>.</param>
		/// <returns>Decoded pixels (RGB or RGBA, bottom row first).</returns>
		/// <exception cref="IOException">The file could not be read.</exception>
		/// <exception cref="InvalidDataException">The data is not a supported image.</exception>
		public static DecodedImage Decode(string path, int maxSize)
		{
			using (var stream = File.OpenRead(path))
			{
				return Decode(stream, maxSize);
			}
		}

		/// <summary>
		/// Decodes an encoded image in memory and downscales it so the long edge fits <paramref name="maxSize"/>.
		/// </summary>
		/// <param name="data">Encoded image bytes.</param>
		/// <param name="maxSize">Upper bound for the long edge, or <see cref="NO_LIMIT"/>.</param>
		/// <returns>Decoded pixels (RGB or RGBA, bottom row first).</returns>
		/// <exception cref="InvalidDataException">The data is not a supported image.</exception>
		public static DecodedImage Decode(byte[] data, int maxSize)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			using (var stream = new MemoryStream(data, false))
			{
				return Decode(stream, maxSize);
			}
		}

		/// <summary>
		/// Decodes an encoded image from a stream and downscales it so the long edge fits <paramref name="maxSize"/>.
		/// </summary>
		/// <param name="stream">Encoded image.</param>
		/// <param name="maxSize">Upper bound for the long edge, or <see cref="NO_LIMIT"/>.</param>
		/// <returns>Decoded pixels (RGB or RGBA, bottom row first).</returns>
		/// <exception cref="InvalidDataException">The data is not a supported image.</exception>
		public static unsafe DecodedImage Decode(Stream stream, int maxSize)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			if (maxSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be at least 1.");
			}

			try
			{
				// Unity textures store the bottom row first.
				StbImage.stbi_set_flip_vertically_on_load(1);

				return StbImage.LoadWithNativePixels<DecodedImage>(stream, ColorComponents.Default,
					(pixels, width, height, comp) =>
					{
						var source = new ReadOnlySpan<byte>(pixels, width * height * comp);
						var (fitWidth, fitHeight) = ImageDownscaler.CalcFitSize(width, height, maxSize);

						// Downscale first (cheap at the source channel count), then widen grey to color.
						byte[] data = fitWidth == width && fitHeight == height ?
							source.ToArray() :
							ImageDownscaler.Downscale(source, width, height, comp, fitWidth, fitHeight);

						data = ImageDownscaler.ExpandToColor(data, comp, out int bytesPerPixel);

						return new DecodedImage(fitWidth, fitHeight, bytesPerPixel, data);
					});
			}
			catch (InvalidOperationException ex)
			{
				// stb reports unsupported or corrupt data this way.
				throw new InvalidDataException($"Failed to decode image: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Same as <see cref="Decode(string, int)"/> but returns false instead of throwing when the file
		/// is missing, unreadable or not a supported image.
		/// </summary>
		/// <param name="path">Image file path.</param>
		/// <param name="maxSize">Upper bound for the long edge, or <see cref="NO_LIMIT"/>.</param>
		/// <param name="image">Decoded pixels, or null on failure.</param>
		/// <returns>Whether decoding succeeded.</returns>
		public static bool TryDecode(string path, int maxSize, out DecodedImage image)
		{
			try
			{
				image = Decode(path, maxSize);
				return true;
			}
			catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
			{
				image = null;
				return false;
			}
		}
	}
}
