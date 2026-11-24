using Microsoft.Xna.Framework;
using OpenGothic.Utility;
using System;
using ZenKit.Vobs;

namespace OpenGothic.Vobs;

/// <summary>
/// Maps a raw ZEN vob type (and, for generic vobs, its visual) to a category node. The mapping
/// follows Try/OpenGothic's <c>Vob::load</c> switch: mobsi/interactive vobs share one class,
/// triggers share one, and effects are selected by their visual.
/// </summary>
internal static class VobFactory
{
	public static VobNode Create(IVirtualObject vob, VobNode parent, Matrix world, Matrix local)
	{
		var kind = KindOf(vob);
		var bounds = DefaultBounds(vob, world);

		return kind switch
		{
			VobKind.Npc => new NpcVob(vob, parent, world, local, kind, bounds),
			VobKind.Item => new ItemVob(vob, parent, world, local, kind, bounds),

			VobKind.Interactive or VobKind.Container or VobKind.Door or VobKind.Bed or VobKind.Fire or
			VobKind.Ladder or VobKind.Switch or VobKind.Wheel =>
				new InteractiveVob(vob, parent, world, local, kind, bounds),

			VobKind.Trigger or VobKind.TriggerList or VobKind.TriggerScript or VobKind.TriggerChangeLevel or
			VobKind.TriggerWorldStart or VobKind.TriggerUntouch or VobKind.CodeMaster or VobKind.MessageFilter or
			VobKind.Earthquake or VobKind.TouchDamage or VobKind.ParticleController =>
				new TriggerVob(vob, parent, world, local, kind, bounds),

			VobKind.Mover or VobKind.MoverController => new MoverVob(vob, parent, world, local, kind, bounds),
			VobKind.Light => new LightVob(vob, parent, world, local, kind, bounds),
			VobKind.Sound or VobKind.SoundDaytime or VobKind.ZoneMusic => new SoundVob(vob, parent, world, local, kind, bounds),
			VobKind.Decal or VobKind.ParticleSystem => new EffectVob(vob, parent, world, local, kind, bounds),
			VobKind.Level or VobKind.Static or VobKind.Animate => new StaticVob(vob, parent, world, local, kind, bounds),
			VobKind.ZoneFog or VobKind.ZoneFarPlane => new ZoneVob(vob, parent, world, local, kind, bounds),
			VobKind.CutsceneCamera or VobKind.CameraKeyFrame => new CameraVob(vob, parent, world, local, kind, bounds),
			VobKind.Spot or VobKind.StartPoint => new SpotVob(vob, parent, world, local, kind, bounds),
			_ => new UnknownVob(vob, parent, world, local, kind, bounds),
		};
	}

	public static VobKind KindOf(IVirtualObject vob)
	{
		switch (vob.Type)
		{
			case VirtualObjectType.oCNpc: return VobKind.Npc;
			case VirtualObjectType.oCItem: return VobKind.Item;

			case VirtualObjectType.oCMOB:
			case VirtualObjectType.oCMobInter: return VobKind.Interactive;
			case VirtualObjectType.oCMobContainer: return VobKind.Container;
			case VirtualObjectType.oCMobDoor: return VobKind.Door;
			case VirtualObjectType.oCMobBed: return VobKind.Bed;
			case VirtualObjectType.oCMobFire: return VobKind.Fire;
			case VirtualObjectType.oCMobLadder: return VobKind.Ladder;
			case VirtualObjectType.oCMobSwitch: return VobKind.Switch;
			case VirtualObjectType.oCMobWheel: return VobKind.Wheel;

			case VirtualObjectType.zCMover: return VobKind.Mover;
			case VirtualObjectType.zCMoverController: return VobKind.MoverController;

			case VirtualObjectType.zCTrigger:
			case VirtualObjectType.oCCSTrigger: return VobKind.Trigger;
			case VirtualObjectType.zCTriggerList: return VobKind.TriggerList;
			case VirtualObjectType.oCTriggerScript: return VobKind.TriggerScript;
			case VirtualObjectType.oCTriggerChangeLevel: return VobKind.TriggerChangeLevel;
			case VirtualObjectType.zCTriggerWorldStart: return VobKind.TriggerWorldStart;
			case VirtualObjectType.zCTriggerUntouch: return VobKind.TriggerUntouch;
			case VirtualObjectType.zCCodeMaster: return VobKind.CodeMaster;
			case VirtualObjectType.zCMessageFilter: return VobKind.MessageFilter;
			case VirtualObjectType.zCEarthquake: return VobKind.Earthquake;
			case VirtualObjectType.oCTouchDamage: return VobKind.TouchDamage;
			case VirtualObjectType.zCPFXController: return VobKind.ParticleController;

			case VirtualObjectType.zCVobLight: return VobKind.Light;
			case VirtualObjectType.zCVobSound: return VobKind.Sound;
			case VirtualObjectType.zCVobSoundDaytime: return VobKind.SoundDaytime;
			case VirtualObjectType.oCZoneMusic:
			case VirtualObjectType.oCZoneMusicDefault: return VobKind.ZoneMusic;

			case VirtualObjectType.zCVobSpot: return VobKind.Spot;
			case VirtualObjectType.zCVobStartpoint: return VobKind.StartPoint;
			case VirtualObjectType.zCCSCamera: return VobKind.CutsceneCamera;
			case VirtualObjectType.zCCamTrj_KeyFrame: return VobKind.CameraKeyFrame;

			case VirtualObjectType.zCZoneZFog:
			case VirtualObjectType.zCZoneZFogDefault: return VobKind.ZoneFog;
			case VirtualObjectType.zCZoneVobFarPlane:
			case VirtualObjectType.zCZoneVobFarPlaneDefault: return VobKind.ZoneFarPlane;

			case VirtualObjectType.zCVobLensFlare: return VobKind.LensFlare;
			case VirtualObjectType.zCVobScreenFX: return VobKind.ScreenFx;
		}

		// A generic vob is typed by its visual before falling back to its file type: decals
		// and particle effects are separate categories even though the file says zCVob.
		if (vob.Visual is VisualDecal)
		{
			return VobKind.Decal;
		}

		if (vob.Visual is VisualParticleEffect)
		{
			return VobKind.ParticleSystem;
		}

		return vob.Type switch
		{
			VirtualObjectType.zCVobLevelCompo => VobKind.Level,
			VirtualObjectType.zCVobAnimate => VobKind.Animate,
			VirtualObjectType.zCVob or VirtualObjectType.zCVobStair => VobKind.Static,
			VirtualObjectType.Unknown => VobKind.Unknown,
			_ => VobKind.Unknown,
		};
	}

	private static BoundingBox DefaultBounds(IVirtualObject vob, Matrix world)
	{
		var min = vob.BoundingBox.Min.ToXna();
		var max = vob.BoundingBox.Max.ToXna();
		var fileBounds = new BoundingBox(
			new Vector3(MathF.Min(min.X, max.X), MathF.Min(min.Y, max.Y), MathF.Min(min.Z, max.Z)),
			new Vector3(MathF.Max(min.X, max.X), MathF.Max(min.Y, max.Y), MathF.Max(min.Z, max.Z)));

		// ZEN vob bounds are world-space. A marker without a meaningful box gets a small
		// point box so it is still queryable by position.
		var extent = fileBounds.Max - fileBounds.Min;
		if (extent.X > 0.01f && extent.Y > 0.01f && extent.Z > 0.01f)
		{
			return fileBounds;
		}

		var position = world.Translation;
		return new BoundingBox(position - new Vector3(16f), position + new Vector3(16f));
	}
}
