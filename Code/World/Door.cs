using Sandbox;

public sealed class Door : Component
{
	[Property] public Dictionary<ECollectable, int> CollectablesToOpen { get; set; }
	[Property] public GameObject DoorObject { get; set; }
	[Property] public GameObject FrameObject { get; set; }

	public bool IsOpen { get; private set; } = false;

	float BaseDoorZ = 0;
	protected override void OnStart()
	{
		base.OnStart();

		BaseDoorZ = DoorObject.WorldPosition.z;
	}

	const float OpenZDiff = -100f;
	TimeSince TimeSinceOpen = 0;
	protected override void OnUpdate()
	{
		foreach (var Collectable in CollectablesToOpen)
		{
			if (GameManager.GetCollectable(Collectable.Key) < Collectable.Value)
			{
				TimeSinceOpen = 0;
				return;
			}
		}

		IsOpen = true;

		if (IsOpen)
		{
			var Lerp = MathX.Lerp(BaseDoorZ, BaseDoorZ + OpenZDiff, TimeSinceOpen * .1f);
			DoorObject.WorldPosition = DoorObject.WorldPosition.WithZ(Lerp);
		}

		// stop ticking?
	}
}
