using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LeanImageLoader
{
	/// <summary>
	/// Settings for <see cref="BatchImageLoader.LoadAsync"/>.
	/// </summary>
	public sealed class BatchLoadOptions
	{
		/// <summary>Upper bound for the long edge of every texture (px).</summary>
		public int MaxSize = 640;

		/// <summary>
		/// Upper bound for decoding threads. The actual count is min(processor count, this).
		/// Each thread temporarily needs memory for one full-size image while decoding.
		/// </summary>
		public int MaxWorkers = 8;

		/// <summary>
		/// Upper bound for images that are decoding or decoded but not yet uploaded.
		/// Workers wait when it is reached, which caps peak memory regardless of how many images
		/// are loaded or how far the main thread falls behind.
		/// </summary>
		public int MaxPending = 16;

		/// <summary>
		/// Main-thread time per frame spent uploading textures (seconds). Keeps the frame rate of a
		/// loading screen steady.
		/// </summary>
		public float FrameBudgetSeconds = 0.025f;

		/// <summary>Whether the textures hold linear (non-color) data.</summary>
		public bool Linear = false;
	}

	/// <summary>
	/// Loads many images (thumbnails, album jackets, banners...) into downscaled textures quickly
	/// while keeping peak memory flat.
	///
	/// Decoding and downscaling run on a fixed number of worker threads; only the texture upload of
	/// the small, already downscaled pixels runs on the main thread, within a per-frame time budget.
	///
	/// Throwing every image at the thread pool at once is what usually makes this kind of loader
	/// crash on phones: decoded full-size images pile up while they wait for the main thread. Here
	/// at most <see cref="BatchLoadOptions.MaxPending"/> images exist between decode and upload.
	///
	/// Not supported on WebGL (no threads).
	/// </summary>
	public static class BatchImageLoader
	{
		/// <summary>
		/// Loads every path into a downscaled texture. Must be called on the main thread.
		/// </summary>
		/// <param name="paths">Image file paths. null entries are skipped.</param>
		/// <param name="options">Settings. null uses the defaults.</param>
		/// <param name="onProgress">Called on the main thread with (finished count, total count), at most once per frame.</param>
		/// <param name="cancellationToken">Cancels the load. Textures created so far are destroyed.</param>
		/// <returns>
		/// One texture per path, in the same order. An entry is null when its path is null, missing,
		/// or not a decodable image. The caller owns the textures and should Destroy them when done.
		/// </returns>
		/// <exception cref="OperationCanceledException">The load was cancelled.</exception>
		public static async Awaitable<Texture2D[]> LoadAsync(
			IReadOnlyList<string> paths,
			BatchLoadOptions options = null,
			Action<int, int> onProgress = null,
			CancellationToken cancellationToken = default)
		{
			if (paths == null)
			{
				throw new ArgumentNullException(nameof(paths));
			}

			options ??= new BatchLoadOptions();
			Validate(options);

			int total = paths.Count;
			var textures = new Texture2D[total];

			if (total == 0)
			{
				onProgress?.Invoke(0, 0);
				return textures;
			}

			// Snapshot the paths so workers never touch a collection the caller may change.
			var pathArray = new string[total];
			for (int i = 0; i < total; i++)
			{
				pathArray[i] = paths[i];
			}

			int maxSize = options.MaxSize;
			var decoded = new ConcurrentQueue<(int Index, DecodedImage Image)>();
			var pendingSlots = new SemaphoreSlim(options.MaxPending, options.MaxPending);
			int nextIndex = -1;

			// Lets the workers stop as soon as the main thread gives up for any reason.
			using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			CancellationToken stopToken = stop.Token;

			void DecodeWorker()
			{
				while (!stopToken.IsCancellationRequested)
				{
					int index = Interlocked.Increment(ref nextIndex);

					if (index >= total)
					{
						return;
					}

					try
					{
						pendingSlots.Wait(stopToken);
					}
					catch (OperationCanceledException)
					{
						return;
					}

					DecodedImage image = null;

					if (pathArray[index] != null)
					{
						try
						{
							image = LeanImageDecoder.Decode(pathArray[index], maxSize);
						}
						catch (Exception)
						{
							// Any failure (missing file, corrupt data...) becomes a null entry. Letting it
							// escape would end this worker without queuing the image, and the main thread
							// would wait for it forever.
							image = null;
						}
					}

					// Failures are queued too, so the main thread can count every image.
					decoded.Enqueue((index, image));
				}
			}

			int workerCount = Mathf.Clamp(SystemInfo.processorCount, 1, options.MaxWorkers);

			for (int i = 0; i < workerCount; i++)
			{
				Task.Run(DecodeWorker);
			}

			int finished = 0;

			try
			{
				while (finished < total)
				{
					cancellationToken.ThrowIfCancellationRequested();

					float frameStart = Time.realtimeSinceStartup;

					while (decoded.TryDequeue(out var item))
					{
						textures[item.Index] = item.Image?.ToTexture2D(options.Linear);
						finished++;
						pendingSlots.Release();

						if (Time.realtimeSinceStartup - frameStart >= options.FrameBudgetSeconds)
						{
							break;
						}
					}

					onProgress?.Invoke(finished, total);

					if (finished < total)
					{
						await Awaitable.NextFrameAsync(cancellationToken);
					}
				}
			}
			catch
			{
				stop.Cancel();

				foreach (var texture in textures)
				{
					ObjectDisposal.Destroy(texture);
				}

				throw;
			}

			return textures;
		}

		static void Validate(BatchLoadOptions options)
		{
			if (options.MaxSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "MaxSize must be at least 1.");
			}

			if (options.MaxWorkers <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "MaxWorkers must be at least 1.");
			}

			if (options.MaxPending <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "MaxPending must be at least 1.");
			}

			if (options.FrameBudgetSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "FrameBudgetSeconds must not be negative.");
			}
		}
	}
}
