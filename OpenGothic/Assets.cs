using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Utility;
using OpenGothic.ZenKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGothic;

public partial class Assets
{
	private class RecordInfo
	{
		public string VfsName { get; }
		public Vfs Vfs { get; }
		public VfsNode Node { get; }
		public string Name => Node.Name;

		public RecordInfo(string vfsName, Vfs vfs, VfsNode node)
		{
			VfsName = vfsName;
			Vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
			Node = node ?? throw new ArgumentNullException(nameof(node));
		}
	}

	private readonly Dictionary<string, List<RecordInfo>> _allRecords =
		new Dictionary<string, List<RecordInfo>>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

	/// <summary>Textures whose stored format is DXT1, by every name they were asked for.</summary>
	private readonly HashSet<string> _dxt1Textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Parses a record with one of the managed ZenKit readers.</summary>
	private static T LoadRecord<T>(VfsNode node, Func<Read, T> parse)
	{
		return parse(node.OpenRead());
	}

	/// <summary>
	/// Parses a record with one of the native ZenKitCS readers that the managed port has not
	/// replaced yet. The bytes are pinned for the duration of the parse: ZenKitCS keeps a
	/// pointer into the array and does not pin it itself.
	/// </summary>
	private static T LoadNativeRecord<T>(VfsNode node, Func<global::ZenKit.Read, T> parse)
	{
		var data = node.ReadAllBytes();
		var handle = System.Runtime.InteropServices.GCHandle.Alloc(
			data, System.Runtime.InteropServices.GCHandleType.Pinned);
		try
		{
			return parse(new global::ZenKit.Read(data));
		}
		finally
		{
			handle.Free();
		}
	}

	/// <summary>
	/// Keeps a native owner alive for the rest of the process. ZenKit getters such as
	/// <c>Model.Hierarchy</c> and <c>Model.Mesh</c> return borrowed handles: the child wrapper
	/// does not own its native object and becomes invalid when the parent is finalized. These
	/// owners are the parent models behind such handles used beyond their parser call.
	/// </summary>
	private static readonly List<object> NativeOwners = new List<object>();

	private static T KeepNativeOwner<T>(T owner) where T : class
	{
		NativeOwners.Add(owner);
		return owner;
	}

	public string Folder { get; }

	public string[] Keys { get; }

	public Assets(string folder)
	{
		if (string.IsNullOrEmpty(folder))
		{
			throw new ArgumentNullException(nameof(folder));
		}

		Folder = folder;

		Load();

		Keys = (from k in _allRecords.Keys orderby k select k).ToArray();
	}

	private void AddRecordsRecursively(string vfsName, Vfs vfs, VfsNode vfsRoot)
	{
		if (vfsRoot.Name.Contains("."))
		{
			if (!_allRecords.TryGetValue(vfsRoot.Name, out var existingRecords))
			{
				existingRecords = new List<RecordInfo>();
				_allRecords[vfsRoot.Name] = existingRecords;
			}
			else
			{
				Log.Info($"Record '{vfsRoot.Name}' is already in the list. Previous vfs: {Path.GetFileName(existingRecords[0].VfsName)}");
			}

			var record = new RecordInfo(vfsName, vfs, vfsRoot);
			existingRecords.Add(record);
		}

		foreach (var child in vfsRoot.Children)
		{
			AddRecordsRecursively(vfsName, vfs, child);
		}
	}

	private void Load()
	{
		Log.Info($"Gothic Path: {Folder}");

		var dataFolder = Path.Combine(Folder, "Data");

		var vdfs = Directory.GetFiles(dataFolder, "*.vdf", SearchOption.AllDirectories);
		foreach (var vfsName in vdfs)
		{
			Log.Info($"Processing file {vfsName}");

			var vfs = new Vfs();

			vfs.MountDisk(vfsName, VfsOverwriteBehavior.Older);

			AddRecordsRecursively(vfsName, vfs, vfs.Root);
		}

		var recordCount = _allRecords.Values.Sum(records => records.Count);
		Log.Info($"Loaded {recordCount} records ({_allRecords.Count} unique names) from {vdfs.Length} VDF archives.");
	}

	private T Get<T>(GraphicsDevice device, string name, Func<GraphicsDevice, string, T> loader) where T : class
	{
		object obj;
		if (_cache.TryGetValue(name, out obj))
		{
			return (T)obj;
		}

		Log.Info($"Quering {typeof(T).Name} '{name}'");

		var result = loader(device, name);
		_cache[name] = result;

		return result;
	}

	/// <summary>
	/// Whether a material is drawn with alpha testing. This is the reference's
	/// <c>Material::loadAlphaFunc</c> classification copied over: only the unset alpha
	/// functions are cut out; blending, additive, multiply and water belong to other passes;
	/// and a DXT1 texture with an opaque material colour is solid. There is no scan of the
	/// alpha channel - the texture format is the only texture input.
	/// </summary>
	internal static bool IsAlphaTested(global::ZenKit.AlphaFunction alpha, global::ZenKit.MaterialGroup group,
		byte colorAlpha, bool hasTexture, bool dxt1)
	{
		switch (alpha)
		{
			case global::ZenKit.AlphaFunction.Blend:
			case global::ZenKit.AlphaFunction.Add:
			case global::ZenKit.AlphaFunction.Subtract:
			case global::ZenKit.AlphaFunction.Multiply:
			case global::ZenKit.AlphaFunction.MultiplyAlt:
				return false;
		}

		if (group == global::ZenKit.MaterialGroup.Water)
		{
			return false;
		}

		if (dxt1 && colorAlpha == 255)
		{
			return false;
		}

		// Without a texture the reference draws a solid colour: it is cut out only when that
		// colour itself is not opaque.
		if (!hasTexture)
		{
			return colorAlpha != 255;
		}

		return true;
	}

