using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using OpenGothic.Vobs;

namespace OpenGothic;

/// <summary>
/// One placed script NPC. Human bodies are built during spawn; a monster starts pending and
/// builds its model when the player comes near, because a full monster animation set is large.
/// Animation playback is advanced by the game's animation pass, then
/// <see cref="UpdateTransform"/> cancels the clip's root X/Z.
/// </summary>
public sealed class NpcActor
{
	public NpcActor(NpcVob vob, NpcVisual visual, Vector3 position, float yaw, Vector3 scale)
	{
		Vob = vob;
		Visual = visual;
		Position = position;
		Yaw = yaw;
		Scale = scale;
	}

	public NpcVob Vob { get; }

	public NpcVisual Visual { get; }

	public Vector3 Position { get; }

	public float Yaw { get; }

	public Vector3 Scale { get; }

	/// <summary>Body visual, or null while a monster is waiting to be built.</summary>
	public ModelNode Node { get; private set; }

	public int RootBoneIndex { get; private set; }

	public float FeetOffset { get; private set; }

	/// <summary>Set when the body cannot be built, so the frame loop stops retrying it.</summary>
	public bool ModelFailed { get; set; }

	public bool HasModel => Node != null;

	/// <summary>Binds a built body and places it at the actor's feet.</summary>
	public void AttachModel(ModelNode node, int rootBoneIndex, float feetOffset)
	{
		Node = node;
		RootBoneIndex = rootBoneIndex;
		FeetOffset = feetOffset;
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
}
