using Sandbox;

public sealed class Platform : Component
{
	[Property] public BoxCollider ForwardBox { get; set; }
	[Property] public BoxCollider BackwardBox { get; set; }
	[Property] public BoxCollider LeftBox { get; set; }
	[Property] public BoxCollider RightBox { get; set; }

	Vector3 MoveDir = Vector3.Zero;

	protected override void OnStart()
	{
		base.OnStart();

		BindBox(ForwardBox, Vector3.Forward);
		BindBox(BackwardBox, Vector3.Backward);
		BindBox(LeftBox, Vector3.Left);
		BindBox(RightBox, Vector3.Right);
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
	}

	private void BindBox(BoxCollider Box, Vector3 Direction)
	{
		Box.OnTriggerEnter += Collider => OnBoxEnter(Collider, Direction);
		Box.OnTriggerExit += Collider => OnBoxLeave(Collider, Direction);
	}

	private void OnBoxEnter(Collider Collider, Vector3 Direction)
	{
		Log.Info($"AAA {Direction}");

		if (!Networking.IsHost)
		{
			return;
		}

		MoveDir += Direction;
	}

	private void OnBoxLeave(Collider Collider, Vector3 Direction)
	{
		Log.Info($"BBB {Direction}");

		if (!Networking.IsHost)
		{
			return;
		}

		MoveDir -= Direction;
	}
}
