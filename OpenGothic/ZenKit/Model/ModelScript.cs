using System;
using System.Collections.Generic;

namespace OpenGothic.ZenKit;

[Flags]
public enum AnimationFlags : byte
{
	None = 0,
	Move = 1,
	Rotate = 2,
	Queue = 4,
	Fly = 8,
	Idle = 16,
	InPlace = 32
}

public enum AnimationDirection : byte
{
	Forward = 0,
	Backward = 1
}

public enum EventType : byte
{
	Unknown = 0,
	ItemCreate = 1,
	ItemInsert = 2,
	ItemRemove = 3,
	ItemDestroy = 4,
	ItemPlace = 5,
	ItemExchange = 6,
	SetFightMode = 7,
	MunitionPlace = 8,
	MunitionRemove = 9,
	SoundDraw = 10,
	SoundUndraw = 11,
	MeshSwap = 12,
	TorchDraw = 13,
	TorchInventory = 14,
	TorchDrop = 15,
	HitLimb = 16,
	HitDirection = 17,
	DamageMultiplier = 18,
	ParryFrame = 19,
	OptimalFrame = 20,
	HitEnd = 21,
	ComboWindow = 22
}

public sealed class AnimationEventTag
{
	public int Frame { get; internal set; }

	public EventType Type { get; internal set; }

	public string Slot { get; internal set; } = string.Empty;

	public string Slot2 { get; internal set; } = string.Empty;

	public string Item { get; internal set; } = string.Empty;

	public string Frames { get; internal set; } = string.Empty;
}

public sealed class AnimationSoundEffect
{
	public int Frame { get; internal set; }
	public string Name { get; internal set; } = string.Empty;
	public float Range { get; internal set; }
	public bool EmptySlot { get; internal set; }
}

public sealed class AnimationParticleEffect
{
	public int Frame { get; internal set; }
	public int Index { get; internal set; }
	public string Name { get; internal set; } = string.Empty;
	public string Position { get; internal set; } = string.Empty;
	public bool Attached { get; internal set; }
}

public sealed class AnimationParticleEffectStop
{
	public int Frame { get; internal set; }
	public int Index { get; internal set; }
}

public sealed class AnimationMorphEffect
{
	public int Frame { get; internal set; }
	public string Animation { get; internal set; } = string.Empty;
	public string Node { get; internal set; } = string.Empty;
}

public sealed class AnimationCameraTremor
{
	public int Frame { get; internal set; }
	public int Field1 { get; internal set; }
	public int Field2 { get; internal set; }
	public int Field3 { get; internal set; }
	public int Field4 { get; internal set; }
}

public sealed class Animation
{
	public string Name { get; internal set; } = string.Empty;
	public uint Layer { get; internal set; }
	public string Next { get; internal set; } = string.Empty;
	public float BlendIn { get; internal set; }
	public float BlendOut { get; internal set; }
	public AnimationFlags Flags { get; internal set; }
	public string Model { get; internal set; } = string.Empty;
	public AnimationDirection Direction { get; internal set; }
	public int FirstFrame { get; internal set; }
	public int LastFrame { get; internal set; }
	public float Fps { get; internal set; }
	public float Speed { get; internal set; }
	public float CollisionVolumeScale { get; internal set; }

	public List<AnimationEventTag> EventTags { get; } = new List<AnimationEventTag>();
	public List<AnimationSoundEffect> SoundEffects { get; } = new List<AnimationSoundEffect>();
	public List<AnimationSoundEffect> SoundEffectsGround { get; } = new List<AnimationSoundEffect>();
	public List<AnimationParticleEffect> ParticleEffects { get; } = new List<AnimationParticleEffect>();
	public List<AnimationParticleEffectStop> ParticleEffectStops { get; } = new List<AnimationParticleEffectStop>();
	public List<AnimationMorphEffect> MorphEffects { get; } = new List<AnimationMorphEffect>();
	public List<AnimationCameraTremor> CameraTremors { get; } = new List<AnimationCameraTremor>();
}

public sealed class AnimationAlias
{
	public string Name { get; internal set; } = string.Empty;
	public uint Layer { get; internal set; }
	public string Next { get; internal set; } = string.Empty;
	public float BlendIn { get; internal set; }
	public float BlendOut { get; internal set; }
	public AnimationFlags Flags { get; internal set; }
	public string Alias { get; internal set; } = string.Empty;
	public AnimationDirection Direction { get; internal set; }
}

