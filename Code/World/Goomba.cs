using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.ComponentModel.Design.Serialization;

namespace Sandbox;

public sealed class Goomba : AiComponent
{
	[Property] public BoxCollider HeadBox { get; set; }
	[Property] public BoxCollider BodyBox { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		AIState = EAIState.Wander;

		HeadBox.OnTriggerEnter += OnHeadCollide;
		BodyBox.OnTriggerEnter += OnBodyCollide;
	}

	////////////////////////////////////////////////////////////////

	// called when spawned as a child
	public void OnSpawn()
	{
		AIState = EAIState.Spawn;
	}

	private TimeSince TimeSinceWanderLocationChange = 999;
	private PlayerPawn ChasePlayer = null;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if (!Networking.IsHost)
		{
			return;
		}

		switch (AIState)
		{

			// done when 'relicating', burst out from parent & spin for a bit
			case EAIState.Spawn:
				if (CharacterController.Velocity.Length <= 5)
				{
					SetStateWander();
					break;
				}

				// slow them down as they yeet out
				var VelX = MathX.Lerp(CharacterController.Velocity.x, 0, .05f);
				var VelY = MathX.Lerp(CharacterController.Velocity.y, 0, .05f);

				CharacterController.Velocity = new(VelX, VelY, CharacterController.Velocity.z);

				// spin
				var Yaw = WorldRotation.Yaw();
				WorldRotation = Rotation.FromYaw(Yaw + Random.Shared.Int(0, 5));

				// check vel
				break;

			// wander round in short bursts, dont tred too far from the spawn location
			// if someone gets close chase them KILL
			case EAIState.Wander:
				const int MaxWanderDistance = 333;

				// check for someone to KILL
				const float MaxPlayerChaseDistance = 100f;
				if (WorldUtil.GetClosestPlayerInRange(Scene, WorldPosition, MaxPlayerChaseDistance, out var PlayerPawn))
				{
					ChasePlayer = PlayerPawn;

					FaceLocation(ChasePlayer.WorldPosition);
					MoveForward();

					AIState = EAIState.Chase;
					break;
				}

				if (TimeSinceWanderLocationChange > Random.Shared.Int(10, 30))
				{
					TimeSinceWanderLocationChange = 0;

					// if we're too far return home
					var DistanceFromSpawn = Vector3.DistanceBetween(SpawnLocation, WorldPosition);
					if (DistanceFromSpawn > MaxWanderDistance)
					{
						FaceLocation(SpawnLocation);
						MoveForward();

						break;
					}

					WorldRotation = Rotation.FromYaw(Random.Shared.Int(0, 360));
					MoveForward();
				}

				break;

			// we're chasing someone, periodically check 
			case EAIState.Chase:
				if (!ChasePlayer.IsValid())
				{
					SetStateWander();
					break;
				}

				var DistanceToPlayer = Vector3.DistanceBetween(WorldPosition, ChasePlayer.WorldPosition);
				if (DistanceToPlayer > 250)
				{
					SetStateWander();
				}
				else
				{
					FaceLocation(ChasePlayer.WorldPosition);
					MoveForward();
				}

				break;
		}

		// make sure we're not gonna fall off a ledge or run into a wall
		var LedgePosition = WorldPosition + (WorldRotation.Forward * 100f);
		var LedgeTrace = Scene.Trace.Ray(LedgePosition, LedgePosition + Vector3.Down * 100f).IgnoreGameObject(GameObject).Run();
		var WallTrace = Scene.Trace.Ray(WorldPosition, LedgePosition).IgnoreGameObject(GameObject).WithoutTags("player").Run();
		if (!LedgeTrace.Hit || WallTrace.Hit)
		{
			WorldRotation = Rotation.FromYaw(WorldRotation.Yaw() + 180f);
			MoveForward();
		}

		if (!CharacterController.IsOnGround)
		{
			CharacterController.Velocity -= GameManager.Gravity * Time.Delta;
		}
	}

	////////////////////////////////////////////////////////////////

	private void OnHeadCollide(Collider Collider)
	{
		if (!Networking.IsHost || TimeSinceSpawn < .33f || IsDead)
		{
			return;
		}

		if (!Collider.Tags.Contains("feet"))
		{
			return;
		}

		IsDead = true;

		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			WorldUtil.KnockbackPlayer(PlayerPawn, 0, true);

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
			WorldUtil.KnockbackPlayer(PlayerPawn, 800f, false);
			PlayerPawn.TakeDamage(25);
		}
	}

	////////////////////////////////////////////////////////////////

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
