namespace OpenGothic.Vobs;

/// <summary>
/// Behavioural category of a ZEN virtual object, derived from its file type and visual type.
/// The tree keeps the raw <see cref="OpenGothic.ZenKit.VirtualObjectType"/> as well; this enum is
/// what focus, interactions and the diagnostics filter on.
/// </summary>
public enum VobKind
{
	Unknown,
	Level,
	Static,
	Animate,
	Npc,
	Item,
	Interactive,
	Container,
	Door,
	Bed,
	Fire,
	Ladder,
	Switch,
	Wheel,
	Mover,
	MoverController,
	Trigger,
	TriggerList,
	TriggerScript,
	TriggerChangeLevel,
	TriggerWorldStart,
	TriggerUntouch,
	CodeMaster,
	MessageFilter,
	Earthquake,
	TouchDamage,
	ParticleController,
	Sound,
	SoundDaytime,
	ZoneMusic,
	Light,
	Spot,
	StartPoint,
	CutsceneCamera,
	CameraKeyFrame,
	ZoneFog,
	ZoneFarPlane,
	LensFlare,
	ScreenFx,
	Decal,
	ParticleSystem,
}
