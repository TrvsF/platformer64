using Sandbox;

public sealed class Door : Component
{
	[Property] public Dictionary<ECollectable, int> CollectablesToOpen { get; set; }
	[Property] public GameObject DoorObject { get; set; }
	[Property] public GameObject FrameObject { get; set; }
	[Property] public Vector3 OpenDirection { get; set; } = Vector3.Up;

	public bool IsOpen { get; private set; } = false;

	Vector3 SpawnLocation;
	protected override void OnStart()
	{
		base.OnStart();

		SpawnLocation = DoorObject.WorldPosition;
	}

	TimeSince TimeSinceOpen = 0;
	protected override void OnUpdate()
	{
		if (!Networking.IsHost)
		{
			return;
		}

		foreach (var Collectable in CollectablesToOpen)
		{
			if (GameManager.GetCollectable_ServerOnly(Collectable.Key) < Collectable.Value)
			{
				TimeSinceOpen = 0;
				return;
			}
		}

		IsOpen = true;

		if (IsOpen)
		{
			var PosLerp = Vector3.Lerp(SpawnLocation, SpawnLocation + (OpenDirection * 100f), TimeSinceOpen * .1f);
			DoorObject.WorldPosition = PosLerp;
		}

		// stop ticking?
	}
}
