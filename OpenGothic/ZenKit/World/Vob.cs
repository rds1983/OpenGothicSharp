using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenGothic.ZenKit;

public enum VirtualObjectType
{
	Unknown = -1,
	zCVob = 0,
	zCVobLevelCompo = 1,
	oCItem = 2,
	oCNpc = 3,
	zCMoverController = 4,
	zCVobScreenFX = 5,
	zCVobStair = 6,
	zCPFXController = 7,
	zCVobAnimate = 8,
	zCVobLensFlare = 9,
	zCVobLight = 10,
	zCVobSpot = 11,
	zCVobStartpoint = 12,
	zCMessageFilter = 13,
	zCCodeMaster = 14,
	zCTriggerWorldStart = 15,
	zCCSCamera = 16,
	zCCamTrj_KeyFrame = 17,
	oCTouchDamage = 18,
	zCTriggerUntouch = 19,
	zCEarthquake = 20,
	oCMOB = 21,
	oCMobInter = 22,
	oCMobBed = 23,
	oCMobFire = 24,
	oCMobLadder = 25,
	oCMobSwitch = 26,
	oCMobWheel = 27,
	oCMobContainer = 28,
	oCMobDoor = 29,
	zCTrigger = 30,
	zCTriggerList = 31,
	oCTriggerScript = 32,
	oCTriggerChangeLevel = 33,
	oCCSTrigger = 34,
	zCMover = 35,
	zCVobSound = 36,
	zCVobSoundDaytime = 37,
	oCZoneMusic = 38,
	oCZoneMusicDefault = 39,
	zCZoneZFog = 40,
	zCZoneZFogDefault = 41,
	zCZoneVobFarPlane = 42,
	zCZoneVobFarPlaneDefault = 43
}

public enum VisualType
{
	Decal = 0,
	Mesh = 1,
	MultiResolutionMesh = 2,
	ParticleEffect = 3,
	AiCamera = 4,
	Model = 5,
	MorphMesh = 6,
	Unknown = 7
}

public enum SpriteAlignment : byte
{
	None = 0,
	Yaw = 1,
	Full = 2
}

public enum ShadowType : byte
{
	None = 0,
	Blob = 1
}

public enum AnimationType : byte
{
	None = 0,
	Wind = 1,
	WindAlt = 2
}

public struct Box3
{
	public Box3(Vector3 min, Vector3 max)
	{
		Min = min;
		Max = max;
	}

	public Vector3 Min { get; set; }

	public Vector3 Max { get; set; }

	public static Box3 Zero => new Box3(Vector3.Zero, Vector3.Zero);
}

/// <summary>Row-major 3x3 matrix; matches ZenKit's rotated vob transform layout.</summary>
public struct Matrix3x3
{
	public Matrix3x3(float m11, float m12, float m13,
		float m21, float m22, float m23,
		float m31, float m32, float m33)
	{
		M11 = m11;
		M12 = m12;
		M13 = m13;
		M21 = m21;
		M22 = m22;
		M23 = m23;
		M31 = m31;
		M32 = m32;
		M33 = m33;
	}

	public float M11;
	public float M12;
	public float M13;
	public float M21;
	public float M22;
	public float M23;
	public float M31;
	public float M32;
	public float M33;

	public static Matrix3x3 Identity => new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);

	/// <summary>Builds the managed layout from the nine floats stored in the file.</summary>
	internal static Matrix3x3 FromFileOrder(Read reader)
	{
		var values = new float[9];
		for (var i = 0; i < values.Length; ++i)
		{
			values[i] = reader.ReadFloat();
		}

		return new Matrix3x3(
			values[0], values[1], values[2],
			values[3], values[4], values[5],
			values[6], values[7], values[8]);
	}
}

public class Visual
{
	public VisualType Type { get; internal set; }

	public string Name { get; internal set; } = string.Empty;
}

public sealed class VisualModel : Visual
{
}

public sealed class VisualMultiResolutionMesh : Visual
{
}

public sealed class VisualMesh : Visual
{
}

public sealed class VisualParticleEffect : Visual
{
}

public sealed class VisualDecal : Visual
{
}

public sealed class VisualCamera : Visual
{
}

public sealed class VisualMorphMesh : Visual
{
}

public interface IVirtualObject
{
	VirtualObjectType Type { get; }

	int Id { get; }

	string Name { get; }

	string PresetName { get; }

	Visual Visual { get; }

	Box3 BoundingBox { get; }

	Vector3 Position { get; }

	Matrix3x3 Rotation { get; }

