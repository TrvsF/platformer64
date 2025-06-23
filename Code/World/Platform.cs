using Sandbox;
using System.Numerics;

public sealed class Platform : Component
{
	[Property] public BoxCollider ForwardBox { get; set; }
	[Property] public BoxCollider BackwardBox { get; set; }
	[Property] public BoxCollider LeftBox { get; set; }
	[Property] public BoxCollider RightBox { get; set; }

	[RequireComponent] public Rigidbody Rigidbody { get; private set; }

	[Sync] NetList<PlayerPawn> PlayerPawns { get; set; } = new();   

	Vector3 MoveDir = Vector3.Zero;

	protected override void OnStart()
	{
		base.OnStart();

		BindBox(ForwardBox, Vector3.Left);
		BindBox(BackwardBox, Vector3.Right);
		BindBox(LeftBox, Vector3.Backward);
		BindBox(RightBox, Vector3.Forward);
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if (!Networking.IsHost)
		{
			return;
		}

		foreach (var Player in PlayerPawns)
		{
			Player.GameObject.Root.WorldPosition += MoveDir;
		}

		const int PhysicsTicks = 50;
		Rigidbody.Velocity = MoveDir * PhysicsTicks;
	}

	private void BindBox(BoxCollider Box, Vector3 Direction)
	{
		Box.OnTriggerEnter += Collider => OnBoxEnter(Collider, Direction);
		Box.OnTriggerExit += Collider => OnBoxLeave(Collider, Direction);
	}

	private void OnBoxEnter(Collider Collider, Vector3 Direction)
	{
		if (!Networking.IsHost)
		{
			return;
		}

		var PlayerPawn = Collider.GameObject.Root.GetComponent<PlayerPawn>();
		if (PlayerPawn == null)
		{
			return;
		}

		PlayerPawns.Add(PlayerPawn);
		MoveDir += Direction;
	}

	private void OnBoxLeave(Collider Collider, Vector3 Direction)
	{
		if (!Networking.IsHost)
		{
			return;
		}

		var PlayerPawn = Collider.GameObject.Root.GetComponent<PlayerPawn>();
		if (PlayerPawn == null)
		{
			return;
		}

		PlayerPawns.Remove(PlayerPawn);
		MoveDir -= Direction;
	}
}
