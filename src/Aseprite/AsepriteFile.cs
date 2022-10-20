using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MyGame.Aseprite;

/// <summary>
/// https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
/// 2021-09-28
/// </summary>
public class AsepriteFile
{
	public readonly AsepriteHeader Header;
	public readonly List<AsepriteFrame> Frames = new();

	public AsepriteFile(Stream stream)
	{
		using var reader = new BinaryReader(stream);
		Header = new AsepriteHeader(reader);

		for (int i = 0; i < Header.Frames; i++)
		{
			var framePos = reader.BaseStream.Position;
			var frame = new AsepriteFrame(Header, reader);
			Frames.Add(frame);
			reader.BaseStream.Seek(framePos + frame.FrameLength, SeekOrigin.Begin);
		}
	}

	public enum LoopAnimation : byte
	{
		Forward = 0,
		Reverse = 1,
		PingPong = 2,
	}

	public enum ColorDepth : ushort
	{
		RGBA = 32,
		Grayscale = 16,
		Indexed = 8,
	}

	public enum ChunkType : ushort
	{
		OldPalette =
			0x0004, // 4 Ignore this chunk if you find the new palette chunk (0x2019) Aseprite v1.1 saves both chunks 0x0004 and 0x2019 just for backward compatibility.
		OldPalette2 = 0x0011, // Ignore this chunk if you find the new palette chunk (0x2019)
		Layer = 0x2004, // 8196 In the first frame should be a set of layer chunks to determine the entire layers layout:
		Cel = 0x2005, // 8197 This chunk determine where to put a cel in the specified layer/frame.
		CelExtra = 0x2006, // Adds extra information to the latest read cel.
		ColorProfile = 0x2007, // 8199 Color profile for RGB or grayscale values.

		ExternalFiles =
			0x2008, // A list of external files linked with this file. It might be used to reference external palettes or tilesets.
		Mask = 0x2016, // DEPRECATED
		Path = 0x2017, // NEVER USED

		FrameTags =
			0x2018, // After the tags chunk, you can write one user data chunk for each tag. E.g. if there are 10 tags, you can then write 10 user data chunks one for each tag.
		Palette = 0x2019, // 8217
		UserData = 0x2020,
		Slice = 0x2022,
		TileSet = 0x2023,
	}

	public enum LayerType : ushort
	{
		Normal = 0,
		Group = 1,
		Tilemap = 2,
	}

	public class AsepriteHeader
	{
		public uint FileSize { get; }
		public ushort MagicNumber { get; }
		public ushort Frames { get; }
		public ushort Width { get; }
		public ushort Height { get; }
		public ColorDepth ColorDepth { get; }
		public uint Flags { get; }

		[Obsolete("DEPRECATED: You should use the frame duration field from each frame header")]
		public ushort Speed { get; }

		public byte TransparentIndex { get; }
		public ushort ColorCount { get; }
		public byte PixelWidth { get; }
		public byte PixelHeight { get; }
		public ushort GridHeight { get; }
		public ushort GridWidth { get; }
		public short GridY { get; }
		public short GridX { get; }

		public AsepriteHeader(BinaryReader reader)
		{
			FileSize = DWORD(reader); // DWORD File size
			MagicNumber = WORD(reader); // WORD Magic number (0xA5E0)

			if (MagicNumber != 0xA5E0)
				throw new Exception("File is not in aseprite format");

			Frames = WORD(reader); // WORD Frames
			Width = WORD(reader); // WORD Width in pixels
			Height = WORD(reader); // WORD Height in pixels
			ColorDepth = (ColorDepth)WORD(reader); // WORD Color depth (bits per pixel) [32 bpp = RGBA, 16 bpp = Grayscale, 8 bpp Indexed]
			Flags = DWORD(reader); // DWORD Flags: 1 = Layer opacity has valid value
#pragma warning disable 618
			Speed = WORD(reader); // WORD Speed (milliseconds between frame, like in FLC files) DEPRECATED: You should use the frame duration field from each frame header
#pragma warning restore 618

			DWORD(reader); // DWORD Set be 0
			DWORD(reader); // DWORD Set be 0

			TransparentIndex =
				BYTE(reader); // BYTE Palette entry (index) which represent transparent color in all non-background layers (only for Indexed sprites)

			SEEK(reader, 3); // BYTE[3] Ignore these bytes

			ColorCount = WORD(reader); // WORD Number of colors (0 means 256 for old sprites)
			PixelWidth = BYTE(
				reader); // BYTE Pixel width (pixel ratio is "pixel width/pixel height"). If pixel height field is zero, pixel ratio is 1:1
			PixelHeight = BYTE(reader); // BYTE Pixel height
			GridX = SHORT(reader); // SHORT       X position of the grid
			GridY = SHORT(reader); // SHORT       Y position of the grid
			GridWidth = WORD(reader); // WORD        Grid width (zero if there is no grid, grid size is 16x16 on Aseprite by default)
			GridHeight = WORD(reader); // WORD        Grid height (zero if there is no grid)
			SEEK(reader, 84); // BYTE[84]    For future (set to zero)
		}
	}

