using Sandbox;
using Sandbox.Citizen;
using Sandbox.Diagnostics;
using System;
using System.Buffers.Text;
using System.Diagnostics;

public sealed class PlayerPawn : Component
{
	[Property] private GameObject CameraPrefab { get; set; }
	[Property] public GameObject CameraTarget { get; set; }

	[RequireComponent] public CharacterController CharacterController { get; private set; }
	[RequireComponent] public CitizenAnimationHelper AnimationHelper { get; private set; }

	private PlayerCamera PlayerCamera = null;

	///////////////////////////////////////////////////////////////////////////

	protected override void OnStart()
	{
		base.OnStart();

		CameraTargetBaseOffset = CameraTarget.LocalPosition.z;

		if (IsProxy)
		{
			return;
		}

		SpawnPlayerCamera();
	}

	private void SpawnPlayerCamera()
	{
		var CameraPrefabConfig = new CloneConfig()
		{
			StartEnabled = true,
			Parent = CameraTarget,
			Transform = new(),
		};

		var CameraObject = CameraPrefab.Clone(CameraPrefabConfig);
		Assert.NotNull(CameraObject);

		PlayerCamera = CameraObject.Components.Get<PlayerCamera>();
		Assert.NotNull(PlayerCamera);

		PlayerCamera.TargetPlayer = this;
	}

	///////////////////////////////////////////////////////////////////////////
	
	private float CameraTargetBaseOffset = 0f;
	private float LastJumpZWorldPos = 0f;

	protected override void OnUpdate()
	{
		base.OnUpdate();

		TickAnimations();
	}

	private void TickAnimations()
	{
		AnimationHelper.WithVelocity(CharacterController.Velocity);
		AnimationHelper.WithWishVelocity(WishMove);
		AnimationHelper.WithLook(WishMove);
	}

	///////////////////////////////////////////////////////////////////////////

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		TickMovement();

		if (!CharacterController.IsOnGround)
		{
			CameraTarget.LocalPosition = CameraTarget.LocalPosition.WithZ(0);
			CameraTarget.WorldPosition = CameraTarget.WorldPosition.WithZ(LastJumpZWorldPos + CameraTargetBaseOffset);
		}
		else
		{
			Log.Info(CameraTarget.LocalPosition);
			CameraTarget.LocalPosition = CameraTarget.LocalPosition.WithZ(CameraTargetBaseOffset);
		}
	}

	const float MaxSpeed = 300f;
	const float MaxSprintSpeed = 420f;

	Vector3 Gravity = new(0, 0, 1200);
	Vector3 WishMove = Vector3.Zero;

	TimeSince TimeSinceJump = 0;
	bool IsJumping = false;

	private void TickMovement()
	{
		var CameraYaw = PlayerCamera.GameObject.WorldRotation.Yaw();
		WishMove = Input.AnalogMove.Normal * Rotation.FromYaw(CameraYaw);

		var WishVel = WishMove * MaxSpeed;

		if (CharacterController.IsOnGround)
		{
			if (IsJumping)
			{
				IsJumping = false;
				TimeSinceJump = 0;
			}

			var RequestDifference = CharacterController.Velocity - WishVel;
			var ShitLerp = Vector3.Lerp(CharacterController.Velocity, WishVel, 15 / RequestDifference.Length);

			CharacterController.Velocity = ShitLerp.WithZ(0);
			
			CheckJump();
		}
		else
		{
			CharacterController.Velocity -= Gravity * Time.Delta;
			CharacterController.Accelerate(WishVel / 3f);
		}

		CharacterController.Move();
	}

	private void CheckJump()
	{
		if (Input.Pressed("Jump"))
		{
			LastJumpZWorldPos = WorldPosition.z;
			IsJumping = true;

			var JumpStrength = TimeSinceJump < .1f ? 500f : 360f;
			CharacterController.Punch(Vector3.Up * JumpStrength);
		}
	}
}