	/// <summary>Applies <see cref="IsAlphaTested(global::ZenKit.AlphaFunction, global::ZenKit.MaterialGroup, byte, bool, bool)"/>
	/// to a ZEN material; its texture must already be loaded so its format is known.</summary>
	internal bool IsAlphaTested(global::ZenKit.IMaterial material)
	{
		if (material == null)
		{
			return false;
		}

		var hasTexture = !string.IsNullOrEmpty(material.Texture);
		var dxt1 = hasTexture && _dxt1Textures.Contains(material.Texture);

		return IsAlphaTested(material.AlphaFunction, material.Group, material.Color.A, hasTexture, dxt1);
	}

	/// <summary>The managed-material counterpart of the native overload above.</summary>
	internal static bool IsAlphaTested(AlphaFunction alpha, MaterialGroup group, byte colorAlpha,
		bool hasTexture, bool dxt1)
	{
		switch (alpha)
		{
			case AlphaFunction.Blend:
			case AlphaFunction.Add:
			case AlphaFunction.Subtract:
			case AlphaFunction.Multiply:
			case AlphaFunction.MultiplyAlt:
				return false;
		}

		if (group == MaterialGroup.Water)
		{
			return false;
		}

		if (dxt1 && colorAlpha == 255)
		{
			return false;
		}

		if (!hasTexture)
		{
			return colorAlpha != 255;
		}

		return true;
	}

	/// <summary>Applies the cut-out rule to a managed world material.</summary>
	internal bool IsAlphaTested(Material material)
	{
		if (material == null)
		{
			return false;
		}

		var hasTexture = !string.IsNullOrEmpty(material.Texture);
		var dxt1 = hasTexture && _dxt1Textures.Contains(material.Texture);

		return IsAlphaTested(material.AlphaFunction, material.Group, material.Color.A, hasTexture, dxt1);
	}

	/// <summary>Whether the installed data contains a record with this exact name.</summary>
	public bool HasRecord(string name)
	{
		return !string.IsNullOrEmpty(name) && _allRecords.ContainsKey(name.ToUpperInvariant());
	}

	/// <summary>
	/// Reads a whole record as bytes. Used for audio files, which MonoGame's decoders take
	/// as a stream rather than through a ZenKit parser.
	/// </summary>
	public bool TryGetBytes(string name, out byte[] data)
	{
		data = null;
		if (string.IsNullOrEmpty(name) || !_allRecords.TryGetValue(name, out var records) ||
		    records.Count == 0)
		{
			return false;
		}

		data = records[records.Count - 1].Node.ReadAllBytes();
		return data != null && data.Length > 0;
	}

	private RecordInfo GetLastRecord(string name)
	{
		return _allRecords[name][^1];
	}

	private Texture2D LoadTexture(GraphicsDevice device, string name)
	{
		name = name.ToUpper();
		if (!_allRecords.TryGetValue(name, out var records))
		{
			// Add '-C' and .TEX ext
			var newName = Path.GetFileNameWithoutExtension(name);
			newName += "-C";
			newName = Path.ChangeExtension(newName, "TEX");
			if (!_allRecords.TryGetValue(newName, out records))
			{
				Log.Warning($"Unable to find texture '{name}', using a placeholder.");
				return null;
			}
		}

		var record = records[records.Count - 1];

		var texture = LoadRecord(record.Node, r => new OpenGothic.ZenKit.Texture(r));
		var mipmapCount = texture.MipmapCount;

		// Compressed formats upload directly; everything else (including palettized
		// formats) is decoded to RGBA by the managed texture reader.
		if (texture.Format.IsCompressed())
		{
			var result = new Texture2D(device, texture.Width, texture.Height, mipmapCount > 1, texture.Format.ToXNA());

			for (var i = 0; i < mipmapCount; ++i)
			{
				var data = texture.GetMipmapRaw(i);
				result.SetData(i, null, data, 0, data.Length);
			}

			MarkDxt1(name, record.Name, texture.Format);
			return result;
		}
		else
		{
			var result = new Texture2D(device, texture.Width, texture.Height, mipmapCount > 1, SurfaceFormat.Color);

			for (var i = 0; i < mipmapCount; ++i)
			{
				var data = texture.GetMipmapRgba(i);
				result.SetData(i, null, data, 0, data.Length);
			}

			MarkDxt1(name, record.Name, texture.Format);
			return result;
		}
	}

	/// <summary>
	/// Remembers DXT1 textures under the requested and the resolved name; the material
	/// classification turns them solid when the material colour is opaque.
	/// </summary>
	private void MarkDxt1(string requestedName, string resolvedName, TextureFormat format)
	{
		if (format != TextureFormat.Dxt1)
		{
			return;
		}

		_dxt1Textures.Add(requestedName);
		_dxt1Textures.Add(resolvedName);
	}


	public Texture2D GetTexture(GraphicsDevice device, string name) => Get(device, name, LoadTexture);
}
