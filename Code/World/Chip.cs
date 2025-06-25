using Microsoft.VisualBasic;
using Sandbox;

public sealed class Chip : AiComponent
{
	[Property] public BoxCollider BodyBox { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		AIState = EAIState.Wander;

		BodyBox.OnTriggerEnter += OnBodyCollide;
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

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if (!Networking.IsHost)
		{
			return;
		}

		switch (AIState)
		{
			case EAIState.Idle:
				break;
			case EAIState.Spawn:
				break;
			case EAIState.Wander:
				WorldRotation = Rotation.FromYaw(WorldRotation.Yaw() + 1);
				CharacterController.IsOnGround = true;
				MoveForward();
				break;
			case EAIState.Chase:
				break;
		}
	}

	public void OnSpawn()
	{
		AIState = EAIState.Spawn;
	}
}