	IReadOnlyList<VirtualObject> Children { get; }

	bool ShowVisual { get; }

	bool CdDynamic { get; }

	string VisualName { get; }
}

public interface INpc
{
	string NpcInstance { get; }

	int Guild { get; }

	int Level { get; }

	IReadOnlyList<int> Attributes { get; }
}

public interface IItem
{
	string Instance { get; }

	int Amount { get; }
}

public interface IInteractiveObject
{
	int State { get; }

	string Target { get; }

	string Item { get; }

	string ConditionFunction { get; }

	string OnStateChangeFunction { get; }

	bool Rewind { get; }
}

public interface IContainer : IInteractiveObject
{
	bool IsLocked { get; }

	string Key { get; }

	string PickString { get; }

	string Contents { get; }

	int ItemCount { get; }
}

public interface IDoor : IInteractiveObject
{
	bool IsLocked { get; }

	string Key { get; }

	string PickString { get; }
}

/// <summary>
/// One ZEN virtual object. The reader decodes the packed common block and leaves the
/// type-specific fields to the subclasses.
/// </summary>
public class VirtualObject : IVirtualObject
{
	private readonly List<VirtualObject> _children = new List<VirtualObject>();

	public VirtualObjectType Type { get; internal set; }

	public uint Index { get; internal set; }

	public int Id => (int)Index;

	public string VobName { get; internal set; } = string.Empty;

	public string Name => VobName;

	public string PresetName { get; internal set; } = string.Empty;

	public string VisualName { get; internal set; } = string.Empty;

	public Visual Visual { get; internal set; } = new Visual { Type = VisualType.Unknown };

	public Box3 BoundingBox { get; internal set; } = Box3.Zero;

	public Vector3 Position { get; internal set; }

	public Matrix3x3 Rotation { get; internal set; } = Matrix3x3.Identity;

	public bool ShowVisual { get; internal set; }

	public bool CdStatic { get; internal set; }

	public bool CdDynamic { get; internal set; }

	public bool VobStatic { get; internal set; }

	public bool PhysicsEnabled { get; internal set; }

	public bool Ambient { get; internal set; }

	public SpriteAlignment SpriteCameraFacingMode { get; internal set; }

	public ShadowType DynamicShadows { get; internal set; }

	public AnimationType AnimationType { get; internal set; }

	public float AnimationStrength { get; internal set; }

	public float FarClipScale { get; internal set; } = 1f;

	public int Bias { get; internal set; }

	public IReadOnlyList<VirtualObject> Children => _children;

	internal virtual void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		Index = source.Index;
		PresetName = source.GetString("presetName");
		VobName = source.GetString("vobName");
		VisualName = source.GetString("visual");

		if (source.GetInt("pack") != 0)
		{
			ParsePacked(source.GetRaw("dataRaw"));
		}
		else
		{
			ParseUnpacked(source);
		}

		if (source.Has("presetName"))
		{
			PresetName = source.GetString("presetName");
		}

		if (source.Has("vobName"))
		{
			VobName = source.GetString("vobName");
		}

		if (source.Has("visual"))
		{
			VisualName = source.GetString("visual");
		}