	public interface IHasUserData
	{
		UserData? UserData { get; set; }
	}

	public class AsepriteFrame
	{
		private readonly bool _useNewChunkCount = true;
		public uint NewChunksCount { get; set; }
		public ushort FrameDuration { get; set; }
		public ushort OldChunksCount { get; set; }
		public ushort MagicNumber { get; set; }
		public uint FrameLength { get; set; }
		public uint ChunkCount => _useNewChunkCount ? NewChunksCount : OldChunksCount;

		public List<Layer> Layers = new();
		public List<Cel> Cels = new();
		public List<Palette> Palettes = new();
		public List<ColorProfile> ColorProfiles = new();
		public List<FrameTag> FrameTags = new();

		public UserData? SpriteUserData;

		public AsepriteFrame(AsepriteHeader asepriteHeader, BinaryReader reader)
		{
			FrameLength = DWORD(reader); // DWORD Bytes in this frame
			MagicNumber = WORD(reader); // WORD Magic number (always 0xF1FA)

			OldChunksCount =
				WORD(reader); // WORD Old field which specifies the number of "chunks" in this frame. If this value is 0xFFFF, we might have more chunks to read in this frame (so we have to use the new field)
			FrameDuration = WORD(reader); // WORD Frame duration (in milliseconds)

			SEEK(reader, 2); // BYTE[2] For future (set to zero)

			NewChunksCount =
				DWORD(reader); // DWORD New field which specifies the number of "chunks" in this frame (if this is 0, use the old field)

			if (NewChunksCount == 0)
				_useNewChunkCount = false;

			var ignoreOldColorChunks = false;

			IHasUserData? lastReadChunk = null;

			var frameTagUserDataCounter = 0;

			for (int i = 0; i < ChunkCount; i++)
			{
				var chunkPos = reader.BaseStream.Position;
				var chunkLength = DWORD(reader); // DWORD Chunk size
				var chunkType = WORD(reader); // WORD Chunk type

				switch (chunkType)
				{
					case (ushort)ChunkType.OldPalette:
					case (ushort)ChunkType.OldPalette2:
						if (!ignoreOldColorChunks)
						{
							var oldPalette = chunkType == (ushort)ChunkType.OldPalette
								? Palette.PaletteOld(reader)
								: Palette.PaletteOld2(reader);
						}

						break;
					case (ushort)ChunkType.Layer:
						var layer = new Layer(reader);
						lastReadChunk = layer;
						Layers.Add(layer);
						break;
					case (ushort)ChunkType.Cel:
						var cel = new Cel(chunkLength, reader, asepriteHeader.ColorDepth);
						lastReadChunk = cel;
						Cels.Add(cel);
						break;
					case (ushort)ChunkType.CelExtra:
						break;
					case (ushort)ChunkType.ColorProfile:
						var colorProfile = new ColorProfile(reader);
						ColorProfiles.Add(colorProfile);
						break;
					case (ushort)ChunkType.ExternalFiles:
						break;
					case (ushort)ChunkType.Mask:
						break;
					case (ushort)ChunkType.Path:
						break;
					case (ushort)ChunkType.FrameTags:
						var numberOfTags = WORD(reader); //WORD        Number of tags
						SEEK(reader, 8); // BYTE[8]     For future (set to zero)
						for (int j = 0; j < numberOfTags; j++)
						{
							var tag = new FrameTag(reader);
							lastReadChunk = tag;
							FrameTags.Add(tag);
						}

						break;
					case (ushort)ChunkType.Palette:
						var palette = new Palette(reader);
						Palettes.Add(palette);
						ignoreOldColorChunks = true;
						break;
					case (ushort)ChunkType.UserData:
						var userData = new UserData(reader);
						if (lastReadChunk is FrameTag)
						{
							FrameTags[frameTagUserDataCounter++].UserData = userData;
						}
						else if (lastReadChunk != null)
						{
							lastReadChunk.UserData = userData;
						}
						else
						{
							SpriteUserData = userData;
						}

						break;
					case (ushort)ChunkType.Slice:
						break;
					case (ushort)ChunkType.TileSet:
						break;
				}

				// Skip chunk size
				reader.BaseStream.Seek(chunkPos + chunkLength, SeekOrigin.Begin);
			}
		}
	}

