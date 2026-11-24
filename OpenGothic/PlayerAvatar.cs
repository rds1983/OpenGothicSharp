using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Graphics;
using System;

namespace OpenGothic;

/// <summary>
/// The animated hero body attached to the walking capsule until the script layer can choose
/// the real hero model. It composes the human skeleton with the naked body, the prisoner
/// armour and the hero's head, which is what <c>PC_HERO</c> resolves to before the Daedalus VM
/// exists.
/// </summary>
/// <remarks>
/// The clips follow the reference's <c>AnimationSolver</c>. It distinguishes idle from move:
/// an unarmed actor idles on <c>S_RUN</c>, walks on <c>S_WALKL</c> and runs on <c>S_RUNL</c>
/// (the <c>S_%sRUN</c>/<c>S_%sRUNL</c>/<c>S_%sWALKL</c> pattern with an empty weapon prefix).
/// The directional clips carry root motion, so the animation's root X/Z is cancelled each
/// frame; gameplay movement comes from the character controller.
/// </remarks>
public sealed class PlayerAvatar
{
	private const string Skeleton = "HUMANS";
	private const string HeadMesh = "HUM_HEAD_PONY.MMB";

	private const string IdleClip = "S_RUN";
	private const string WalkClip = "S_WALKL";
	private const string RunClip = "S_RUNL";

	/// <summary>Below this speed the hero is standing, not moving; units per second.</summary>
	public const float MoveThreshold = 10f;

	private readonly ModelNode _node;
	private readonly int _rootBoneIndex;
	private readonly float _feetOffset;
	private string _requestedClip;

	private PlayerAvatar(ModelNode node, int rootBoneIndex, float feetOffset, string clip)
	{
		_node = node;
		_rootBoneIndex = rootBoneIndex;
		_feetOffset = feetOffset;
		_requestedClip = clip;
	}

	/// <summary>The visual to draw for the player, or to skip when the camera is in the head.</summary>
	public ModelNode Node => _node;

	/// <summary>Clip currently playing, or null before the first update.</summary>
	public string CurrentClip => _node.AnimationData?.CurrentClip;

	public static PlayerAvatar Create(GraphicsDevice device, Assets assets)
	{
		if (device == null)
		{
			throw new ArgumentNullException(nameof(device));
		}

		if (assets == null)
		{
			throw new ArgumentNullException(nameof(assets));
		}

		var model = assets.GetComposedModel(device, Skeleton, "HUM_BODY_NAKED0", "ARMOR_PRISONER");
		var instance = new DrModelInstance(model);
		var node = new ModelNode
		{
			Instance = instance,
			// The capsule from milestone D is the player's collision.
			Collision = false
		};

		var headBone = model.FindBoneByName("BIP01 HEAD");
		if (headBone != null)
		{
			try
			{
				var headMesh = assets.GetMorphMesh(device, HeadMesh);
				node.BonesAttachments.Add(new ModelBoneAttachment(ModelBoneAttachment.WrapMesh(headMesh))
				{
					Bone = headBone
				});
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to load the player head '{HeadMesh}': {ex.Message}");
			}
		}
		else
		{
			Log.Warning("Player skeleton has no 'BIP01 HEAD' bone; the body will be headless.");
		}

		var rootBone = model.FindBoneByName("BIP01") ?? model.Bones[0];
		var boundBox = instance.BoundingBox ?? new BoundingBox(Vector3.Zero, Vector3.Zero);

		var animation = new AnimationData(instance);
		node.AnimationData = animation;
		var clip = animation.Play(IdleClip) ? IdleClip : null;
		if (clip == null)
		{
			Log.Warning($"Player avatar has no playable clip '{IdleClip}'.");
		}
		else
		{
			Log.Info($"Player avatar clip '{clip}'.");
		}

		// The bind box is measured at the skeleton's default root. The first idle frame puts
		// the root a little lower, so the offset is derived from the pose actually drawn;
		// otherwise the feet would hover or sink by that difference.
		var defaultRootY = rootBone.DefaultPose.Translation.Y;
		var idleRootY = instance.GetBoneGlobalTransform(rootBone.Index).Translation.Y;
		var feetOffset = -boundBox.Min.Y - (idleRootY - defaultRootY);

		return new PlayerAvatar(node, rootBone.Index, feetOffset, clip);
	}

	/// <summary>
	/// Chooses the clip the reference's animation solver would use for an unarmed actor.
	/// Idle, walk and run are distinct clips; the first is the standing loop.
	/// </summary>
	public static string SelectClip(float speed, bool walking)
	{
		if (speed < MoveThreshold)
		{
			return IdleClip;
		}

		return walking ? WalkClip : RunClip;
	}

	/// <summary>
	/// Places the body at the capsule's feet and advances its clip. The bind already includes
	/// the skeleton's root translation, so no extra Y offset is needed; the animation's root
	/// X/Z is cancelled because the directional clips carry root motion that the controller
	/// already applies to the capsule.
	/// </summary>
	public void Update(TimeSpan elapsed, Vector3 feet, float yaw, float speed, bool walking)
	{
		var animation = _node.AnimationData;
		var wanted = SelectClip(speed, walking);
		if (animation != null && !string.Equals(_requestedClip, wanted, StringComparison.OrdinalIgnoreCase))
		{
			if (animation.Play(wanted))
			{
				Log.Info($"Player avatar clip '{wanted}'.");
			}
			else if (_requestedClip == null)
			{
				Log.Warning($"Player avatar has no playable clip '{wanted}'.");
			}

			_requestedClip = wanted;
		}

		animation?.Update(elapsed);

		// The reference cancels the root node's X/Z before composing the pose. Without it a
		// run cycle would slide the body a metre forward and snap back at every loop.
		var root = _node.Instance.GetBoneGlobalTransform(_rootBoneIndex).Translation;

		_node.Transform =
			Matrix.CreateTranslation(-root.X, _feetOffset, -root.Z) *
			Matrix.CreateRotationY(-yaw) *
			Matrix.CreateTranslation(feet);
	}
}