		var visualChild = FindVisual(source, objects);
		if (visualChild != null)
		{
			var className = VobReader.ClassName(visualChild, objects);
			Visual = CreateVisual(className, VisualName);
		}
		else
		{
			Visual = new Visual { Type = VisualType.Unknown, Name = VisualName };
		}
	}

	internal void AddChild(VirtualObject child)
	{
		_children.Add(child);
	}

	private void ParsePacked(byte[] raw)
	{
		if (raw == null || raw.Length < 74)
		{
			return;
		}

		var reader = new MemoryRead(raw);
		var min = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
		var max = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
		BoundingBox = new Box3(min, max);
		Position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
		Rotation = Matrix3x3.FromFileOrder(reader);

		var bit0 = reader.ReadUByte();
		var gothic2 = raw.Length >= 83;
		var bit1 = gothic2 ? reader.ReadUShort() : reader.ReadUByte();

		ShowVisual = (bit0 & 0x01) != 0;
		SpriteCameraFacingMode = (SpriteAlignment)((bit0 >> 1) & 0x03);
		CdStatic = (bit0 & 0x08) != 0;
		CdDynamic = (bit0 & 0x10) != 0;
		VobStatic = (bit0 & 0x20) != 0;
		DynamicShadows = (ShadowType)((bit0 >> 6) & 0x03);

		PhysicsEnabled = (bit1 & (1 << 6)) != 0;
		if (gothic2)
		{
			AnimationType = (AnimationType)((bit1 >> 7) & 0x03);
			Bias = (bit1 >> 9) & 0x1F;
			Ambient = (bit1 & (1 << 14)) != 0;
			AnimationStrength = reader.ReadFloat();
			FarClipScale = reader.ReadFloat();
		}
	}

	private void ParseUnpacked(ZenObject source)
	{
		var bounds = source.Get("bbox3DWS");
		if (bounds?.Raw != null && bounds.Raw.Length >= 24)
		{
			var reader = new MemoryRead(bounds.Raw);
			var min = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
			var max = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
			BoundingBox = new Box3(min, max);
		}

		var rotation = source.Get("trafoOSToWSRot");
		if (rotation?.Raw != null && rotation.Raw.Length >= 36)
		{
			Rotation = Matrix3x3.FromFileOrder(new MemoryRead(rotation.Raw));
		}

		Position = source.GetVec3("trafoOSToWSPos");
		ShowVisual = source.GetBool("showVisual");
		SpriteCameraFacingMode = (SpriteAlignment)source.GetInt("visualCamAlign");
		AnimationType = (AnimationType)source.GetInt("visualAniMode");
		AnimationStrength = source.GetFloat("visualAniModeStrength");
		FarClipScale = source.GetFloat("vobFarClipZScale", 1f);
		CdStatic = source.GetBool("cdStatic");
		CdDynamic = source.GetBool("cdDyn");
		VobStatic = source.GetBool("staticVob");
		DynamicShadows = (ShadowType)source.GetInt("dynShadow");
		Bias = source.GetInt("zbias");
		Ambient = source.GetBool("isAmbient");
		PhysicsEnabled = source.GetBool("physicsEnabled");
	}

	private static ZenObject FindVisual(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		foreach (var child in source.Children)
		{
			if (child.Name == "visual" || IsVisualClass(VobReader.ClassName(child, objects)))
			{
				return child;
			}
		}

		return null;
	}

	private static bool IsVisualClass(string className)
	{
		return className is "zCDecal" or "zCMesh" or "zCProgMeshProto" or "zCParticleFX" or
			"zCAICamera" or "zCModel" or "zCMorphMesh";
	}

	private static Visual CreateVisual(string className, string name)
	{
		Visual visual = className switch
		{
			"zCDecal" => new VisualDecal { Type = VisualType.Decal },
			"zCMesh" => new VisualMesh { Type = VisualType.Mesh },
			"zCProgMeshProto" => new VisualMultiResolutionMesh { Type = VisualType.MultiResolutionMesh },
			"zCParticleFX" => new VisualParticleEffect { Type = VisualType.ParticleEffect },
			"zCAICamera" => new VisualCamera { Type = VisualType.AiCamera },
			"zCModel" => new VisualModel { Type = VisualType.Model },
			"zCMorphMesh" => new VisualMorphMesh { Type = VisualType.MorphMesh },
			_ => new Visual { Type = VisualType.Unknown }
		};

		visual.Name = name;
		return visual;
	}
}

/// <summary>A vob with a focus name and optional physics properties (oCMOB).</summary>
public class MovableObject : VirtualObject
{
	public string FocusName { get; internal set; } = string.Empty;

	public int HitPoints { get; internal set; }

	public int Damage { get; internal set; }

	public bool Moveable { get; internal set; }

	public bool Takeable { get; internal set; }

	public bool FocusOverride { get; internal set; }

	public string VisualDestroyed { get; internal set; } = string.Empty;

	public string Owner { get; internal set; } = string.Empty;

	public string OwnerGuild { get; internal set; } = string.Empty;

	public bool Destroyed { get; internal set; }

	internal override void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		base.Parse(source, objects);
		FocusName = source.GetString("focusName");
		HitPoints = source.GetInt("hitpoints");
		Damage = source.GetInt("damage");
		Moveable = source.GetBool("moveable");
		Takeable = source.GetBool("takeable");
		FocusOverride = source.GetBool("focusOverride");
		VisualDestroyed = source.GetString("visualDestroyed");
		Owner = source.GetString("owner");
		OwnerGuild = source.GetString("ownerGuild");
		Destroyed = source.GetBool("isDestroyed");
	}
}

/// <summary>A door, chest, switch, wheel or other mobsi.</summary>
public class InteractiveObject : MovableObject, IInteractiveObject
{
	public int State { get; internal set; }

	public string Target { get; internal set; } = string.Empty;

	public string Item { get; internal set; } = string.Empty;

