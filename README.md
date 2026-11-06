# Lean Image Loader

[日本語](README.ja.md)

Load hundreds of images (thumbnails, album jackets, banners...) into downscaled Unity textures **fast**, while keeping **peak memory flat**.

Built for a mobile rhythm game whose song-select screen loads a jacket for every song. The naive approach, throwing every image at the thread pool, decoded full-size images faster than the main thread could upload them, and phones ran out of memory during loading. This library keeps the same speed with roughly a third of the peak memory.

## Features

- **Bounded parallel loading**: decoding runs on at most `MaxWorkers` threads, and at most `MaxPending` images exist between decode and upload. Peak memory no longer grows with the number of images.
- **No full-size managed copy**: images are downscaled straight from the decoder's native buffer, which is freed right away. Only the small result touches the managed heap (the IL2CPP/Mono heap never shrinks once it grows).
- **Fast box-filter downscaling** that reads the source once, front to back.
- **Frame-budgeted uploads**: the main thread uploads textures for at most `FrameBudgetSeconds` per frame, so loading screens keep animating.
- **No dependencies**: uses Unity's `Awaitable`. The decoder ([StbImageSharp](https://github.com/StbSharp/StbImageSharp), public domain) is bundled as *internal* types, so it never conflicts with another copy of StbImageSharp in your project.
- Formats: PNG, JPEG, BMP, TGA, PSD, GIF (first frame), HDR. Greyscale images are expanded to RGB.

## Requirements

- Unity 2023.1 or later (Unity 6 supported)
- Not supported on WebGL (needs threads)

## Installation

Package Manager → **+** → **Add package from git URL...**

```
https://github.com/na24ddr/LeanImageLoader.git
```

or add to `Packages/manifest.json`:

```json
"com.na24ddr.lean-image-loader": "https://github.com/na24ddr/LeanImageLoader.git"
```

## Usage

### Load many images

```csharp
using LeanImageLoader;

async Awaitable LoadJacketsAsync(string[] paths, CancellationToken ct)
{
    var options = new BatchLoadOptions { MaxSize = 640 };

    Texture2D[] textures = await BatchImageLoader.LoadAsync(
        paths,
        options,
        (finished, total) => progressBar.value = (float)finished / total,
        ct);

    // textures[i] matches paths[i]; it is null when the file is missing or not an image.
}
```

Call it on the main thread. You own the returned textures: `Destroy` them when you no longer need them. When cancelled, textures created so far are destroyed and `OperationCanceledException` is thrown.

| Option | Default | Meaning |
|---|---|---|
| `MaxSize` | 640 | Upper bound for the long edge (px). Smaller images are kept as they are. |
| `MaxWorkers` | 8 | Upper bound for decoding threads. The actual count is `min(SystemInfo.processorCount, MaxWorkers)`. |
| `MaxPending` | 16 | Upper bound for images being decoded or waiting for upload. |
| `FrameBudgetSeconds` | 0.025 | Main-thread time per frame spent on uploads. |
| `Linear` | false | Create linear (non-color) textures. |

### Load one image on the main thread

```csharp
Texture2D texture = LeanTextureLoader.LoadDownscaled(path, 640);
```

Uses Unity's native `ImageConversion.LoadImage` (PNG / JPEG), then downscales.

### Decode on your own thread

```csharp
// Any thread: no Unity API involved.
DecodedImage image = LeanImageDecoder.Decode(path, 640);

// Main thread.
Texture2D texture = image.ToTexture2D();
```

`ImageDownscaler` (pure C#) is also public if you only need the downscaling.

## Benchmark

300 images (500×500 JPEG, 1000×708 PNG, 2000×1500 PNG), process limited to 8 cores, downscaled to 640 px:

| | Time | Peak working set |
|---|---|---|
| Thread pool, all at once, full-size decode | 1.66 s | ~580 MB |
| Lean Image Loader (8 workers, 16 pending) | 1.66–1.73 s | ~180 MB |

## Running the tests

Add the package to `testables` in your project's `Packages/manifest.json`, then use the Test Runner:

```json
"testables": ["com.na24ddr.lean-image-loader"]
```

## License

[MIT](LICENSE.md). Bundled third-party code is listed in [Third Party Notices](Third%20Party%20Notices.md).
