using Microsoft.VisualBasic;
using Sandbox;
using Sandbox.Services;
using System;

public abstract class AiComponent : Component
{
	[Property] public bool CanHaveChildren { get; set; } = false;
	[Property] public GameObject ChildPrefab { get; set; }

	[Property] public int Damage { get; set; } = 25;
	[Property] public int Speed { get; set; } = 30;

	[RequireComponent] public CharacterController CharacterController { get; set; }

	protected Vector3 SpawnLocation = Vector3.Zero;
	protected TimeSince TimeSinceSpawn = 0;
	protected bool IsDead = false;

	protected EAIState AIState = EAIState.Idle;

	protected sealed override void OnUpdate()
	{
		base.OnUpdate();

		CharacterController.Move();
	}

	protected override void OnStart()
	{
		base.OnStart();

		TimeSinceSpawn = 0;
		SpawnLocation = WorldPosition;
	}

	protected void FaceLocation(Vector3 Location)
	{
		var RotationTowardPlayer = Rotation.LookAt(Location - WorldPosition, Vector3.Up);
		WorldRotation = Rotation.FromYaw(RotationTowardPlayer.Yaw());
	}

	protected void SetStateWander()
	{
		WorldRotation = Rotation.FromYaw(Random.Shared.Int(0, 360));
		MoveForward();

		AIState = EAIState.Wander;
	}

	protected void MoveForward()
	{
		CharacterController.Velocity = Vector3.Forward * Speed * Rotation.FromYaw(WorldRotation.Yaw());
	}
}