public sealed class AnimationBlend
{
	public string Name { get; internal set; } = string.Empty;
	public string Next { get; internal set; } = string.Empty;
	public float BlendIn { get; internal set; }
	public float BlendOut { get; internal set; }
}

public sealed class AnimationCombine
{
	public string Name { get; internal set; } = string.Empty;
	public uint Layer { get; internal set; }
	public string Next { get; internal set; } = string.Empty;
	public float BlendIn { get; internal set; }
	public float BlendOut { get; internal set; }
	public AnimationFlags Flags { get; internal set; }
	public string Model { get; internal set; } = string.Empty;
	public int LastFrame { get; internal set; }
}

public sealed class ModelTag
{
	public string Bone { get; internal set; } = string.Empty;
}

/// <summary>
/// Managed model script reader (MSB/MDS). Ported from ZenKit's <c>ModelScript</c>; the
/// shipped Gothic 2 scripts use the binary chunk layout parsed here.
/// </summary>
public sealed class ModelScript
{
	private const ushort ChunkRoot = 0xF000;
	private const ushort ChunkEnd = 0xFFFF;
	private const ushort ChunkSource = 0xF100;
	private const ushort ChunkMeshAndTree = 0xF300;
	private const ushort ChunkRegisterMesh = 0xF400;
	private const ushort ChunkAnimation = 0xF520;
	private const ushort ChunkAnimationAlias = 0xF530;
	private const ushort ChunkAnimationBlend = 0xF540;
	private const ushort ChunkAnimationCombine = 0xF570;
	private const ushort ChunkAnimationDisable = 0xF580;
	private const ushort ChunkModelTag = 0xF590;
	private const ushort ChunkAnimationEvents = 0xF5A0;
	private const ushort ChunkEventSfx = 0xF5A1;
	private const ushort ChunkEventSfxGround = 0xF5A2;
	private const ushort ChunkEventTag = 0xF5A3;
	private const ushort ChunkEventPfx = 0xF5A4;
	private const ushort ChunkEventPfxStop = 0xF5A5;
	private const ushort ChunkEventMorphAnimation = 0xF5A9;
	private const ushort ChunkEventCameraTremor = 0xF5AA;

	private static readonly Dictionary<string, EventType> EventTypes =
		new Dictionary<string, EventType>(StringComparer.Ordinal)
		{
			["DEF_CREATE_ITEM"] = EventType.ItemCreate,
			["DEF_INSERT_ITEM"] = EventType.ItemInsert,
			["DEF_REMOVE_ITEM"] = EventType.ItemRemove,
			["DEF_DESTROY_ITEM"] = EventType.ItemDestroy,
			["DEF_PLACE_ITEM"] = EventType.ItemPlace,
			["DEF_EXCHANGE_ITEM"] = EventType.ItemExchange,
			["DEF_FIGHTMODE"] = EventType.SetFightMode,
			["DEF_PLACE_MUNITION"] = EventType.MunitionPlace,
			["DEF_REMOVE_MUNITION"] = EventType.MunitionRemove,
			["DEF_DRAWSOUND"] = EventType.SoundDraw,
			["DEF_UNDRAWSOUND"] = EventType.SoundUndraw,
			["DEF_SWAPMESH"] = EventType.MeshSwap,
			["DEF_DRAWTORCH"] = EventType.TorchDraw,
			["DEF_INV_TORCH"] = EventType.TorchInventory,
			["DEF_DROP_TORCH"] = EventType.TorchDrop,
			["DEF_HIT_LIMB"] = EventType.HitLimb,
			["HIT_LIMB"] = EventType.HitLimb,
			["DEF_HIT_DIR"] = EventType.HitDirection,
			["DEF_DIR"] = EventType.HitDirection,
			["DEF_DAM_MULTIPLY"] = EventType.DamageMultiplier,
			["DEF_PAR_FRAME"] = EventType.ParryFrame,
			["DEF_OPT_FRAME"] = EventType.OptimalFrame,
			["DEF_HIT_END"] = EventType.HitEnd,
			["DEF_WINDOW"] = EventType.ComboWindow
		};

	private int _animationIndex = -1;

	public string SkeletonName { get; private set; } = string.Empty;

