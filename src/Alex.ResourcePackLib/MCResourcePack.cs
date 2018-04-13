﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Alex.API.World;
using Alex.ResourcePackLib.Json;
using Alex.ResourcePackLib.Json.BlockStates;
using Alex.ResourcePackLib.Json.Models;
using Alex.ResourcePackLib.Json.Models.Blocks;
using Alex.ResourcePackLib.Json.Models.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NLog;
using Color = Microsoft.Xna.Framework.Color;

namespace Alex.ResourcePackLib
{
	public class McResourcePack : IDisposable
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(McResourcePack));
		public ResourcePackInfo Info { get; private set; }

		public IReadOnlyDictionary<string, BlockStateResource> BlockStates => _blockStates;
		public IReadOnlyDictionary<string, BlockModel> BlockModels => _blockModels;
		public IReadOnlyDictionary<string, ResourcePackItem> ItemModels => _itemModels;
		public IReadOnlyDictionary<string, Bitmap> Textures => _textureCache;

	//	private ZipArchive _archive;
		private readonly Dictionary<string, BlockStateResource> _blockStates = new Dictionary<string, BlockStateResource>();
		private readonly Dictionary<string, BlockModel> _blockModels = new Dictionary<string, BlockModel>();
		private readonly Dictionary<string, ResourcePackItem> _itemModels = new Dictionary<string, ResourcePackItem>();
		private readonly Dictionary<string, Bitmap> _textureCache = new Dictionary<string, Bitmap>();

		private Color[] FoliageColors { get; set; } = null;
		private int _foliageWidth = 256;
		private int _foliageHeight = 256;

		private Color[] GrassColors { get; set; } = null;
		private int _grassHeight = 256;
		private int _grassWidth = 256;

		public McResourcePack(byte[] resourcePackData, GraphicsDevice graphicsDevice) : this(new ZipArchive(new MemoryStream(resourcePackData), ZipArchiveMode.Read, false), graphicsDevice)
		{

		}

		public McResourcePack(ZipArchive archive, GraphicsDevice graphicsDevice)
		{
			//_archive = archive;
			Load(archive, graphicsDevice);
		}

		public Color GetGrassColor(float temp, float rain, int elevation)
		{
			if (GrassColors == null) return new Color(94, 157, 52);

			temp = MathHelper.Clamp(temp - elevation * 0.00166667f, 0f, 1f);
			rain = MathHelper.Clamp(rain, 0f, 1f) * temp;

			int x = (int)Math.Floor(MathHelper.Clamp(_grassWidth - (_grassWidth * temp), 0, _grassWidth));
			int y = (int)Math.Floor(MathHelper.Clamp(_grassHeight - (_grassHeight * rain), 0, _grassHeight));

			var indx = _grassWidth * y + x;

			if (indx < 0) indx = 0;
			if (indx > GrassColors.Length - 1) indx = GrassColors.Length - 1;
			
			var result = GrassColors[indx];

			return new Color(result.R, result.G, result.B);
		}

		public Color GetFoliageColor(float temp, float rain, int elevation)
		{
			if (FoliageColors == null) return new Color(94, 157, 52);
			temp = MathHelper.Clamp(temp - elevation * 0.00166667f, 0f, 1f);
			rain = MathHelper.Clamp(rain, 0f, 1f) * temp;

			int x = (int)Math.Floor(MathHelper.Clamp(_foliageWidth - (_foliageWidth * temp), 0, _foliageWidth));
			int y = (int)Math.Floor(MathHelper.Clamp(_foliageHeight - (_foliageHeight * rain), 0, _foliageHeight));

			var indx = _foliageWidth * y + x;

			if (indx < 0) indx = 0;
			if (indx > FoliageColors.Length - 1) indx = FoliageColors.Length - 1;

			var result = FoliageColors[indx];

			return new Color(result.R, result.G, result.B);
		}

		private static readonly Regex IsTextureResource = new Regex(@"assets\/(?'namespace'.*)\/textures\/(?'filename'.*)\.png$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex IsModelRegex = new Regex(@"assets\/(?'namespace'.*)\/models\/(?'filename'.*)\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex IsBlockStateRegex = new Regex(@"assets\/(?'namespace'.*)\/blockstates\/(?'filename'.*)\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex IsGlyphSizes = new Regex(@"assets\/(?'namespace'.*)\/font\/glyph_sizes.bin$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private void Load(ZipArchive archive, GraphicsDevice graphicsDevice)
		{
			LoadMeta(archive);

			Dictionary<string, BlockModel> models = new Dictionary<string, BlockModel>();
			foreach (var entry in archive.Entries)
			{
				var textureMatchs = IsTextureResource.Match(entry.FullName);
				if (textureMatchs.Success)
				{
					LoadTexture(entry, textureMatchs);
					continue;
				}

				var modelMatch = IsModelRegex.Match(entry.FullName);
				if (modelMatch.Success)
				{
					var fileName = modelMatch.Groups["filename"].Value;
					if (fileName.StartsWith("block"))
					{
						var model = LoadBlockModel(entry, modelMatch);
						models.Add($"{model.Namespace}:{model.Name}", model);
					}
					else if (fileName.StartsWith("item"))
					{
						LoadItemModel(entry, modelMatch);
					}

					continue;
				}

				var blockStateMatch = IsBlockStateRegex.Match(entry.FullName);
				if (blockStateMatch.Success)
				{
					LoadBlockState(entry, blockStateMatch);
					continue;
				}

				var glyphSizeMatch = IsGlyphSizes.Match(entry.FullName);
				if (glyphSizeMatch.Success)
				{
					LoadGlyphSizes(entry);
					continue;
				}
			}

			foreach (var blockModel in models)
			{
				if (!_blockModels.ContainsKey(blockModel.Key))
					ProcessBlockModel(blockModel.Value, ref models);
			}

			foreach (var itemModel in _itemModels.ToArray())
			{
				_itemModels[itemModel.Key] = ProcessItem(itemModel.Value);
			}

			foreach (var blockState in _blockStates.ToArray())
			{
				_blockStates[blockState.Key] = ProcessBlockState(blockState.Value);
			}

			LoadColormap();

			LoadFonts(graphicsDevice);
		}


		private byte[] GlyphWidth = null;
		private void LoadFonts(GraphicsDevice graphicsDevice)
		{
			if (TryGetTexture("font/ascii", out Bitmap asciiTexture))
			{
				AsciiFont = LoadFont(graphicsDevice, asciiTexture, false);
			}
		}
		private static Texture2D BitmapToTexture2D(GraphicsDevice device, Bitmap bmp)
		{
			uint[] imgData = new uint[bmp.Width * bmp.Height];
			Texture2D texture = new Texture2D(device, bmp.Width, bmp.Height);

			unsafe
			{
				BitmapData origdata =
					bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);

				uint* byteData = (uint*)origdata.Scan0;

				for (int i = 0; i < imgData.Length; i++)
				{
					var val = byteData[i];
					imgData[i] = (val & 0x000000FF) << 16 | (val & 0x0000FF00) | (val & 0x00FF0000) >> 16 | (val & 0xFF000000);
				}

				byteData = null;

				bmp.UnlockBits(origdata);
			}

			texture.SetData(imgData);

			return texture;
		}

		public FontRenderer AsciiFont { get; private set; } = null;
		private FontRenderer LoadFont(GraphicsDevice graphicsDevice, Bitmap fontTexture, bool unicode)
		{

			return new FontRenderer(unicode, BitmapToTexture2D(graphicsDevice, fontTexture), GlyphWidth);
		}

		private void LoadGlyphSizes(ZipArchiveEntry entry)
		{
			byte[] glyphWidth = new byte[65536];
			using (Stream stream = entry.Open())
			{
				int length = stream.Read(glyphWidth, 0, glyphWidth.Length);
				Array.Resize(ref glyphWidth, length);
			}

			GlyphWidth = glyphWidth;
		}

		private void LoadTexture(ZipArchiveEntry entry, Match match)
		{
			Bitmap img;
			using (var s = entry.Open())
			{
				img = new Bitmap(s);
			}

			_textureCache[match.Groups["filename"].Value] = img;
		}

		private void LoadItemModel(ZipArchiveEntry entry, Match match)
		{
			string name = match.Groups["filename"].Value;
			string nameSpace = match.Groups["namespace"].Value;

			using (var r = new StreamReader(entry.Open()))
			{
				var blockModel = MCJsonConvert.DeserializeObject<ResourcePackItem>(r.ReadToEnd());
				blockModel.Name = name;
				blockModel.Namespace = nameSpace;

				//blockModel = ProcessItem(blockModel);
				_itemModels[$"{nameSpace}:{name}"] = blockModel;
			}

		}

		private ResourcePackItem ProcessItem(ResourcePackItem model)
		{
			if (!string.IsNullOrWhiteSpace(model.Parent) && !model.Parent.Equals(model.Name, StringComparison.InvariantCultureIgnoreCase))
			{
				
			}

			return model;
		}

		private void LoadColormap()
		{
			if (TryGetTexture("colormap/foliage", out Bitmap foliage))
			{
				var foliageColors = new LockBitmap(foliage);
				foliageColors.LockBits();
				FoliageColors = foliageColors.GetColorArray();
				foliageColors.UnlockBits();

				_foliageHeight = foliageColors.Height;
				_foliageWidth = foliageColors.Width;
			}

			if (TryGetTexture("colormap/grass", out Bitmap grass))
			{
				var grassColors = new LockBitmap(grass);
				grassColors.LockBits();
				GrassColors = grassColors.GetColorArray();
				grassColors.UnlockBits();

				_grassWidth = grassColors.Width;
				_grassHeight = grassColors.Height;
			}
		}

		private void LoadMeta(ZipArchive archive)
		{
			ResourcePackInfo info;

			var entry = archive.GetEntry("pack.mcmeta");
			if (entry == null)
			{
				info = new ResourcePackInfo();
			}
			else
			{
				using (TextReader reader = new StreamReader(entry.Open()))
				{
					ResourcePackInfoWrapper wrap = MCJsonConvert.DeserializeObject<ResourcePackInfoWrapper>(reader.ReadToEnd());
					info = wrap.pack;
				}
			}


			var imgEntry = archive.GetEntry("pack.png");
			if (imgEntry != null)
			{
				Bitmap bmp = new Bitmap(imgEntry.Open());
				info.Logo = bmp;
			}
		}

		private BlockModel LoadBlockModel(ZipArchiveEntry entry, Match match)
		{
			string name = match.Groups["filename"].Value;
			string nameSpace = match.Groups["namespace"].Value;

			using (var r = new StreamReader(entry.Open()))
			{
				var blockModel = MCJsonConvert.DeserializeObject<BlockModel>(r.ReadToEnd());
				blockModel.Name = name.Replace("block/", "");
				blockModel.Namespace = nameSpace;
				if (blockModel.ParentName != null)
				{
					blockModel.ParentName = blockModel.ParentName.Replace("block/", "");
				}
				//blockModel = ProcessBlockModel(blockModel);
				//_blockModels[$"{nameSpace}:{name}"] = blockModel;
				return blockModel;
			}
		}

		public bool TryGetBlockModel(string modelName, out BlockModel model)
		{
			string @namespace = DefaultNamespace;
			var split = modelName.Split(':');
			if (split.Length > 1)
			{
				@namespace = split[0];
				modelName = split[1];
			}

			return TryGetBlockModel(@namespace, modelName, out model);
		}

		public bool TryGetBlockModel(string @namespace, string modelName, out BlockModel model)
		{
			string fullName = $"{@namespace}:{modelName}";

			if (_blockModels.TryGetValue(fullName, out model))
				return true;

			var m = _blockModels.FirstOrDefault(x => x.Value.Name.EndsWith(modelName, StringComparison.InvariantCultureIgnoreCase))
				.Value;

			if (m != null)
			{
				model = m;
				return true;
			}

			model = null;
			return false;
		}

		private void LoadBlockState(ZipArchiveEntry entry, Match match)
		{
			try
			{
				string name = match.Groups["filename"].Value;
				string nameSpace = match.Groups["namespace"].Value;

				using (var r = new StreamReader(entry.Open()))
				{
					var json = r.ReadToEnd();

					var blockState = MCJsonConvert.DeserializeObject<BlockStateResource>(json);
					blockState.Name = name;
					blockState.Namespace = nameSpace;

					
					_blockStates[$"{nameSpace}:{name}"] = blockState;

					//return blockState;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"Could not load {entry.Name}!", ex);
			//	return null;
			}
		}

		public bool TryGetBlockState(string modelName, out BlockStateResource stateResource)
		{
			if (_blockStates.TryGetValue(modelName, out stateResource))
				return true;

			stateResource = null;
			return false;
		}

		public bool TryGetTexture(BlockModel model, string textureName, out Bitmap texture)
		{
			while (textureName.StartsWith("#"))
			{
				if (!model.Textures.TryGetValue(textureName.TrimStart('#'), out textureName))
				{
					texture = null;
					return false;
				}
			}

			if (_textureCache.TryGetValue(textureName, out texture))
				return true;

			texture = null;
			return false;
		}

		public bool TryGetTexture(string textureName, out Bitmap texture)
		{
			if (_textureCache.TryGetValue(textureName, out texture))
				return true;

			texture = null;
			return false;
		}
		
		private BlockModel ProcessBlockModel(BlockModel model, ref Dictionary<string, BlockModel> models)
		{
			string key = $"{model.Namespace}:{model.Name}";
			if (!string.IsNullOrWhiteSpace(model.ParentName) && !model.ParentName.Equals(model.Name, StringComparison.InvariantCultureIgnoreCase))
			{
				string parentKey = $"{model.Namespace}:{model.ParentName}";

				BlockModel parent;
				if (!_blockModels.TryGetValue(parentKey, out parent))
				{
					if (models.TryGetValue(parentKey, out parent))
					{
						parent = ProcessBlockModel(parent, ref models);
					}
				}

				if (parent != null)
				{
					model.Parent = parent;

					if (model.Elements.Length == 0 && parent.Elements.Length > 0)
					{
						model.Elements = (BlockModelElement[])parent.Elements.Clone();
					}

					foreach (var kvp in parent.Textures)
					{
						if (!model.Textures.ContainsKey(kvp.Key))
						{
							model.Textures.Add(kvp.Key, kvp.Value);
						}
					}
				}
			}

			_blockModels.Add(key, model);

			return model;
		}

		private const string DefaultNamespace = "minecraft";
		private BlockStateResource ProcessBlockState(BlockStateResource blockStateResource)
		{
			if (blockStateResource.Parts.Length > 0)
			{
				foreach (var part in blockStateResource.Parts)
				{
					foreach (var sVariant in part.Apply)
					{
						if (!TryGetBlockModel(sVariant.ModelName, out BlockModel model))
						{
							Log.Debug($"Could not get multipart blockmodel! Variant: {blockStateResource} Model: {sVariant.ModelName}");
							continue;
						}

						sVariant.Model = model;
					}
				}
			}
			else
			{
				foreach (var variant in blockStateResource.Variants)
				{
					foreach (var sVariant in variant.Value)
					{
						if (!TryGetBlockModel(sVariant.ModelName, out BlockModel model))
						{
							Log.Debug($"Could not get blockmodel for variant! Variant: {variant.Key} Model: {sVariant.ModelName}");
							continue;
						}

						sVariant.Model = model;
					}
				}
			}

			return blockStateResource;
		}

		public void Dispose()
		{
			//_archive?.Dispose();
		}
	}

	internal class LockBitmap
	{
		Bitmap _source = null;
		IntPtr _iptr = IntPtr.Zero;
		BitmapData _bitmapData = null;

		public byte[] Pixels { get; set; }
		public int Depth { get; private set; }
		public int Width { get; private set; }
		public int Height { get; private set; }

		public LockBitmap(Bitmap source)
		{
			this._source = source;
		}

		/// <summary>
		/// Lock bitmap data
		/// </summary>
		public void LockBits()
		{
			try
			{
				// Get width and height of bitmap
				Width = _source.Width;
				Height = _source.Height;

				// get total locked pixels count
				int pixelCount = Width * Height;

				// Create rectangle to lock
				System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, Width, Height);

				// get source bitmap pixel format size
				Depth = System.Drawing.Bitmap.GetPixelFormatSize(_source.PixelFormat);

				// Check if bpp (Bits Per Pixel) is 8, 24, or 32
				if (Depth != 8 && Depth != 24 && Depth != 32)
				{
					throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
				}

				// Lock bitmap and return bitmap data
				_bitmapData = _source.LockBits(rect, ImageLockMode.ReadWrite,
											 _source.PixelFormat);

				// create byte array to copy pixel values
				int step = Depth / 8;
				Pixels = new byte[pixelCount * step];
				_iptr = _bitmapData.Scan0;

				// Copy data from pointer to array
				Marshal.Copy(_iptr, Pixels, 0, Pixels.Length);
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		/// <summary>
		/// Unlock bitmap data
		/// </summary>
		public void UnlockBits()
		{
			try
			{
				// Copy data from byte array to pointer
				Marshal.Copy(Pixels, 0, _iptr, Pixels.Length);

				// Unlock bitmap data
				_source.UnlockBits(_bitmapData);
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		/// <summary>
		/// Get the color of the specified pixel
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public Color GetPixel(int x, int y)
		{
			Color clr = Color.White;

			// Get color components count
			int cCount = Depth / 8;

			// Get start index of the specified pixel
			int i = ((y * Width) + x) * cCount;

			if (i > Pixels.Length - cCount)
				throw new IndexOutOfRangeException();

			if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
			{
				byte b = Pixels[i];
				byte g = Pixels[i + 1];
				byte r = Pixels[i + 2];
				byte a = Pixels[i + 3]; // a
				clr = new Color(r,g,b,a);
			}
			if (Depth == 24) // For 24 bpp get Red, Green and Blue
			{
				byte b = Pixels[i];
				byte g = Pixels[i + 1];
				byte r = Pixels[i + 2];
				clr =  new Color(r, g, b);
			}
			if (Depth == 8)
			// For 8 bpp get color value (Red, Green and Blue values are the same)
			{
				byte c = Pixels[i];
				clr = new Color(c, c, c);
			}
			return clr;
		}

		/// <summary>
		/// Set the color of the specified pixel
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color"></param>
		public void SetPixel(int x, int y, Color color)
		{
			// Get color components count
			int cCount = Depth / 8;

			// Get start index of the specified pixel
			int i = ((y * Width) + x) * cCount;

			if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
			{
				Pixels[i] = color.B;
				Pixels[i + 1] = color.G;
				Pixels[i + 2] = color.R;
				Pixels[i + 3] = color.A;
			}
			if (Depth == 24) // For 24 bpp set Red, Green and Blue
			{
				Pixels[i] = color.B;
				Pixels[i + 1] = color.G;
				Pixels[i + 2] = color.R;
			}
			if (Depth == 8)
			// For 8 bpp set color value (Red, Green and Blue values are the same)
			{
				Pixels[i] = color.B;
			}
		}

		public Color[] GetColorArray()
		{
			Color[] colors = new Color[Width * Height];
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					var indx = Width * y + x;
					colors[indx] = GetPixel(x, y);
				}
			}

			return colors;
		}
	}
}
