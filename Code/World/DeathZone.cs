using Microsoft.VisualBasic;
using Sandbox;

public sealed class DeathZone : Component
{
	[Property] public BoxCollider Box { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		Box.OnTriggerEnter += OnHeadCollide;
	}

	private void OnHeadCollide(Collider Collider)
	{
		if (!Networking.IsHost)
		{
			return;
		}

		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			PlayerPawn.TakeDamage_ServerOnly(99999);
		}
	}
}
