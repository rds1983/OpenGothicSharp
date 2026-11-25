using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using OpenGothic.Utility;
using OpenGothic.Vobs;
using System.Collections.Generic;
using OpenGothic.ZenKit;

namespace OpenGothic;

/// <summary>
/// One placed script NPC. Human bodies are built during spawn; a monster starts pending and
/// builds its model when the player comes near, because a full monster animation set is large.
/// Animation playback is advanced by the game's animation pass, then
/// <see cref="UpdateTransform"/> places the model at <see cref="Position"/>/<see cref="Yaw"/>.
/// </summary>
public sealed class NpcActor
{
	public NpcActor(NpcVob vob, NpcInstance instance, int symbolIndex, NpcVisual visual,
		Vector3 position, float yaw, Vector3 scale, bool human)
	{
		Vob = vob;
		Instance = instance;
		SymbolIndex = symbolIndex;
		Visual = visual;
		Position = position;
		Yaw = yaw;
		Scale = scale;
		IsHuman = human;
		IdleClip = human ? "S_RUN" : "S_FISTRUN";
		MoveClip = human ? "S_RUNL" : "S_FISTRUNL";
	}

	public NpcVob Vob { get; }

	/// <summary>Script instance, used as <c>SELF</c> when a state function runs.</summary>
	public NpcInstance Instance { get; }

	/// <summary>Symbol index of <see cref="Instance"/>, the key the script side uses.</summary>
	public int SymbolIndex { get; }

	/// <summary>Running script AI state, or null.</summary>
	public NpcAiState State { get; set; }

	/// <summary>Whether the current routine's action has been started.</summary>
	public bool RoutineStateStarted { get; set; }

	/// <summary>Reference body-state flags: standing excludes sitting and adds free hands.</summary>
	public const int BodyStateStand = (1 << 15) | (1 << 16);
	/// <summary>Reference <c>BS_SIT</c> (11) with free hands.</summary>
	public const int BodyStateSit = 11 | (1 << 16);

	/// <summary>Reference <c>BS_LIE</c>.</summary>
	public const int BodyStateLie = 12;

	/// <summary>Reference <c>BS_MOBINTERACT</c>: using a workbench, anvil or similar mob.</summary>
	public const int BodyStateMobInteract = 15;

	/// <summary>Body state reported to <c>NPC_GetBodyState</c>; standing until a state changes it.</summary>
	public int BodyState { get; set; } = BodyStateStand;

	/// <summary>Walk-mode bits from <c>AI_SetWalkMode</c>, with the weapon bit stripped.</summary>
	public int WalkMode { get; set; }

	/// <summary>Script-driven destination (<c>AI_GotoWP</c>) while it is active.</summary>
	public Vector3 AiDestination { get; set; }

	public bool HasAiMove { get; set; }

	/// <summary>Perception sampling interval requested by <c>NPC_SetPercTime</c>, in seconds.</summary>
	public float PerceptionSeconds { get; set; } = 0.4f;

	/// <summary>Mob scheme the body is using (e.g. <c>CHAIR</c>), or empty when standing.</summary>
	public string MobScheme { get; set; } = string.Empty;

	/// <summary>State requested by <c>AI_UseMob</c>, or -1 when not using a mob.</summary>
	public int MobState { get; set; } = -1;

	/// <summary>Clip played when the mob was entered, reversed to stand up again.</summary>
	public string MobClip { get; set; } = string.Empty;

	/// <summary>Follow-up clip to play once the current mob transitions into its S0 loop.</summary>
	public string MobNextClip { get; set; } = string.Empty;

	/// <summary>Mob vob the actor is attached to, or null when standing.</summary>
	public VobNode MobVob { get; set; }

	/// <summary>Attach point on <see cref="MobVob"/>, or empty when none was found.</summary>
	public string MobPoint { get; set; } = string.Empty;

	/// <summary>Position/yaw to return to when the mob is released.</summary>
	public Vector3 MobReturnPosition { get; set; }

	public float MobReturnYaw { get; set; }

	public bool HasMobReturn { get; set; }

	/// <summary>AI time before which an ended routine state is not restarted, in seconds.</summary>
	public double NextRoutineStateAt { get; set; }

	/// <summary>Number of <c>PercType</c> slots the script can enable (reference PERC_Count).</summary>
	public const int PerceptionCount = 33;

	/// <summary>
	/// Perception handler per <c>PercType</c>, filled by <c>NPC_PercEnable</c>; -1 means the
	/// type is disabled. The reference keeps the same table on the NPC.
	/// </summary>
	public int[] PerceptionFunctions { get; } = CreatePerceptionFunctions();

	/// <summary>AI time at which this NPC samples its surroundings again.</summary>
	public double NextPerceptionAt { get; set; }

	/// <summary>Script target set by <c>NPC_SetTarget</c>; -1 when none.</summary>
	public int TargetSymbol { get; set; } = -1;

	/// <summary>Update budget chosen from the distance to the player (reference policy).</summary>
	public NpcProcessPolicy Policy { get; set; } = NpcProcessPolicy.AiNormal;

	private static int[] CreatePerceptionFunctions()
	{
		var functions = new int[PerceptionCount];
		System.Array.Fill(functions, -1);
		return functions;
	}

	public NpcVisual Visual { get; }

	private Vector3 _mobRoot;
	private string _mobRootClip;
	private bool _mobRootValid;

	public Vector3 Position { get; private set; }

	public float Yaw { get; private set; }

	public Vector3 Scale { get; }

	public bool IsHuman { get; }

	public string IdleClip { get; }

