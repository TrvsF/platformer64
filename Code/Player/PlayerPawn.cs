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
	[Property] public GameObject CameraTopBound { get; set; }
	[Property] public GameObject CameraLowBound { get; set; }

	[RequireComponent] public CharacterController CharacterController { get; private set; }
	[RequireComponent] public CitizenAnimationHelper AnimationHelper { get; private set; }

	[Sync] public float Yaw { get; private set; } = 0f;
	[Sync] public bool IsCrouching { get; private set; } = false;

	private PlayerCamera PlayerCamera = null;

	///////////////////////////////////////////////////////////////////////////

	protected override void OnStart()
	{
		base.OnStart();

		CameraBaseTargetBaseOffset = CameraTarget.LocalPosition.z;
		CameraTopTargetBaseOffset = CameraTopBound.LocalPosition.z;
		CameraLowTargetBaseOffset = CameraLowBound.LocalPosition.z;

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
		AnimationHelper.WorldRotation = Rotation.FromYaw(Yaw);
		AnimationHelper.DuckLevel = IsCrouching ? 1 : 0.5f;
		AnimationHelper.IsGrounded = CharacterController.IsOnGround;
	}

	///////////////////////////////////////////////////////////////////////////

	const float DamageCooldownTime = 0.8f;
	public TimeSince TimeSinceHealthChange = 0;
	public int Health { get; private set; } = 100;

	public bool IsInvunrable()
	{
		return TimeSinceHealthChange < DamageCooldownTime;
	}

	public void TakeDamage(int Damage)
	{
		if (IsInvunrable())
		{
			return;
		}

		Health -= Damage;
		TimeSinceHealthChange = 0;

		if (Health <= 0)
		{
			Kill();
		}
	}

	private void Kill()
	{
		DestroyGameObject();
	}

	///////////////////////////////////////////////////////////////////////////

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if (IsProxy)
		{
			return;
		}

		TickMovement();
		TickCamera();
	}

	private float CameraBaseTargetBaseOffset = 0f;
	private float CameraTopTargetBaseOffset = 0f;
	private float CameraLowTargetBaseOffset = 0f;

	private bool IsOutsideOfTopBound = false;
	private bool IsOutsideOfLowBound = false;
	
	private float LastJumpZWorldPos = 0f;

	private void TickCamera()
	{
		// HACK : keep hold of yaw for animations
		Yaw = PlayerCamera.WorldRotation.Yaw();

		// check our bounds
		if (WorldPosition.z > CameraTopBound.WorldPosition.z)
		{
			IsOutsideOfTopBound = true;
		}
		else if (WorldPosition.z < CameraLowBound.WorldPosition.z)
		{
			IsOutsideOfLowBound = true;
		}

		// move our targets around
		if (!CharacterController.IsOnGround)
		{
			if (IsOutsideOfTopBound)
			{
				CameraTarget.WorldPosition = CameraTarget.WorldPosition.WithZ(WorldPosition.z + CameraBaseTargetBaseOffset);
			}
			else if (IsOutsideOfLowBound)
			{
				CameraTarget.WorldPosition = CameraTarget.WorldPosition.WithZ(WorldPosition.z + CameraBaseTargetBaseOffset);
			}
			else
			{
				CameraTarget.WorldPosition = CameraTarget.WorldPosition.WithZ(LastJumpZWorldPos + CameraBaseTargetBaseOffset);
			}

			// ensure these are in the correct position
			CameraTopBound.WorldPosition = CameraTopBound.WorldPosition.WithZ(LastJumpZWorldPos + CameraTopTargetBaseOffset);
			CameraLowBound.WorldPosition = CameraLowBound.WorldPosition.WithZ(LastJumpZWorldPos + CameraLowTargetBaseOffset);
		}
		else
		{
			CameraTarget.LocalPosition = CameraTarget.LocalPosition.WithZ(CameraBaseTargetBaseOffset);
			CameraTopBound.LocalPosition = CameraTopBound.LocalPosition.WithZ(CameraTopTargetBaseOffset);
			CameraLowBound.LocalPosition = CameraLowBound.LocalPosition.WithZ(CameraLowTargetBaseOffset);

			IsOutsideOfTopBound = false;
			IsOutsideOfLowBound = false;
		}
	}

	const float DoubleJumpWindow = .2f;
	const float MaxSpeed = 300f;
	const float MaxCrouchSpeed = 150f;

	Vector3 WishMove = Vector3.Zero;
	TimeSince TimeSinceJump = 0;
	bool HadDoubleJumped = false;
	bool IsJumping = false;
	bool IsRolling = false;

	private void TickMovement()
	{
		var CameraYaw = PlayerCamera.GameObject.WorldRotation.Yaw();

		var WishInput = Input.AnalogMove.Normal;
		if (WishInput == 0 && IsRolling)
		{
			WishInput = Vector3.Forward;
		}

		WishMove = WishInput * Rotation.FromYaw(CameraYaw);

		var MaxTickSpeed = IsCrouching && !IsRolling ? MaxCrouchSpeed : MaxSpeed;
		if (IsRolling)
		{
			MaxTickSpeed *= 1.33f;
		}

		Log.Info(MaxTickSpeed);
		var WishVel = WishMove * MaxTickSpeed;

		if (CharacterController.IsOnGround)
		{
			if (IsJumping)
			{
				IsJumping = false;
				TimeSinceJump = 0;
			}

			var RequestDifference = CharacterController.Velocity - WishVel;
			var ShitLerpFactor = IsRolling ? 5 : 15;
			var ShitLerp = Vector3.Lerp(CharacterController.Velocity, WishVel, ShitLerpFactor / RequestDifference.Length);

			CharacterController.Velocity = ShitLerp.WithZ(0);

			CheckJump();
			CheckCrouch();
		}
		else
		{
			CharacterController.Velocity -= GameManager.Gravity * Time.Delta;
			CharacterController.Accelerate(WishVel / 3f);
		}

		CharacterController.Move();
	}

	private void CheckCrouch()
	{
		IsCrouching = Input.Down("Duck") && !IsJumping;

		if (IsCrouching && CharacterController.Velocity.Length >= MaxSpeed)
		{
			IsRolling = true;
		}
		else
		{
			IsRolling = false;
		}
	}

	private void CheckJump()
	{
		if (Input.Pressed("Jump"))
		{
			LastJumpZWorldPos = WorldPosition.z;
			IsJumping = true;

			var JumpStrength = 360f;
			if (TimeSinceJump < DoubleJumpWindow && !HadDoubleJumped)
			{
				HadDoubleJumped = true;
				IsOutsideOfTopBound = true;
				JumpStrength = 600f;
			}
			else
			{
				HadDoubleJumped = false;
			}

			var PunchVector = Vector3.Up * JumpStrength;
			if (HadDoubleJumped)
			{
				PunchVector += Rotation.FromYaw(WorldRotation.Yaw()) * Vector3.Forward * (CharacterController.Velocity.Length * .5f);
			}
			CharacterController.Punch(PunchVector);
		}
	}
}