	public List<string> Meshes { get; } = new List<string>();

	public List<string> DisabledAnimations { get; } = new List<string>();

	public List<Animation> Animations { get; } = new List<Animation>();

	public List<AnimationAlias> AnimationAliases { get; } = new List<AnimationAlias>();

	public List<AnimationBlend> AnimationBlends { get; } = new List<AnimationBlend>();

	public List<AnimationCombine> AnimationCombines { get; } = new List<AnimationCombine>();

	public List<ModelTag> ModelTags { get; } = new List<ModelTag>();

	public static ModelScript Load(Read reader)
	{
		var script = new ModelScript();
		script.ReadChunks(reader);
		return script;
	}

	private void ReadChunks(Read reader)
	{
		while (!reader.Eof)
		{
			var type = reader.ReadUShort();
			var size = reader.ReadUInt();
			var target = reader.Position + size;

			switch (type)
			{
				case ChunkRoot:
					_ = reader.ReadUInt();
					_ = reader.ReadLine();
					break;

				case ChunkSource:
					reader.Skip(16);
					_ = reader.ReadLine();
					break;

				case ChunkMeshAndTree:
					_ = reader.ReadUInt();
					SkeletonName = reader.ReadLine();
					break;

				case ChunkRegisterMesh:
					Meshes.Add(reader.ReadLine(true));
					break;

				case ChunkAnimation:
				{
					var animation = new Animation
					{
						Name = reader.ReadLine(),
						Layer = reader.ReadUInt(),
						Next = reader.ReadLine(),
						BlendIn = reader.ReadFloat(),
						BlendOut = reader.ReadFloat(),
						Flags = ParseFlags(reader.ReadLine()),
						Model = reader.ReadLine()
					};

					animation.Direction = reader.ReadLine().StartsWith("R", StringComparison.Ordinal)
						? AnimationDirection.Backward
						: AnimationDirection.Forward;
					animation.FirstFrame = reader.ReadInt();
					animation.LastFrame = reader.ReadInt();
					animation.Fps = reader.ReadFloat();
					animation.Speed = reader.ReadFloat();
					animation.CollisionVolumeScale = reader.ReadFloat();
					Animations.Add(animation);
					_animationIndex = Animations.Count - 1;
					break;
				}

				case ChunkAnimationAlias:
				{
					var alias = new AnimationAlias
					{
						Name = reader.ReadLine(),
						Layer = reader.ReadUInt(),
						Next = reader.ReadLine(),
						BlendIn = reader.ReadFloat(),
						BlendOut = reader.ReadFloat(),
						Flags = ParseFlags(reader.ReadLine()),
						Alias = reader.ReadLine()
					};

					alias.Direction = reader.ReadLine().StartsWith("R", StringComparison.Ordinal)
						? AnimationDirection.Backward
						: AnimationDirection.Forward;
					AnimationAliases.Add(alias);
					break;
				}

				case ChunkAnimationBlend:
					AnimationBlends.Add(new AnimationBlend
					{
						Name = reader.ReadLine(),
						Next = reader.ReadLine(),
						BlendIn = reader.ReadFloat(),
						BlendOut = reader.ReadFloat()
					});
					break;

				case ChunkAnimationCombine:
					AnimationCombines.Add(new AnimationCombine
					{
						Name = reader.ReadLine(),
						Layer = reader.ReadUInt(),
						Next = reader.ReadLine(),
						BlendIn = reader.ReadFloat(),
						BlendOut = reader.ReadFloat(),
						Flags = ParseFlags(reader.ReadLine()),
						Model = reader.ReadLine(),
						LastFrame = reader.ReadInt()
					});
					break;

				case ChunkAnimationDisable:
					DisabledAnimations.Add(reader.ReadLine());
					break;

				case ChunkModelTag:
					_ = reader.ReadInt();
					_ = reader.ReadLine(); // DEF_HIT_LIMB / HIT_LIMB
					ModelTags.Add(new ModelTag { Bone = reader.ReadLine(true) });
					break;

				case ChunkAnimationEvents:
					_ = reader.ReadUInt();
					break;

				case ChunkEventSfx:
				case ChunkEventSfxGround:
				{
					var effect = new AnimationSoundEffect
					{
						Frame = reader.ReadInt(),
						Name = reader.ReadLine(),
						Range = reader.ReadFloat(),
						EmptySlot = reader.ReadUInt() != 0
					};

					if (_animationIndex >= 0)
					{
						(type == ChunkEventSfx ? Animations[_animationIndex].SoundEffects :
							Animations[_animationIndex].SoundEffectsGround).Add(effect);
					}

					break;
				}

				case ChunkEventTag:
				{
					var eventTag = ReadEventTag(reader);
					if (_animationIndex >= 0)
					{
						Animations[_animationIndex].EventTags.Add(eventTag);
					}

					break;
				}

				case ChunkEventPfx:
				{
					var effect = new AnimationParticleEffect
					{
						Frame = reader.ReadInt(),
						Index = reader.ReadInt(),
						Name = reader.ReadLine(),
						Position = reader.ReadLine(),
						Attached = reader.ReadUInt() != 0
					};

					if (_animationIndex >= 0)
					{
						Animations[_animationIndex].ParticleEffects.Add(effect);
					}

					break;
				}

				case ChunkEventPfxStop:
				{
					var effect = new AnimationParticleEffectStop
					{
						Frame = reader.ReadInt(),
						Index = reader.ReadInt()
					};

					if (_animationIndex >= 0)
					{
						Animations[_animationIndex].ParticleEffectStops.Add(effect);
					}

					break;
				}

				case ChunkEventMorphAnimation:
				{
					var effect = new AnimationMorphEffect
					{
						Frame = reader.ReadInt(),
						Animation = reader.ReadLine(),
						Node = reader.ReadLine()
					};
					_ = reader.ReadFloat();
					_ = reader.ReadFloat();

					if (_animationIndex >= 0)
					{
						Animations[_animationIndex].MorphEffects.Add(effect);
					}

					break;
				}

				case ChunkEventCameraTremor:
				{
					var effect = new AnimationCameraTremor
					{
						Frame = reader.ReadInt(),
						Field1 = reader.ReadInt(),
						Field2 = reader.ReadInt(),
						Field3 = reader.ReadInt(),
						Field4 = reader.ReadInt()
					};

					if (_animationIndex >= 0)
					{
						Animations[_animationIndex].CameraTremors.Add(effect);
					}

					break;
				}

				case ChunkEnd:
					return;
			}

			reader.Seek(target);
		}
	}

