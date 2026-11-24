using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using OpenGothic.Utility;
using OpenGothic.Vobs;
using System.Collections.Generic;

namespace OpenGothic;

/// <summary>
/// One placed script NPC. Human bodies are built during spawn; a monster starts pending and
/// builds its model when the player comes near, because a full monster animation set is large.
/// Animation playback is advanced by the game's animation pass, then
/// <see cref="UpdateTransform"/> places the model at <see cref="Position"/>/<see cref="Yaw"/>.
/// </summary>
public sealed class NpcActor
{
	public NpcActor(NpcVob vob, NpcVisual visual, Vector3 position, float yaw, Vector3 scale,
		bool human)
	{
		Vob = vob;
		Visual = visual;
		Position = position;
		Yaw = yaw;
		Scale = scale;
		IsHuman = human;
		IdleClip = human ? "S_RUN" : "S_FISTRUN";
		MoveClip = human ? "S_RUNL" : "S_FISTRUNL";
	}

	public NpcVob Vob { get; }

	public NpcVisual Visual { get; }

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
		UpdateTransform();
	}

	/// <summary>Applies the current bone pose to the model node.</summary>
	public void UpdateTransform()
	{
		if (Node == null)
		{
			return;
		}

		var root = Node.Instance.GetBoneGlobalTransform(RootBoneIndex).Translation;
		Node.Transform =
			Matrix.CreateTranslation(-root.X, FeetOffset, -root.Z) *
			Matrix.CreateScale(Scale) *
			Matrix.CreateRotationY(-Yaw) *
			Matrix.CreateTranslation(Position);
	}

	public void SetMovingClip(bool moving)
	{
		SetClip(moving ? MoveClip : IdleClip);
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
