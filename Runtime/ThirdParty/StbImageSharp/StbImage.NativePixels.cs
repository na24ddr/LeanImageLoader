using Hebron.Runtime;
using System;
using System.IO;

namespace StbImageSharp
{
	/// <summary>
	/// Extension added by LeanImageLoader (not part of upstream StbImageSharp).
	///
	/// <see cref="ImageResult.FromStream"/> copies the decoded pixels (native memory) into a full-size
	/// managed byte[]. When the image is downscaled right after decoding, that large array only
	/// inflates the managed heap, which on IL2CPP/Mono never shrinks back. This entry point hands the
	/// native pixel buffer straight to a callback and frees it as soon as the callback returns.
	/// </summary>
#if !STBSHARP_INTERNAL
	public
#else
	internal
#endif
	static unsafe partial class StbImage
	{
		/// <summary>
		/// Receives the decoded pixels. <paramref name="pixels"/> is freed when the callback returns,
		/// so it must not be stored.
		/// </summary>
		/// <param name="pixels">First byte of the tightly packed pixel rows.</param>
		/// <param name="width">Image width.</param>
		/// <param name="height">Image height.</param>
		/// <param name="comp">Components (= bytes) per pixel.</param>
		public delegate T NativePixelsHandler<T>(byte* pixels, int width, int height, int comp);

		/// <summary>
		/// Decodes an image from <paramref name="stream"/> and passes the native pixel buffer to
		/// <paramref name="handler"/>. Vertical flipping follows stbi_set_flip_vertically_on_load.
		/// </summary>
		/// <param name="stream">Encoded image.</param>
		/// <param name="requiredComponents">Components to convert to (Default keeps the source's).</param>
		/// <param name="handler">Callback that consumes the pixels.</param>
		/// <returns>The value returned by <paramref name="handler"/>.</returns>
		public static T LoadWithNativePixels<T>(Stream stream, ColorComponents requiredComponents, NativePixelsHandler<T> handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			byte* result = null;

			try
			{
				int x, y, comp;

				var context = new stbi__context(stream);

				result = stbi__load_and_postprocess_8bit(context, &x, &y, &comp, (int)requiredComponents);

				if (result == null)
					throw new InvalidOperationException(stbi__g_failure_reason);

				int outComp = requiredComponents == ColorComponents.Default ? comp : (int)requiredComponents;

				return handler(result, x, y, outComp);
			}
			finally
			{
				if (result != null)
					CRuntime.free(result);
			}
		}
	}
}