	public string MoveClip { get; }

	/// <summary>Body visual, or null while a monster is waiting to be built.</summary>
	public ModelNode Node { get; private set; }

	public int RootBoneIndex { get; private set; }

	public float FeetOffset { get; private set; }

	/// <summary>Model-space bounds of the body, used after each movement step.</summary>
	public BoundingBox LocalBounds { get; private set; }

	/// <summary>Set when the body cannot be built, so the frame loop stops retrying it.</summary>
	public bool ModelFailed { get; set; }

	public bool HasModel => Node != null;

	/// <summary>Routine currently assigned to this actor, or null.</summary>
	public NpcRoutine Routine { get; set; }

	/// <summary>True when the current routine replaced an earlier one, not the spawn assignment.</summary>
	public bool RoutineChanged { get; set; }

	/// <summary>Waypoint indices still to walk; empty when idle.</summary>
	public List<int> Path { get; } = new List<int>();

	public int PathIndex { get; set; }

	public bool Moving => PathIndex < Path.Count;

	public BoundingBox WorldBounds
	{
		get
		{
			var transform = Node?.Transform ?? Matrix.Identity;
			return LocalBounds.Transform(ref transform);
		}
	}

	/// <summary>Binds a built body and places it at the actor's feet.</summary>
	public void AttachModel(ModelNode node, int rootBoneIndex, float feetOffset, BoundingBox localBounds)
	{
		Node = node;
		RootBoneIndex = rootBoneIndex;
		FeetOffset = feetOffset;
		LocalBounds = localBounds;
		UpdateTransform();
	}

	public void SetPosition(Vector3 position, float yaw)
	{
		Position = position;
		Yaw = yaw;
		_mobRootValid = false;
		UpdateTransform();
	}

	/// <summary>
	/// Turns the current clip's root translation into world movement while the actor is on a
	/// mob. The reference's <c>MoveAlgo::tickMobsi</c> moves the NPC by the same per-frame
	/// delta that <c>Pose::mkBaseTranslation</c> removes from the object matrix, so the body
	/// slides from the use point onto the furniture without rendering the root offset twice.
	/// Returns true when the actor position changed.
	/// </summary>
	public bool AdvanceMobRootMotion()
	{
		if (Node == null || Node.AnimationData == null || string.IsNullOrEmpty(MobScheme))
		{
			_mobRootValid = false;
			return false;
		}

		if (!Node.AnimationData.TryGetBaseRootTranslation(RootBoneIndex, out var root))
		{
			_mobRootValid = false;
			return false;
		}

		var clip = Node.AnimationData.CurrentClip;

		// The reference measures the delta within one sequence. A hand-over to the next clip
		// starts a new baseline instead of integrating the jump between two clips.
		if (!_mobRootValid || !string.Equals(clip, _mobRootClip, System.StringComparison.OrdinalIgnoreCase))
		{
			_mobRoot = root;
			_mobRootClip = clip;
			_mobRootValid = true;
			return false;
		}

		var delta = root - _mobRoot;
		_mobRoot = root;

		if (Node.AnimationData.IsBaseClipReversed)
		{
			delta = -delta;
		}

		// The reference drops the vertical part and rotates the model-space step into the
		// world, which is the same rotation this actor's render transform applies. The step
		// is not scaled: MoveAlgo applies the raw sequence speed with a unit multiplier.
		var step = Vector3.TransformNormal(new Vector3(delta.X, 0f, delta.Z),
			Matrix.CreateRotationY(-Yaw));
		if (step.X == 0f && step.Z == 0f)
		{
			return false;
		}

		Position += step;
		return true;
	}

	/// <summary>Applies the current bone pose to the model node.</summary>
	public void UpdateTransform()
	{
		if (Node == null)
		{
			return;
		}

		var root = Node.Instance.GetBoneGlobalTransform(RootBoneIndex).Translation;

		// The object matrix cancels the animated root on the horizontal plane, like
		// Pose::mkBaseTranslation. Horizontal motion is carried by Position instead (walking
		// from the path, mob entry from AdvanceMobRootMotion). The vertical root translation
		// stays visible: it is what lifts the body onto a seat or lowers it onto a bed. For
		// mob clips the animated root already supplies that height, so the idle-derived feet
		// offset must not be added on top of it.
		var rootY = string.IsNullOrEmpty(MobScheme) ? FeetOffset : 0f;

		Node.Transform =
			Matrix.CreateTranslation(-root.X, rootY, -root.Z) *
			Matrix.CreateScale(Scale) *
			Matrix.CreateRotationY(-Yaw) *
			Matrix.CreateTranslation(Position);
	}

	public void SetMovingClip(bool moving)
	{
		SetClip(moving ? MoveClip : IdleClip);
	}

	/// <summary>Plays a clip by name for a script state; false when the body lacks it.</summary>
	public bool PlayClip(string name)
	{
		var animation = Node?.AnimationData;
		if (animation == null || string.IsNullOrEmpty(name))
		{
			return false;
		}

		return animation.Play(name);
	}

	/// <summary>Plays a clip backwards, e.g. the sit transition to stand up again.</summary>
	public bool PlayClipReverse(string name)
	{
		var animation = Node?.AnimationData;
		if (animation == null || string.IsNullOrEmpty(name))
		{
			return false;
		}

		return animation.Play(name, blend: true, reverse: true);
	}

	private void SetClip(string name)
	{
		var animation = Node?.AnimationData;
		if (animation == null ||
		    string.Equals(animation.CurrentClip, name, System.StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		animation.Play(name);
	}
}