	public class UserData
	{
		private const int ASE_USER_DATA_FLAG_HAS_TEXT = 1;
		private const int ASE_USER_DATA_FLAG_HAS_COLOR = 2;
		public string Text { get; private set; } = string.Empty;
		public Color Color { get; private set; }

		public UserData(BinaryReader reader)
		{
			/*
			DWORD       Flags
			1 = Has text
			2 = Has color
			*/
			var flags = DWORD(reader);
			if ((flags & ASE_USER_DATA_FLAG_HAS_TEXT) != 0)
			{
				Text = STRING(reader);
			}

			if ((flags & ASE_USER_DATA_FLAG_HAS_COLOR) != 0)
			{
				var r = BYTE(reader); // BYTE      Red (0-255)
				var g = BYTE(reader); // BYTE      Green (0-255)
				var b = BYTE(reader); // BYTE      Blue (0-255)
				var a = BYTE(reader); // BYTE      Alpha (0-255)
				Color = new Color(r, g, b, a);
			}
		}
	}

	public class FrameTag : IHasUserData
	{
		public UserData? UserData { get; set; }
		public ushort FrameFrom { get; private set; }
		public ushort FrameTo { get; private set; }
		public LoopAnimation Animation { get; private set; }
		public Color TagColor { get; set; } // 3 Bytes
		public string TagName { get; private set; }

		public FrameTag(BinaryReader reader)
		{
			FrameFrom = WORD(reader); // WORD      From frame
			FrameTo = WORD(reader); //  WORD      To frame
			Animation = (LoopAnimation)BYTE(reader); // Loop animation direction
			SEEK(reader, 8); // For future (set to zero)

			/*
			RGB values of the tag color
			Deprecated, used only for backward compatibility with Aseprite v1.2.x
			The color of the tag is the one in the user data field following
			the tags chunk
			*/
			var colorBytes = BYTES(reader, 3);
			TagColor = new Color(colorBytes[0], colorBytes[1], colorBytes[2]);

			BYTE(reader); // BYTE      Extra byte (zero)

			TagName = STRING(reader);
		}
	}

	public class Palette
	{
		public struct PaletteEntry
		{
			public ushort EntryFlags;
			public byte Red;
			public byte Green;
			public byte Blue;
			public byte Alpha;
			public string Name;
		}

		public List<PaletteEntry> Entries = new();

		public Palette()
		{
		}

		public Palette(BinaryReader reader)
		{
			PaletteSize = DWORD(reader); // DWORD New palette size (total number of entries)
			FirstColorIndex = DWORD(reader); // DWORD First color index to change
			LastColorIndex = DWORD(reader); // DWORD Last color index to change
			SEEK(reader, 8); // BYTE[8] For future (set to zero)

			// + For each palette entry in [from,to] range (to-from+1 entries)
			for (int i = 0; i < PaletteSize; i++)
			{
				var entry = WORD(reader); // WORD Entry flags: 1 = Has name
				var r = BYTE(reader); // BYTE      Red (0-255)
				var g = BYTE(reader); // BYTE      Green (0-255)
				var b = BYTE(reader); // BYTE      Blue (0-255)
				var a = BYTE(reader); // BYTE      Alpha (0-255)

				string name = string.Empty;
				if (entry != 0)
				{
					name = STRING(reader); // STRING  Color name
				}

				Entries.Add(new PaletteEntry()
				{
					EntryFlags = entry,
					Red = r,
					Green = g,
					Blue = b,
					Alpha = a,
					Name = name
				});
			}
		}

