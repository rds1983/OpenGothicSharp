using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using OpenGothic.Vobs;

namespace OpenGothic;

/// <summary>
/// One placed script NPC. The body model is shared between NPCs with the same composition;
/// this object owns the instance transform. Animation playback is advanced by the game's
/// animation pass, then <see cref="UpdateTransform"/> cancels the clip's root X/Z so an idle
/// does not slide the body.
/// </summary>
public sealed class NpcActor
{
	public NpcActor(NpcVob vob, ModelNode node, int rootBoneIndex, float feetOffset,
		Vector3 position, float yaw, Vector3 scale)
	{
		Vob = vob;
		Node = node;
		RootBoneIndex = rootBoneIndex;
		FeetOffset = feetOffset;
		Position = position;
		Yaw = yaw;
		Scale = scale;
	}

	public NpcVob Vob { get; }

	public ModelNode Node { get; }

	public int RootBoneIndex { get; }

	public float FeetOffset { get; }

	public Vector3 Position { get; }

	public float Yaw { get; }

	public Vector3 Scale { get; }

	/// <summary>Applies the current bone pose to the model node.</summary>
	public void UpdateTransform()
	{
		var root = Node.Instance.GetBoneGlobalTransform(RootBoneIndex).Translation;
		Node.Transform =
			Matrix.CreateTranslation(-root.X, FeetOffset, -root.Z) *
			Matrix.CreateScale(Scale) *
			Matrix.CreateRotationY(-Yaw) *
			Matrix.CreateTranslation(Position);
	}
}