	public string ConditionFunction { get; internal set; } = string.Empty;

	public string OnStateChangeFunction { get; internal set; } = string.Empty;

	public bool Rewind { get; internal set; }

	internal override void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		base.Parse(source, objects);
		State = source.GetInt("stateNum");
		Target = source.GetString("triggerTarget");
		Item = source.GetString("useWithItem");
		ConditionFunction = source.GetString("conditionFunc");
		OnStateChangeFunction = source.GetString("onStateFunc");
		Rewind = source.GetBool("rewind");
	}
}

public sealed class ContainerObject : InteractiveObject, IContainer
{
	public bool IsLocked { get; internal set; }

	public string Key { get; internal set; } = string.Empty;

	public string PickString { get; internal set; } = string.Empty;

	public string Contents { get; internal set; } = string.Empty;

	public int ItemCount => 0;

	internal override void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		base.Parse(source, objects);
		IsLocked = source.GetBool("locked");
		Key = source.GetString("keyInstance");
		PickString = source.GetString("pickLockStr");
		Contents = source.GetString("contains");
	}
}

public sealed class DoorObject : InteractiveObject, IDoor
{
	public bool IsLocked { get; internal set; }

	public string Key { get; internal set; } = string.Empty;

	public string PickString { get; internal set; } = string.Empty;

	internal override void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		base.Parse(source, objects);
		IsLocked = source.GetBool("locked");
		Key = source.GetString("keyInstance");
		PickString = source.GetString("pickLockStr");
	}
}

public sealed class ItemObject : VirtualObject, IItem
{
	public string Instance { get; internal set; } = string.Empty;

	public int Amount { get; internal set; } = 1;

	internal override void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		base.Parse(source, objects);
		Instance = source.GetString("itemInstance");
		if (source.Has("amount"))
		{
			Amount = Math.Max(1, source.GetInt("amount", 1));
		}
	}
}

public sealed class NpcObject : VirtualObject, INpc
{
	public string NpcInstance { get; internal set; } = string.Empty;

	public int Guild { get; internal set; }

	public int Level { get; internal set; } = 1;

	public IReadOnlyList<int> Attributes { get; internal set; } = Array.Empty<int>();

	internal override void Parse(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		base.Parse(source, objects);
		NpcInstance = source.GetString("npcInstance");
		Guild = source.GetInt("guild");
		Level = Math.Max(1, source.GetInt("level", 1));
	}
}

/// <summary>Maps ZEN class names and the generic archive objects to the managed vob tree.</summary>
internal static class VobReader
{
	private static readonly Dictionary<string, VirtualObjectType> Types =
		new Dictionary<string, VirtualObjectType>(StringComparer.OrdinalIgnoreCase)
		{
			["zCVob"] = VirtualObjectType.zCVob,
			["zCVobLevelCompo"] = VirtualObjectType.zCVobLevelCompo,
			["oCItem"] = VirtualObjectType.oCItem,
			["oCNpc"] = VirtualObjectType.oCNpc,
			["zCMoverController"] = VirtualObjectType.zCMoverController,
			["zCVobScreenFX"] = VirtualObjectType.zCVobScreenFX,
			["zCVobStair"] = VirtualObjectType.zCVobStair,
			["zCPFXController"] = VirtualObjectType.zCPFXController,
			["zCPFXControler"] = VirtualObjectType.zCPFXController,
			["zCVobAnimate"] = VirtualObjectType.zCVobAnimate,
			["zCVobLensFlare"] = VirtualObjectType.zCVobLensFlare,
			["zCVobLight"] = VirtualObjectType.zCVobLight,
			["zCVobSpot"] = VirtualObjectType.zCVobSpot,
			["zCVobStartpoint"] = VirtualObjectType.zCVobStartpoint,
			["zCMessageFilter"] = VirtualObjectType.zCMessageFilter,
			["zCCodeMaster"] = VirtualObjectType.zCCodeMaster,
			["zCTriggerWorldStart"] = VirtualObjectType.zCTriggerWorldStart,
			["zCCSCamera"] = VirtualObjectType.zCCSCamera,
			["zCCamTrj_KeyFrame"] = VirtualObjectType.zCCamTrj_KeyFrame,
			["oCTouchDamage"] = VirtualObjectType.oCTouchDamage,
			["zCTriggerUntouch"] = VirtualObjectType.zCTriggerUntouch,
			["zCEarthquake"] = VirtualObjectType.zCEarthquake,
			["oCMOB"] = VirtualObjectType.oCMOB,
			["oCMobInter"] = VirtualObjectType.oCMobInter,
			["oCMobBed"] = VirtualObjectType.oCMobBed,
			["oCMobFire"] = VirtualObjectType.oCMobFire,
			["oCMobLadder"] = VirtualObjectType.oCMobLadder,
			["oCMobSwitch"] = VirtualObjectType.oCMobSwitch,
			["oCMobWheel"] = VirtualObjectType.oCMobWheel,
			["oCMobContainer"] = VirtualObjectType.oCMobContainer,
			["oCMobDoor"] = VirtualObjectType.oCMobDoor,
			["zCTrigger"] = VirtualObjectType.zCTrigger,
			["zCTriggerList"] = VirtualObjectType.zCTriggerList,
			["oCTriggerScript"] = VirtualObjectType.oCTriggerScript,
			["oCTriggerChangeLevel"] = VirtualObjectType.oCTriggerChangeLevel,
			["oCCSTrigger"] = VirtualObjectType.oCCSTrigger,
			["zCMover"] = VirtualObjectType.zCMover,
			["zCVobSound"] = VirtualObjectType.zCVobSound,
			["zCVobSoundDaytime"] = VirtualObjectType.zCVobSoundDaytime,
			["oCZoneMusic"] = VirtualObjectType.oCZoneMusic,
			["oCZoneMusicDefault"] = VirtualObjectType.oCZoneMusicDefault,
			["zCZoneZFog"] = VirtualObjectType.zCZoneZFog,
			["zCZoneZFogDefault"] = VirtualObjectType.zCZoneZFogDefault,
			["zCZoneVobFarPlane"] = VirtualObjectType.zCZoneVobFarPlane,
			["zCZoneVobFarPlaneDefault"] = VirtualObjectType.zCZoneVobFarPlaneDefault
		};

