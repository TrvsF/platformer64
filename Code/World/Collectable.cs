using Sandbox;
using System.Diagnostics;

public enum ECollectable
{
	Disc,
	Fast,
}

public sealed class Collectable : Component
{
	[Property] public ECollectable CollectableType { get; set; }
	[Property] public GameObject ModelObject { get; set; }
	[Property] public BoxCollider ColliderBox { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		ColliderBox.OnTriggerEnter += OnCollide;

		var DownTrace = Scene.Trace.Ray(WorldPosition, WorldPosition + Vector3.Down * 100f).IgnoreGameObject(GameObject).Run();
		WorldPosition = WorldPosition.WithZ(DownTrace.HitPosition.z + 25f);
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		var Yaw = GameObject.WorldRotation.Yaw();
		GameObject.WorldRotation = Rotation.FromYaw(Yaw + 2);
	}

	private void OnCollide(Collider Collider)
	{
		if (Collider.GameObject.Root.GetComponent<PlayerPawn>() is { } PlayerPawn)
		{
			GameManager.OnCollect(CollectableType);
			DestroyGameObject();
		}
	}
}