	private static AnimationEventTag ReadEventTag(Read reader)
	{
		var tag = new AnimationEventTag
		{
			Frame = reader.ReadInt()
		};

		var type = reader.ReadLine();
		tag.Type = EventTypes.TryGetValue(type, out var parsed) ? parsed : EventType.Unknown;

		switch (tag.Type)
		{
			case EventType.ItemCreate:
			case EventType.ItemExchange:
				tag.Slot = reader.ReadLine(true);
				tag.Item = reader.ReadLine(true);
				break;

			case EventType.ItemInsert:
			case EventType.MunitionPlace:
				tag.Slot = reader.ReadLine(true);
				break;

			case EventType.SetFightMode:
				tag.Slot = reader.ReadLine(true);
				break;

			case EventType.MeshSwap:
				tag.Slot = reader.ReadLine(true);
				tag.Slot2 = reader.ReadLine(true);
				break;

			case EventType.HitLimb:
			case EventType.HitDirection:
			case EventType.SoundDraw:
			case EventType.SoundUndraw:
			case EventType.MunitionRemove:
			case EventType.ItemDestroy:
			case EventType.TorchInventory:
			case EventType.ItemRemove:
				tag.Slot = reader.ReadLine(true);
				break;

			case EventType.DamageMultiplier:
			case EventType.ParryFrame:
			case EventType.OptimalFrame:
			case EventType.HitEnd:
			case EventType.ComboWindow:
				tag.Frames = reader.ReadLine(true);
				break;
		}

		return tag;
	}

	private static AnimationFlags ParseFlags(string value)
	{
		var flags = AnimationFlags.None;
		foreach (var c in value)
		{
			flags |= c switch
			{
				'M' => AnimationFlags.Move,
				'R' => AnimationFlags.Rotate,
				'E' => AnimationFlags.Queue,
				'F' => AnimationFlags.Fly,
				'I' => AnimationFlags.Idle,
				'P' => AnimationFlags.InPlace,
				_ => AnimationFlags.None
			};
		}

		return flags;
	}
}