	public static string ClassName(ZenObject obj, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		if (obj.IsReference && objects.TryGetValue(obj.Index, out var target))
		{
			return target.ClassName;
		}

		return obj.ClassName;
	}

	public static VirtualObject CreateShallow(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		if (source.IsReference && objects.TryGetValue(source.Index, out var target))
		{
			source = target;
		}

		var className = source.ClassName;
		var colon = className.IndexOf(':');
		if (colon >= 0)
		{
			className = className.Substring(0, colon);
		}

		var type = Types.TryGetValue(className, out var parsed) ? parsed : VirtualObjectType.Unknown;
		VirtualObject vob = type switch
		{
			VirtualObjectType.oCNpc => new NpcObject(),
			VirtualObjectType.oCItem => new ItemObject(),
			VirtualObjectType.oCMobContainer => new ContainerObject(),
			VirtualObjectType.oCMobDoor => new DoorObject(),
			VirtualObjectType.oCMOB => new MovableObject(),
			VirtualObjectType.oCMobInter or VirtualObjectType.oCMobBed or VirtualObjectType.oCMobFire or
				VirtualObjectType.oCMobLadder or VirtualObjectType.oCMobSwitch or VirtualObjectType.oCMobWheel =>
				new InteractiveObject(),
			_ => new VirtualObject()
		};

		vob.Type = type;
		vob.Parse(source, objects);
		return vob;
	}

	/// <summary>
	/// Rebuilds the vob hierarchy from the flat archive stream. Every vob is followed by a
	/// <c>childsN</c> field with its child count, then by its children in pre-order.
	/// </summary>
	public static List<VirtualObject> CreateTree(ZenObject vobTree,
		IReadOnlyDictionary<uint, ZenObject> objects)
	{
		var result = new List<VirtualObject>();
		var entries = vobTree.Entries;
		if (entries.Count == 0)
		{
			return result;
		}

		var rootCount = entries[0].Value?.Integer ?? 0;
		var index = 1;

		void ReadSequence(int count, List<VirtualObject> output)
		{
			for (var i = 0; i < count; ++i)
			{
				while (index < entries.Count && entries[index].Child == null)
				{
					++index;
				}

				if (index >= entries.Count)
				{
					return;
				}

				var vobObject = entries[index++].Child;
				var vob = CreateShallow(vobObject, objects);

				var childCount = 0;
				if (index < entries.Count && !entries[index].IsChild && entries[index].Value != null)
				{
					childCount = entries[index++].Value.Integer;
				}

				var children = new List<VirtualObject>();
				ReadSequence(childCount, children);
				foreach (var child in children)
				{
					vob.AddChild(child);
				}

				output.Add(vob);
			}
		}

		ReadSequence(rootCount, result);
		return result;
	}
}