		public uint LastColorIndex { get; set; }

		public uint FirstColorIndex { get; set; }

		public uint PaletteSize { get; set; }

		public static Palette PaletteOld(BinaryReader reader)
		{
			var packets = WORD(reader); // Number of packets
			var palette = new Palette();

			var skip = 0;
			for (int i = 0; i < packets; i++)
			{
				skip += BYTE(reader); // BYTE Number of palette entries to skip from the last packet (start from 0)
				int size = BYTE(reader); // BYTE Number of colors in the packet (0 means 256)

				if (size == 0)
					size = 256;

				for (int j = skip; j < skip + size; j++)
				{
					var red = BYTE(reader); // BYTE Red (0-255)
					var green = BYTE(reader); // BYTE Green (0-255)
					var blue = BYTE(reader); // BYTE Blue (0-255)
					palette.Entries.Add(new PaletteEntry()
					{
						Red = red,
						Green = green,
						Blue = blue
					});
				}
			}

			return palette;
		}

		public static Palette PaletteOld2(BinaryReader reader)
		{
			var palette = new Palette();
			var packets = WORD(reader); // Number of packets

			var skip = 0;
			for (int i = 0; i < packets; i++)
			{
				skip += BYTE(reader); // BYTE Number of palette entries to skip from the last packet (start from 0)
				int size = BYTE(reader); // BYTE Number of colors in the packet (0 means 256)

				if (size == 0)
					size = 256;

				for (int j = skip; j < skip + size; j++)
				{
					// TODO (marpe): scale_6bits_to_8bits https://github.com/aseprite/aseprite/blob/c42c5e1453357ba7176cdba1322eda0efafdb4a5/src/dio/aseprite_decoder.cpp#L357
					var red = BYTE(reader); // BYTE Red (0-63)
					var green = BYTE(reader); // BYTE Green (0-63)
					var blue = BYTE(reader); // BYTE Blue (0-63)
					palette.Entries.Add(new PaletteEntry()
					{
						Red = red,
						Green = green,
						Blue = blue
					});
				}
			}

			return palette;
		}

		public Color GetColor(byte index)
		{
			if (index >= FirstColorIndex && index <= LastColorIndex)
			{
				var entry = Entries[index];

				var red = entry.Red / 255f;
				var green = entry.Green / 255f;
				var blue = entry.Blue / 255f;
				var alpha = entry.Alpha / 255f;

				return new Color(red, green, blue, alpha);
			}

			return Color.Magenta;
		}
	}

	public enum LayerBlendMode : ushort
	{
		Normal = 0,
		Multiply = 1,
		Screen = 2,
		Overlay = 3,
		Darken = 4,
		Lighten = 5,
		ColorDodge = 6,
		ColorBurn = 7,
		HardLight = 8,
		SoftLight = 9,
		Difference = 10,
		Exclusion = 11,
		Hue = 12,
		Saturation = 13,
		Color = 14,
		Luminosity = 15,
		Addition = 16,
		Subtract = 17,
		Divide = 18
	}

	[Flags]
	public enum LayerFlags : ushort
	{
		None = 0,
		Visible = 1,
		Editable = 2,
		LockMovement = 4,
		Background = 8,
		PreferLinkedCels = 16,
		LayerGroupCollapsed = 32,
		ReferenceLayer = 64,
	}

	public class Layer : IHasUserData
	{
		public UserData? UserData { get; set; }

		public Layer(BinaryReader reader)
		{
			Flags = WORD(reader); // WORD        Flags
			Type = WORD(reader); // WORD        Layer type

			ChildLevel = WORD(reader); // WORD        Layer child level (see NOTE.1)
			var defaultLayerWidth = WORD(reader); // WORD        Default layer width in pixels (ignored)
			var defaultLayerHeight = WORD(reader); // WORD        Default layer height in pixels (ignored)
			BlendMode = WORD(reader); // WORD        Blend mode (always 0 for layer set)
			Opacity = BYTE(reader); // BYTE        Opacity Note: valid only if file header flags field has bit 1 set
			SEEK(reader, 3); // BYTE[3]     For future (set to zero)
			LayerName = STRING(reader); // STRING      Layer name
			if (Type == (ushort)LayerType.Tilemap)
			{
				TilesetIndex = DWORD(reader); // DWORD     Tileset index
			}
		}

