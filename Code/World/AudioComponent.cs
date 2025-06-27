using System;
using System.Threading.Tasks;
using static Sandbox.Gizmo;

public sealed class AudioComponent : Component
{
	private static List<FSound> StoppedSounds { get; set; }
	private static Dictionary<FSound, SoundHandle> SoundHandles { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();
		SoundHandles = new Dictionary<FSound, SoundHandle>();
		StoppedSounds = new List<FSound>();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		foreach (var soundHandle in SoundHandles)
		{
			if (!soundHandle.Key.Owner.IsValid())
			{
				soundHandle.Value.Stop();
				StoppedSounds.Add(soundHandle.Key);
				continue;
			}
			if (soundHandle is { Value: not null, Value.Finished: false, Key.UpdatePosition: true, }
				&& soundHandle.Value.Position != soundHandle.Key.Owner.WorldPosition
				)
			{
				soundHandle.Value.Position = soundHandle.Key.Owner.WorldPosition;
			}

			if (!soundHandle.Value.IsValid() || soundHandle.Value.Finished || soundHandle.Value.IsStopped)
			{
				StoppedSounds.Add(soundHandle.Key);
			}
		}

		CleanUpStoppedSounds();
	}

	[Rpc.Broadcast]
	public static void PlaySound(FSound soundEvent)
	{
		if (soundEvent.SoundEvent == null)
		{
			Log.Warning("invalid sound event");
			return;
		}

		var alreadyPlaying = SoundHandles.Any(x => x.Key.Owner.Id == soundEvent.Owner.Id || x.Key.SoundId == soundEvent.SoundId);
		if (alreadyPlaying) return;
		Log.Info("Playing sound " + soundEvent.SoundId);
		var handle = Sound.Play(soundEvent.SoundEvent, soundEvent.Position);
		SoundHandles.Add(soundEvent, handle);
	}

	[Rpc.Broadcast]
	public static void StopSound(FSound soundEvent)
	{
		var soundHandle = SoundHandles.FirstOrDefault(x => x.Key.SoundId == soundEvent.SoundId);
		var alreadyRemoving = StoppedSounds.Any(x => x.SoundId == soundEvent.SoundId);
		if (soundHandle.Value == null || alreadyRemoving) return;
		soundHandle.Value.Stop();
		StoppedSounds.Add(soundHandle.Key);
	}

	public static bool IsPlayingSound(FSound soundEvent)
	{
		return SoundHandles.Any(x => x.Key.SoundId == soundEvent.SoundId);
	}

	private static void CleanUpStoppedSounds()
	{
		foreach (var soundHandle in StoppedSounds)
		{
			SoundHandles.Remove(soundHandle);
		}

		StoppedSounds.Clear();
	}
}

public struct FSound : IEquatable<FSound>
{
	public SoundEvent SoundEvent { get; init; }
	public Vector3 Position { get; init; }
	public Guid SoundId { get; init; }
	public Component Owner { get; init; }

	public bool UpdatePosition { get; init; } = false;

	public FSound(SoundEvent soundEvent, Vector3 position, Component owner, bool updatePosition = false)
	{
		SoundEvent = soundEvent;
		Position = position;
		Owner = owner;
		UpdatePosition = updatePosition;
		SoundId = Guid.NewGuid();
	}

	public bool Equals(FSound other)
	{
		return other.SoundId == SoundId;
	}

	public override bool Equals(object obj)
	{
		return obj is FSound other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(SoundEvent, UpdatePosition, SoundId);
	}
}
