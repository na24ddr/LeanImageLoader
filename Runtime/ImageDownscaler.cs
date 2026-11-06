using System;

namespace LeanImageLoader
{
	/// <summary>
	/// Downscales tightly packed 8-bit pixel buffers with an area-average (box) filter.
	///
	/// Uses no Unity API, so it is safe to call from worker threads.
	/// Row order (top-down or bottom-up) is preserved.
	/// </summary>
	public static class ImageDownscaler
	{
		/// <summary>Largest supported number of bytes per pixel (RGBA).</summary>
		public const int MAX_BYTES_PER_PIXEL = 4;

		/// <summary>
		/// Returns the size that fits within <paramref name="maxSize"/> on the long edge while keeping
		/// the aspect ratio. Images that already fit keep their size (they are never enlarged).
		/// </summary>
		/// <param name="width">Source width.</param>
		/// <param name="height">Source height.</param>
		/// <param name="maxSize">Upper bound for the long edge.</param>
		/// <returns>The fitted (Width, Height). Neither edge becomes smaller than 1.</returns>
		public static (int Width, int Height) CalcFitSize(int width, int height, int maxSize)
		{
			if (width <= 0 || height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(width), "Image size must be at least 1.");
			}

			if (maxSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be at least 1.");
			}

			if (width <= maxSize && height <= maxSize)
			{
				return (width, height);
			}

			// Fit the long edge to maxSize and round the short edge to keep the ratio.
			if (width >= height)
			{
				int scaledHeight = (int)(((long)height * maxSize + width / 2) / width);
				return (maxSize, Math.Max(1, scaledHeight));
			}

			int scaledWidth = (int)(((long)width * maxSize + height / 2) / height);
			return (Math.Max(1, scaledWidth), maxSize);
		}

		/// <summary>
		/// Downscales a pixel buffer to the given size (area average, rounded to nearest).
		/// </summary>
		/// <param name="source">Source pixels without row padding.</param>
		/// <param name="sourceWidth">Source width.</param>
		/// <param name="sourceHeight">Source height.</param>
		/// <param name="bytesPerPixel">Bytes per pixel (1 to 4).</param>
		/// <param name="destWidth">Destination width (at most the source width).</param>
		/// <param name="destHeight">Destination height (at most the source height).</param>
		/// <returns>The downscaled pixels.</returns>
		public static byte[] Downscale(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, int bytesPerPixel, int destWidth, int destHeight)
		{
			if (bytesPerPixel < 1 || bytesPerPixel > MAX_BYTES_PER_PIXEL)
			{
				throw new ArgumentOutOfRangeException(nameof(bytesPerPixel), "bytesPerPixel must be between 1 and 4.");
			}

			if (destWidth <= 0 || destHeight <= 0 || destWidth > sourceWidth || destHeight > sourceHeight)
			{
				throw new ArgumentOutOfRangeException(nameof(destWidth), "Destination size must be at least 1 and no larger than the source.");
			}

			if (source.Length < (long)sourceWidth * sourceHeight * bytesPerPixel)
			{
				throw new ArgumentException("Source buffer is shorter than width * height * bytesPerPixel.", nameof(source));
			}

			int sourceRowLength = sourceWidth * bytesPerPixel;
			var dest = new byte[destWidth * destHeight * bytesPerPixel];

			// Sums the source rows that collapse into one destination row, column by column.
			// Reading the source once, front to back, keeps it cache friendly.
			var columnSums = new int[sourceRowLength];
			Span<int> sums = stackalloc int[MAX_BYTES_PER_PIXEL];

			for (int dy = 0; dy < destHeight; dy++)
			{
				// Source rows [sy0, sy1) collapse into this destination row.
				int sy0 = (int)((long)dy * sourceHeight / destHeight);
				int sy1 = (int)((long)(dy + 1) * sourceHeight / destHeight);

				// Vertical pass.
				Array.Clear(columnSums, 0, sourceRowLength);

				for (int sy = sy0; sy < sy1; sy++)
				{
					var row = source.Slice(sy * sourceRowLength, sourceRowLength);

					for (int i = 0; i < row.Length; i++)
					{
						columnSums[i] += row[i];
					}
				}

				int rowCount = sy1 - sy0;
				int destRowOffset = dy * destWidth * bytesPerPixel;

				// Horizontal pass.
				for (int dx = 0; dx < destWidth; dx++)
				{
					// Source columns [sx0, sx1) collapse into this destination pixel.
					int sx0 = (int)((long)dx * sourceWidth / destWidth);
					int sx1 = (int)((long)(dx + 1) * sourceWidth / destWidth);

					sums.Clear();

					for (int sx = sx0; sx < sx1; sx++)
					{
						int pixelOffset = sx * bytesPerPixel;

						for (int c = 0; c < bytesPerPixel; c++)
						{
							sums[c] += columnSums[pixelOffset + c];
						}
					}

					int count = rowCount * (sx1 - sx0);
					int destOffset = destRowOffset + dx * bytesPerPixel;

					for (int c = 0; c < bytesPerPixel; c++)
					{
						dest[destOffset + c] = (byte)((sums[c] + count / 2) / count);
					}
				}
			}

			return dest;
		}

		/// <summary>
		/// Expands grey (1 byte) to RGB and grey + alpha (2 bytes) to RGBA so the pixels can be used
		/// with <c>TextureFormat.RGB24</c> / <c>RGBA32</c>. RGB and RGBA buffers are returned as is.
		/// </summary>
		/// <param name="pixels">Source pixels.</param>
		/// <param name="bytesPerPixel">Bytes per pixel of <paramref name="pixels"/> (1 to 4).</param>
		/// <param name="expandedBytesPerPixel">Bytes per pixel of the returned buffer (3 or 4).</param>
		/// <returns>RGB or RGBA pixels.</returns>
		public static byte[] ExpandToColor(byte[] pixels, int bytesPerPixel, out int expandedBytesPerPixel)
		{
			if (pixels == null)
			{
				throw new ArgumentNullException(nameof(pixels));
			}

			switch (bytesPerPixel)
			{
				case 3:
				case 4:
					expandedBytesPerPixel = bytesPerPixel;
					return pixels;

				case 1:
				{
					expandedBytesPerPixel = 3;
					var rgb = new byte[pixels.Length * 3];

					for (int i = 0, o = 0; i < pixels.Length; i++, o += 3)
					{
						rgb[o] = rgb[o + 1] = rgb[o + 2] = pixels[i];
					}

					return rgb;
				}

				case 2:
				{
					expandedBytesPerPixel = 4;
					var rgba = new byte[pixels.Length * 2];

					for (int i = 0, o = 0; i + 1 < pixels.Length; i += 2, o += 4)
					{
						rgba[o] = rgba[o + 1] = rgba[o + 2] = pixels[i];
						rgba[o + 3] = pixels[i + 1];
					}

					return rgba;
				}

				default:
					throw new ArgumentOutOfRangeException(nameof(bytesPerPixel), "bytesPerPixel must be between 1 and 4.");
			}
		}
	}
}
