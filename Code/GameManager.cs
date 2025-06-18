using Sandbox;
using Sandbox.Citizen;
using Sandbox.Diagnostics;
using Sandbox.Network;
using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Transactions;

public enum EGameNetworkMode
{
	Singleplayer,
	Multiplayer,
	Menu,
}

public sealed class GameManager : Component, Component.INetworkListener
{
	[Sync(SyncFlags.FromHost)] public static NetList<PlayerState> PlayerStates { get; set; } = new();

	[Property] public GameObject PlayerStatePrefab { get; set; } = null;
	[Property] public EGameNetworkMode NetworkMode { get; set; } = EGameNetworkMode.Singleplayer;

	protected override async Task OnLoad()
	{
		await base.OnLoad();

		if (Scene.IsEditor)
		{
			return;
		}

		if (NetworkMode == EGameNetworkMode.Multiplayer)
		{
			if (Networking.IsActive)
			{
				// INetworkListener.OnActive will be called
				return;
			}

			bool Joined = false;

			if (!Game.IsEditor)
			{
				Joined = await Networking.JoinBestLobby(Game.Ident);
			}

			if (!Joined)
			{
				Log.Info("starting own lobby...");
				CreateLobby();
				// INetworkListener.OnActive will be called
			}
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if (NetworkMode == EGameNetworkMode.Singleplayer)
		{
			StartClient(Connection.Local);
		}
	}

	protected override void OnDestroy()
	{
		PlayerStates.Clear();

		base.OnDestroy();
	}

	private static bool CreateLobby(string LobbyName = "awesomelobby", LobbyPrivacy Privacy = LobbyPrivacy.Public)
	{
		LobbyConfig Config = new();
		Config.Name = LobbyName;
		Config.DestroyWhenHostLeaves = false;
		Config.MaxPlayers = 16;
		Config.Privacy = Privacy;

		Networking.CreateLobby(Config);

		return true;
	}

	///////////////////////////////////////////////////////////////////////////

	private Transform GetRandomPlayerSpawn()
	{
		Assert.NotNull(Game.ActiveScene);

		var Spawn = Random.Shared.FromList(Game.ActiveScene.GetAllComponents<PlayerSpawn>().ToList());
		return Spawn.WorldTransform;
	}

	private void StartClient(Connection ConnectionChannel)
	{
		bool CreatedPlayerState = CreatePlayerState_ServerOnly(ConnectionChannel, out GameObject PlayerState, out PlayerState PlayerStateComponent);
		PlayerStateComponent.SpawnPlayerPawn_ServerOnly(ConnectionChannel, GetRandomPlayerSpawn());

		if (!CreatedPlayerState)
		{
			Networking.Disconnect();
			throw new Exception($"Something went wrong when trying to create PlayerState for {ConnectionChannel.DisplayName}");
		}

		PlayerStates.Add(PlayerStateComponent);
	}

	private bool CreatePlayerState_ServerOnly(Connection ConnectionChannel, out GameObject PlayerState, out PlayerState PlayerStateComponent)
	{
		Assert.True(Networking.IsHost);
		Assert.True(PlayerStatePrefab.IsValid(), "Could not spawn player as no PlayerStatePrefab assigned to network manager");

		PlayerState = PlayerStatePrefab.Clone();
		PlayerState.Name = $"PlayerState:{ConnectionChannel.DisplayName}";
		PlayerState.Network.SetOrphanedMode(NetworkOrphaned.Destroy);
		PlayerState.NetworkSpawn(Connection.Host);

		PlayerStateComponent = PlayerState.Components.Get<PlayerState>();

		if (!PlayerStateComponent.IsValid())
		{
			throw new Exception($"Could not spawn player as no PlayerStatePrefab assigned to network manager for {ConnectionChannel.DisplayName}");
		}

		if (PlayerStateComponent.Initilize_ServerOnly(ConnectionChannel))
		{
			return true;
		}

		PlayerState.DestroyImmediate();
		return false;
	}

	///////////////////////////////////////////////////////////////////////////

	void INetworkListener.OnActive(Connection ConnectionChannel)
	{
		Log.Info($"Connection activating with name = {ConnectionChannel.DisplayName}:{ConnectionChannel.Ping} | is host = {ConnectionChannel.IsHost}");

		// HACK : to stop the host creating another state if they create a lobby in a singleplayer session
		if (ConnectionChannel == Connection.Host)
		{
			return;
		}

		StartClient(ConnectionChannel);
	}

	void INetworkListener.OnDisconnected(Connection ConnectionChannel)
	{
		// Assert.True(Networking.IsHost); // after changing hosts this assert fails :)

		Log.Info($"Disconnection from {ConnectionChannel}");

		PlayerState PlayerStateToDestroy = PlayerStates.FirstOrDefault(PlayerState => PlayerState.Connection == ConnectionChannel);

		if (PlayerStateToDestroy == null)
		{
			return;
		}

		PlayerStates.Remove(PlayerStateToDestroy);

		if (PlayerStateToDestroy.PlayerPawn.IsValid())
		{
			PlayerStateToDestroy.PlayerPawn.GameObject.Root.Destroy();
		}

		PlayerStateToDestroy.GameObject.Root.Destroy();
	}

	void INetworkListener.OnBecameHost(Connection PreviousHost)
	{
		Networking.Disconnect();

		// TODO : FIX
		// Game.ActiveScene.LoadFromFile("scenes/menu/menu.scene");
	}
}
