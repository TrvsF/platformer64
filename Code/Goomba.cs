using Sandbox;
using Sandbox.Diagnostics;
using System;

public sealed class Goomba : Component
{
	[Property] public BoxCollider HeadBox { get; set; }
	[Property] public BoxCollider BodyBox { get; set; }

	[RequireComponent] public CharacterController CharacterController { get; private set; }

	private TimeSince TimeSinceSpawn = 0;
	public bool IsQuater = false;

	protected override void OnStart()
	{
		base.OnStart();

		HeadBox.OnTriggerEnter += OnHeadCollide;
		BodyBox.OnTriggerEnter += OnBodyCollide;

		TimeSinceSpawn = 0;
	}

	Vector3 Gravity = new(0, 0, 1200);

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		var VelX = MathX.Lerp(CharacterController.Velocity.x, 0, .05f);
		var VelY = MathX.Lerp(CharacterController.Velocity.y, 0, .05f);

		CharacterController.Velocity = new(VelX, VelY, CharacterController.Velocity.z);

		if (!CharacterController.IsOnGround)
		{
			CharacterController.Velocity -= Gravity * Time.Delta;
		}

		CharacterController.Move();
	}

	private void OnHeadCollide(Collider Collider)
	{
		if (!Networking.IsHost)
		{
			return;
		}

		if (TimeSinceSpawn < .5f)
		{
			return;
		}

		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			Log.Info($"{PlayerPawn} STOMP");

			var Knockback = (PlayerPawn.CharacterController.Velocity * -200f).WithZ(200f);
			Knockback = Knockback.ClampLength(200f);
			PlayerPawn.CharacterController.Punch(Knockback);

			if (IsQuater)
			{
				DestroyGameObject();
				return;
			}

			List<Vector3> SpawnQuadrents = [];
			SpawnQuadrents.Add(new(50, 50, 50));
			SpawnQuadrents.Add(new(-50, 50, 50));
			SpawnQuadrents.Add(new(50, -50, 50));
			SpawnQuadrents.Add(new(-50, -50, 50));

			for (int QuaterChildIndex = 0; QuaterChildIndex < 4; ++QuaterChildIndex)
			{
				var SpawnPlayerPawnPrefab = GameObject.Clone(WorldTransform, null, true);
				SpawnPlayerPawnPrefab.Network.SetOrphanedMode(NetworkOrphaned.Destroy);

				var SpawnedGoomba = SpawnPlayerPawnPrefab.Components.Get<Goomba>();
				Assert.NotNull(SpawnedGoomba);

				SpawnedGoomba.IsQuater = true;
				SpawnedGoomba.CharacterController.Velocity = SpawnQuadrents[QuaterChildIndex] * 4f;

				if (!SpawnPlayerPawnPrefab.NetworkSpawn(Connection.Host))
				{
					SpawnPlayerPawnPrefab.Destroy();
					return;
				}
			}

			DestroyGameObject();
		}
	}

	private void OnBodyCollide(Collider Collider)
	{
		if (!Networking.IsHost)
		{
			return;
		}

		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			Log.Info($"{PlayerPawn} Ouch!");
		}
	}
}
