using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace LeanImageLoader.Tests
{
	/// <summary>
	/// Builds small encoded images for tests.
	/// </summary>
	internal static class TestImages
	{
		/// <summary>
		/// Encodes a PNG whose bottom half is <paramref name="bottom"/> and top half is <paramref name="top"/>.
		/// </summary>
		public static byte[] TwoBandPng(int width, int height, Color32 bottom, Color32 top)
		{
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

			try
			{
				var colors = new Color32[width * height];

				// Unity textures store the bottom row first.
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						colors[y * width + x] = y < height / 2 ? bottom : top;
					}
				}

				texture.SetPixels32(colors);
				texture.Apply();
				return texture.EncodeToPNG();
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}

		/// <summary>
		/// Encodes an 8-bit greyscale PNG (color type 0) filled with <paramref name="value"/>.
		/// Written by hand because Unity cannot be relied on to emit greyscale PNGs.
		/// </summary>
		public static byte[] GreyPng(int width, int height, byte value)
		{
			// Raw scanlines: filter byte 0 + pixels.
			var raw = new byte[(width + 1) * height];
			for (int y = 0; y < height; y++)
			{
				int offset = y * (width + 1);
				raw[offset] = 0;
				for (int x = 0; x < width; x++)
				{
					raw[offset + 1 + x] = value;
				}
			}

			using var png = new MemoryStream();
			png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

			var header = new byte[13];
			WriteBigEndian(header, 0, (uint)width);
			WriteBigEndian(header, 4, (uint)height);
			header[8] = 8;  // bit depth
			header[9] = 0;  // color type: greyscale
			WriteChunk(png, "IHDR", header);
			WriteChunk(png, "IDAT", Zlib(raw));
			WriteChunk(png, "IEND", Array.Empty<byte>());

			return png.ToArray();
		}

		static byte[] Zlib(byte[] data)
		{
			using var output = new MemoryStream();
			output.WriteByte(0x78);
			output.WriteByte(0x01);

			using (var deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
			{
				deflate.Write(data, 0, data.Length);
			}

			uint a = 1, b = 0;
			foreach (byte d in data)
			{
				a = (a + d) % 65521;
				b = (b + a) % 65521;
			}

			var adler = new byte[4];
			WriteBigEndian(adler, 0, (b << 16) | a);
			output.Write(adler, 0, 4);
			return output.ToArray();
		}

		static void WriteChunk(Stream stream, string type, byte[] data)
		{
			var length = new byte[4];
			WriteBigEndian(length, 0, (uint)data.Length);
			stream.Write(length, 0, 4);

			byte[] typeBytes = Encoding.ASCII.GetBytes(type);
			stream.Write(typeBytes, 0, 4);
			stream.Write(data, 0, data.Length);

			// The chunk CRC covers the type and the data.
			uint crc = UpdateCrc32(UpdateCrc32(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
			var crcBytes = new byte[4];
			WriteBigEndian(crcBytes, 0, crc);
			stream.Write(crcBytes, 0, 4);
		}

		static uint UpdateCrc32(uint crc, byte[] bytes)
		{
			foreach (byte b in bytes)
			{
				crc ^= b;
				for (int k = 0; k < 8; k++)
				{
					crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
				}
			}

			return crc;
		}

		static void WriteBigEndian(byte[] buffer, int offset, uint value)
		{
			buffer[offset] = (byte)(value >> 24);
			buffer[offset + 1] = (byte)(value >> 16);
			buffer[offset + 2] = (byte)(value >> 8);
			buffer[offset + 3] = (byte)value;
		}
	}
}
