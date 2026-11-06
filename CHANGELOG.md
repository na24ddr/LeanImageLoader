# Changelog

## [1.0.0] - 2026-11-06

### Added

- `BatchImageLoader.LoadAsync`: bounded parallel loading of many images into downscaled textures.
- `LeanImageDecoder`: thread-safe decode + downscale without a full-size managed copy.
- `LeanTextureLoader.LoadDownscaled`: one-off main-thread loading via `ImageConversion.LoadImage`.
- `ImageDownscaler`: area-average downscaling of raw pixel buffers.
- `DecodedImage.ToTexture2D`: uploads decoded pixels to a non-readable texture.