		public bool Visible => ((LayerFlags)Flags).HasFlag(LayerFlags.Visible);

		public uint TilesetIndex { get; set; }

		public ushort ChildLevel { get; set; }

		public ushort BlendMode { get; set; }

		public byte Opacity { get; set; }

		public string LayerName { get; set; }

		public ushort Flags { get; set; }

		public ushort Type { get; set; }
	}

	public enum ColorProfileType : ushort
	{
		None = 0,
		sRGB = 1,
		EmbeddedIcc = 2
	}

	public class ColorProfile
	{
		public ushort Type { get; set; }

		public ColorProfile(BinaryReader reader)
		{
			Type = WORD(reader); // WORD        Type
			Flags = WORD(reader); // WORD Flags 1 - use special fixed gamma

			/*
			FIXED       Fixed gamma (1.0 = linear)
			            Note: The gamma in sRGB is 2.2 in overall but it doesn't use
			            this fixed gamma, because sRGB uses different gamma sections
			            (linear and non-linear). If sRGB is specified with a fixed
			            gamma = 1.0, it means that this is Linear sRGB.
			 */
			Gamma = reader.ReadSingle();
			SEEK(reader, 8); // BYTE[8]     Reserved (set to zero)

			/*
			+ If type = ICC:
			DWORD     ICC profile data length
			BYTE[]    ICC profile data. More info: http://www.color.org/ICC1V42.pdf
			*/
			if (Type == (ushort)ColorProfileType.EmbeddedIcc)
			{
				var iccProfileLength = DWORD(reader);
				var iccProfileData = BYTES(reader, (int)iccProfileLength);
			}
		}

		public float Gamma { get; set; }

		public ushort Flags { get; set; }
	}

	public enum CelType : ushort
	{
		RawImageData = 0, // (unused, compressed image is preferred)
		LinkedCel = 1,
		CompressedImage = 2,
		CompressedTilemap = 3,
	}

	public class Cel : IHasUserData
	{
		public UserData? UserData { get; set; }

		private uint[] ReadRGBA(Span<byte> colors) // [Red, Green, Blue, Alpha]
		{
			var size = (int)Width * (int)Height;
			var pixels = new uint[size];

			for (int i = 0; i < size; i++)
			{
				var j = i * 4;
				// Xna packs colors in ABGR
				// pixels[i] = (uint)((colors[j + 3] << 24) | (colors[j + 2] << 16) | (colors[j + 1] << 8) | (colors[j + 0]));
				var a = colors[j + 3];
				var r = colors[j + 0] * a / 255;
				var g = colors[j + 1] * a / 255;
				var b = colors[j + 2] * a / 255;
				pixels[i] = (uint)((a << 24) | (b << 16) | (g << 8) | (r));
			}

			return pixels;
		}

		private uint[] ReadGrayscale(Span<byte> colors) // [Value, Alpha]
		{
			var size = (int)Width * (int)Height;
			var pixels = new uint[size];

			for (int i = 0; i < size; i++)
			{
				var j = i * 2;
				pixels[i] = (uint)((colors[j + 1] << 8) | (colors[j + 0]));
			}

			return pixels;
		}

		private uint[] ReadIndexed(Span<byte> colors) // [Index]
		{
			var size = (int)Width * (int)Height;
			var pixels = new uint[size];

			for (int i = 0; i < size; i++)
			{
				pixels[i] = colors[i];
			}

			return pixels;
		}

		private uint[] ConvertBytesToPixels(Span<byte> reader, ColorDepth colorDepth)
		{
			if (colorDepth == ColorDepth.RGBA)
			{
				return ReadRGBA(reader);
			}
			else if (colorDepth == ColorDepth.Grayscale)
			{
				return ReadGrayscale(reader);
			}
			else if (colorDepth == ColorDepth.Indexed)
			{
				return ReadIndexed(reader);
			}

			return Array.Empty<uint>();
		}

