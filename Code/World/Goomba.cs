using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.ComponentModel.Design.Serialization;

enum EAIState
{
	Idle,
	Spawn,
	Wonder,
	Chase,
}

public sealed class Goomba : Component
{
	[Property] public BoxCollider HeadBox { get; set; }
	[Property] public BoxCollider BodyBox { get; set; }

	[Property] public bool CanHaveChildren { get; set; } = false;
	[Property] public GameObject ChildPrefab { get; set; }

	[RequireComponent] public CharacterController CharacterController { get; private set; }

	private TimeSince TimeSinceSpawn = 0;
	private bool IsDead = false;

	protected override void OnStart()
	{
		base.OnStart();

		HeadBox.OnTriggerEnter += OnHeadCollide;
		BodyBox.OnTriggerEnter += OnBodyCollide;

		TimeSinceSpawn = 0;
	}

	readonly Vector3 Gravity = new(0, 0, 1200);

	private EAIState AIState = EAIState.Idle;

	// called when spawned as a child
	public void OnSpawn()
	{
		AIState = EAIState.Spawn;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if (!Networking.IsHost)
		{
			return;
		}

		switch (AIState)
		{
			case EAIState.Spawn:
				var VelX = MathX.Lerp(CharacterController.Velocity.x, 0, .05f);
				var VelY = MathX.Lerp(CharacterController.Velocity.y, 0, .05f);

				CharacterController.Velocity = new(VelX, VelY, CharacterController.Velocity.z);
				break;
			case EAIState.Wonder:
				var Yaw = GameObject.WorldRotation.Yaw();
				GameObject.WorldRotation = Rotation.FromYaw(Yaw + 2);



				CharacterController.Velocity = Vector3.Backward.RotateAround(0, Rotation.FromYaw(Yaw + 2)) * 50;
				break;
		}

		if (!CharacterController.IsOnGround)
		{
			CharacterController.Velocity -= Gravity * Time.Delta;
		}

		if (CharacterController.Velocity == 0)
		{
			AIState = EAIState.Wonder;
		}

		if (AIState == EAIState.Wonder)
		{
			CheckForPlayer();
		}

		CharacterController.Move();
	}

	private void CheckForPlayer()
	{

	}

	private void OnHeadCollide(Collider Collider)
	{
		if (!Networking.IsHost || TimeSinceSpawn < .33f || IsDead)
		{
			return;
		}

		IsDead = true;

		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			Log.Info($"{PlayerPawn} STOMP");

			KnockbackPlayer(PlayerPawn, 0, true);

			if (CanHaveChildren)
			{
				SpawnChildren();
			}

			DestroyGameObject();
		}
	}

	private void OnBodyCollide(Collider Collider)
	{
		if (!Networking.IsHost || TimeSinceSpawn < .33f || IsDead)
		{
			return;
		}

		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			Log.Info($"{PlayerPawn} Ouch!");

			KnockbackPlayer(PlayerPawn, 800f, false);
			PlayerPawn.TakeDamage(25);
		}
	}

	private void KnockbackPlayer(PlayerPawn Player, float Magnatude, bool IsStomp = false)
	{
		if (Player.IsInvunrable())
		{
			return;
		}

		var VelocityInverse = Player.CharacterController.Velocity * -1f;
		var VelocityInverseNormal = VelocityInverse.Normal;
		var Knockback = VelocityInverseNormal * Magnatude;

		if (IsStomp)
		{
			Knockback = Knockback.WithZ(650f);
		}
		else
		{
			Player.CharacterController.Velocity = 0;
		}

		Player.CharacterController.Punch(Knockback);
	}

	private readonly List<Vector3> ChildSpawnVelocities =
	[
		new Vector3(250, 250, 250),
		new Vector3(-250, 250, 250),
		new Vector3(250, -250, 250),
		new Vector3(-250, -250, 250)
	];

	private void SpawnChildren()
	{
		for (int QuaterChildIndex = 0; QuaterChildIndex < 4; ++QuaterChildIndex)
		{
			var SpawnPlayerPawnPrefab = ChildPrefab.Clone(WorldTransform, null, true);
			SpawnPlayerPawnPrefab.Network.SetOrphanedMode(NetworkOrphaned.Destroy);

			var SpawnedGoomba = SpawnPlayerPawnPrefab.Components.Get<Goomba>();
			Assert.NotNull(SpawnedGoomba);

			SpawnedGoomba.CharacterController.Velocity = ChildSpawnVelocities[QuaterChildIndex];
			SpawnedGoomba.OnSpawn();

			if (!SpawnPlayerPawnPrefab.NetworkSpawn(Connection.Host))
			{
				SpawnPlayerPawnPrefab.Destroy();
				return;
			}
		}
	}
}
