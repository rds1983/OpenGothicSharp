using System;

namespace OpenGothic.Gameplay;

/// <summary>
/// One frame of player intent, produced by a host and consumed by <see cref="GameCore"/>.
/// </summary>
/// <remarks>
/// Axes are relative to the player, not the world: <see cref="MoveForward"/> and
/// <see cref="MoveRight"/> are mapped onto the camera's ground plane by the engine. Hosts turn
/// key or gamepad state into these values and never touch the camera directly.
/// </remarks>
public readonly struct GameInput
{
	/// <param name="moveForward">Forward axis: <c>+1</c> forward, <c>-1</c> back.</param>
	/// <param name="moveRight">Right axis: <c>+1</c> right, <c>-1</c> left.</param>
	/// <param name="run">Whether the player is sprinting.</param>
	/// <param name="lookDeltaX">Horizontal look delta in pixels since the last frame.</param>
	/// <param name="lookDeltaY">Vertical look delta in pixels since the last frame.</param>
	/// <param name="wheelDelta">Mouse wheel delta since the last frame; used for camera zoom.</param>
	/// <param name="toggleDebugOverlay">
	/// Set for a single frame when the toggle key is pressed. The host is responsible for
	/// edge detection, so holding the key does not flip the overlay every frame.
	/// </param>
	public GameInput(
		float moveForward,
		float moveRight,
		bool run,
		float lookDeltaX,
		float lookDeltaY,
		int wheelDelta,
		bool toggleDebugOverlay)
	{
		MoveForward = moveForward;
		MoveRight = moveRight;
		Run = run;
		LookDeltaX = lookDeltaX;
		LookDeltaY = lookDeltaY;
		WheelDelta = wheelDelta;
		ToggleDebugOverlay = toggleDebugOverlay;
	}

	/// <summary>Forward axis: <c>+1</c> forward, <c>-1</c> back, <c>0</c> idle.</summary>
	public float MoveForward { get; }

	/// <summary>Right axis: <c>+1</c> right, <c>-1</c> left, <c>0</c> idle.</summary>
	public float MoveRight { get; }

	/// <summary>Whether the player is sprinting.</summary>
	public bool Run { get; }

	/// <summary>Horizontal look delta in pixels since the last frame.</summary>
	public float LookDeltaX { get; }

	/// <summary>Vertical look delta in pixels since the last frame.</summary>
	public float LookDeltaY { get; }

	/// <summary>Mouse wheel delta since the last frame.</summary>
	public int WheelDelta { get; }

	/// <summary>Whether the debug overlay should be toggled this frame.</summary>
	public bool ToggleDebugOverlay { get; }
}