		public Cel(uint chunkLength, BinaryReader reader, ColorDepth colorDepth)
		{
			LayerIndex = WORD(reader); // WORD        Layer index (see NOTE.2)
			XPosition = SHORT(reader); // SHORT X position
			YPosition = SHORT(reader); // SHORT Y position
			Opacity = BYTE(reader); // BYTE Opacity
			Type = WORD(reader); // WORD Cel Type
			SEEK(reader, 7); // BYTE[7] For future (set to zero

			if (Type == (ushort)CelType.RawImageData ||
			    Type == (ushort)CelType.CompressedImage)
			{
				Width = WORD(reader); //WORD      Width in pixels
				Height = WORD(reader); //WORD      Height in pixels
				var bytesPerColor = ((int)colorDepth) / 8;
				var byteCount = Width * Height * bytesPerColor;
				var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
				// byte[] buffer = new byte[byteCount];
				if (Type == (ushort)CelType.RawImageData)
				{
					var readBytes = reader.Read(buffer);
				}
				else
				{
					SEEK(reader, 2);
					using var deflate = new DeflateStream(reader.BaseStream, CompressionMode.Decompress, true);
					int readBytes;
					var totalBytesRead = 0;
					do
					{
						readBytes = deflate.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
						totalBytesRead += readBytes;
					} while (readBytes > 0);
				}

				Pixels = ConvertBytesToPixels(buffer, colorDepth);
				ArrayPool<byte>.Shared.Return(buffer);
			}
			else if (Type == (ushort)CelType.LinkedCel)
			{
				FramePositionToLinkWith = WORD(reader); // WORD      Frame position to link with
			}
			else if (Type == (ushort)CelType.CompressedTilemap)
			{
				TileWidth = WORD(reader); // WORD      Width in number of tiles
				TileHeight = WORD(reader); // WORD      Height in number of tiles
				BitsPerTile = WORD(reader); // WORD      Bits per tile (at the moment it's always 32-bit per tile)
				Bitmask = DWORD(reader); // DWORD     Bitmask for tile ID (e.g. 0x1fffffff for 32-bit tiles)
				BitmaskFlipX = DWORD(reader); // DWORD     Bitmask for X flip
				BitmaskFlipY = DWORD(reader); // DWORD     Bitmask for Y flip
				BitmaskRotation = DWORD(reader); // DWORD     Bitmask for 90CW rotation
				SEEK(reader, 10); // BYTE[10]  Reserved
				// TILE[]    Row by row, from top to bottom tile by tile compressed with ZLIB method (see NOTE.3)
			}
		}

		public uint[] Pixels { get; set; } = Array.Empty<uint>();

		public uint BitmaskRotation { get; set; }

		public uint BitmaskFlipY { get; set; }

		public uint BitmaskFlipX { get; set; }

		public uint Bitmask { get; set; }

		public ushort BitsPerTile { get; set; }

		public ushort TileHeight { get; set; }

		public ushort TileWidth { get; set; }

		public ushort FramePositionToLinkWith { get; set; }

		public ushort Width { get; set; }

		public ushort Height { get; set; }

		public short YPosition { get; set; }

		public short XPosition { get; set; }

		public byte Opacity { get; set; }

		public ushort Type { get; set; }

		public ushort LayerIndex { get; set; }
	}

	public static AsepriteFile LoadAsepriteFile(string filePath)
	{
		using var stream = File.OpenRead(filePath);
		return new AsepriteFile(stream);
	}

	public static byte BYTE(BinaryReader reader) => reader.ReadByte();
	public static ushort WORD(BinaryReader reader) => reader.ReadUInt16();
	public static short SHORT(BinaryReader reader) => reader.ReadInt16();
	public static uint DWORD(BinaryReader reader) => reader.ReadUInt32();
	public static long LONG(BinaryReader reader) => reader.ReadInt32();

	public static void SEEK(BinaryReader reader, int numberOfBytes)
	{
		reader.BaseStream.Position += numberOfBytes;
	}

	public static byte[] BYTES(BinaryReader reader, int numberOfBytes) => reader.ReadBytes(numberOfBytes);

	public static string STRING(BinaryReader reader) => Encoding.UTF8.GetString(BYTES(reader, WORD(reader)));
}
