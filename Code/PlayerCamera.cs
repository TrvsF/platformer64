using Sandbox;
using Sandbox.Citizen;
using System;

public sealed class PlayerCamera : Component
{
	[Property] private CameraComponent CameraComponent { get; set; }
	[Property] public int DefaultTargetDistance { get; set; } = 150;

	// defaults
	private float OrbitYaw = 135f;
	private float OrbitPitch = -35f;

	const float MinPitch = -50f;
	const float MaxPitch = 10f;

	public PlayerPawn TargetPlayer { get; set; }

	protected override void OnStart()
	{
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		if (IsProxy)
		{
			return;
		}

		if (!TargetPlayer.IsValid())
		{
			Log.Warning($"Camera {this} does not have a valid player");
			return;
		}

		var TargetLocation = TargetPlayer.CameraTarget.WorldPosition;
		var LookData = Input.AnalogLook * .33f; // magic

		OrbitYaw += LookData.yaw;
		OrbitPitch += LookData.pitch;
		OrbitPitch = Math.Clamp(OrbitPitch, MinPitch, MaxPitch);
		Angles OrbitAngles = new(OrbitPitch, OrbitYaw, 0);

		var OrbitOffset = OrbitAngles.ToRotation() * Vector3.Forward * DefaultTargetDistance;
		WorldPosition = TargetLocation + OrbitOffset;

		var CameraRotation = Rotation.LookAt(TargetLocation - WorldPosition);
		WorldRotation = CameraRotation;
	}
}
