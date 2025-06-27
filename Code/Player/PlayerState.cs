using Sandbox;
using Sandbox.Citizen;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Threading;

public sealed class PlayerState : Component
{
	public static PlayerState Local { get; private set; }
	public Connection Connection { get; private set; }
	public bool IsConnected => Connection != null && Connection.IsActive;

	[Property] public GameObject DefaultPlayerPawnPrefab { get; private set; }

	[Sync(SyncFlags.FromHost), Property] public PlayerPawn PlayerPawn { get; private set; }
	[Sync(SyncFlags.FromHost), Property] public ulong SteamId { get; private set; }
	[Sync(SyncFlags.FromHost), Property] public string SteamName { get; private set; }
	[Sync(SyncFlags.FromHost), Property] public string PingString { get; set; }

	public bool Initilize_ServerOnly(Connection ConnectionIn)
	{
		Assert.True(Networking.IsHost);
		Assert.NotNull(ConnectionIn);

		Connection = ConnectionIn;
		SteamId = Connection.SteamId;
		SteamName = Connection.DisplayName;

		using (Rpc.FilterInclude(Connection))
		{
			ClientInitilize();
		}

		return true;
	}

	[Rpc.Broadcast]
	public void ClientInitilize()
	{
		Local = this;
	}

	public void OnKill()
	{
		HostRequestServerSpawn();
	}

	[Rpc.Host]
	private void HostRequestServerSpawn()
	{
		SpawnPlayerPawn_ServerOnly(GameManager.GetRandomPlayerSpawn());
	}

	public void SpawnPlayerPawn_ServerOnly(Transform SpawnTransform)
	{
		Assert.True(Networking.IsHost);

		var SpawnPlayerPawnPrefab = DefaultPlayerPawnPrefab.Clone(SpawnTransform, null, true);
		SpawnPlayerPawnPrefab.Network.SetOrphanedMode(NetworkOrphaned.Destroy);

		var SpawnPlayerPawnComponent = SpawnPlayerPawnPrefab.Components.Get<PlayerPawn>();
		Assert.NotNull(SpawnPlayerPawnComponent);

		if (!SpawnPlayerPawnPrefab.NetworkSpawn(Connection))
		{
			SpawnPlayerPawnPrefab.Destroy();
			return;
		}

		PlayerPawn = SpawnPlayerPawnComponent;
		PlayerPawn.OnDeath += OnKill;
	}

	public bool IsPaused { get; set; } = false;

	// HACK : doing the pause here because we can't listen
	// to Input.EscapePressed within the UI update method..
	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Local != this)
		{
			return;
		}

		if (Input.EscapePressed)
		{
			Input.EscapePressed = false;
			IsPaused = !IsPaused;
		}
	}
}
