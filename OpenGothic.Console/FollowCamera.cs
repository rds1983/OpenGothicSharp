using Microsoft.Xna.Framework;

namespace OpenGothic.Tools;

/// <summary>
/// Third-person orbit camera: yaw/pitch around a target, at a configurable distance.
/// </summary>
internal sealed class FollowCamera
{
	private const float MaxPitch = MathHelper.PiOver2 - 0.01f;
	private const float MinPitch = -MathHelper.PiOver2 + 0.01f;

	public float Yaw;
	public float Pitch = -0.25f;

	/// <summary>Distance from the target in world units.</summary>
	public float Distance = 450.0f;

	/// <summary>Mouse sensitivity in radians per pixel.</summary>
	public float LookSensitivity = 0.0022f;

	public Vector3 Forward => Vector3.Transform(Vector3.Forward, Matrix.CreateFromYawPitchRoll(Yaw, Pitch, 0.0f));

	/// <summary>Camera forward projected onto the ground plane.</summary>
	public Vector3 PlanarForward => Vector3.Transform(Vector3.Forward, Matrix.CreateRotationY(Yaw));

	/// <summary>Camera right projected onto the ground plane.</summary>
	public Vector3 PlanarRight => Vector3.Transform(Vector3.Right, Matrix.CreateRotationY(Yaw));

	public Matrix GetView(Vector3 target) => Matrix.CreateLookAt(target - Forward * Distance, target, Vector3.Up);

	public void Look(float deltaX, float deltaY)
	{
		Yaw -= deltaX * LookSensitivity;
		Pitch -= deltaY * LookSensitivity;
		Pitch = MathHelper.Clamp(Pitch, MinPitch, MaxPitch);
	}

	public void Zoom(float wheelDelta)
	{
		if (wheelDelta == 0.0f)
		{
			return;
		}

		Distance = MathHelper.Clamp(Distance * (wheelDelta > 0.0f ? 0.85f : 1.0f / 0.85f), 150.0f, 5000.0f);
	}
}
