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

	[RequireComponent] public CharacterController CharacterController { get; private set; }
	[RequireComponent] public CitizenAnimationHelper AnimationHelper { get; private set; }

	[Sync] public float Yaw { get; private set; }

	private PlayerCamera PlayerCamera = null;

	///////////////////////////////////////////////////////////////////////////

	protected override void OnStart()
	{
		base.OnStart();

		CameraBaseTargetBaseOffset = CameraTarget.LocalPosition.z;
		CameraTopTargetBaseOffset = CameraTopBound.LocalPosition.z;

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
		AnimationHelper.DuckLevel = CharacterController.Velocity.z > 300f ? 0 : 0.5f;
	}

	///////////////////////////////////////////////////////////////////////////

	const float DamageCooldownTime = 0.8f;
	public TimeSince TimeSinceHealthChange = 0;
	public int Health { get; private set; } = 100;

	public void TakeDamage(int Damage)
	{
		if (TimeSinceHealthChange < DamageCooldownTime)
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
	private float LastJumpZWorldPos = 0f;
	private bool IsOutsideOfTopBound = false;

	private void TickCamera()
	{
		// HACK : keep hold of yaw for animations
		Yaw = PlayerCamera.WorldRotation.Yaw();

		// move our targets around
		if (!CharacterController.IsOnGround)
		{
			if (IsOutsideOfTopBound)
			{
				CameraTarget.WorldPosition = CameraTarget.WorldPosition.WithZ(WorldPosition.z + CameraBaseTargetBaseOffset);
			}
			else
			{
				CameraTarget.WorldPosition = CameraTarget.WorldPosition.WithZ(LastJumpZWorldPos + CameraBaseTargetBaseOffset);
			}

			CameraTopBound.WorldPosition = CameraTopBound.WorldPosition.WithZ(LastJumpZWorldPos + CameraTopTargetBaseOffset);
		}
		else
		{
			CameraTarget.LocalPosition = CameraTarget.LocalPosition.WithZ(CameraBaseTargetBaseOffset);
			CameraTopBound.LocalPosition = CameraTopBound.LocalPosition.WithZ(CameraTopTargetBaseOffset);
			IsOutsideOfTopBound = false;
		}

		if (WorldPosition.z > CameraTopBound.WorldPosition.z)
		{
			IsOutsideOfTopBound = true;
		}
	}

	const float MaxSpeed = 300f;
	const float MaxSprintSpeed = 420f;

	readonly Vector3 Gravity = new(0, 0, 1200);

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

			var JumpStrength = 360f;
			if (TimeSinceJump < .1f)
			{
				IsOutsideOfTopBound = true;
				JumpStrength = 500f;
			}

			CharacterController.Punch(Vector3.Up * JumpStrength);
		}
	}
}
