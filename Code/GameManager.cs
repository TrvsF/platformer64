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

	// TODO : move?
	static readonly public Vector3 Gravity = new(0, 0, 1200);

	protected override async Task OnLoad()
	{
		await base.OnLoad();

		if (Scene.IsEditor)
		{
			return;
		}

		if (!IsProxy)
		{
			StartClient(Connection.Local);
		}
	}

	protected override void OnDestroy()
	{
		PlayerStates.Clear();

		base.OnDestroy();
	}

	///////////////////////////////////////////////////////////////////////////

	private static TimeSince TimeSinceStart;
	[Sync(SyncFlags.FromHost)] private static NetDictionary<ECollectable, int> CollectableQuantities { get; set; }
	[Sync(SyncFlags.FromHost)] private static NetDictionary<ECollectable, int> CollectablesCollected { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		if (!Networking.IsHost)
		{
			return;
		}

		CollectableQuantities = new()
		{
			{ ECollectable.Disc, 0 },
			{ ECollectable.Egg, 0 },
		};

		foreach (var Collectable in Scene.GetAllComponents<Collectable>())
		{
			++CollectableQuantities[Collectable.CollectableType];
		}

		ResetGame_ServerOnly();
	}

	private static void ResetGame_ServerOnly()
	{
		Assert.True(Networking.IsHost);

		TimeSinceStart = 0;

		CollectablesCollected = new()
		{
			{ ECollectable.Disc, 0 },
			{ ECollectable.Egg, 0 },
		};
	}

	public static int GetCollectable_ServerOnly(ECollectable CollectableType)
	{
		Assert.True(Networking.IsHost);
		return CollectablesCollected[CollectableType];
	}

	public static void OnCollect_ServerOnly(Scene Scene, ECollectable CollectableType, int Collectables = 1)
	{
		Assert.True(Networking.IsHost);

		var Num = CollectablesCollected[CollectableType] += Collectables;
		if (Num >= CollectableQuantities[CollectableType])
		{
			Log.Info($"found all of {CollectableType}!!!");

			if (CollectableType == ECollectable.Egg)
			{
				double Time = TimeSinceStart;
				BroadcastOnGameOver(Scene, Time, CollectableQuantities.ToDictionary(), CollectablesCollected.ToDictionary());
			}
		}
	}

	public static string GetCollectableString()
	{
		var CollectableString = "";

		if (Networking.IsHost)
		{
			foreach (var CollectablePair in CollectablesCollected)
			{
				CollectableString += $"{CollectablePair.Key.ToString().ToLower()}s : {CollectablePair.Value}\n";
			}
		}

		return CollectableString;
	}

	[Rpc.Broadcast]
	public static void BroadcastOnGameOver(Scene Scene, double Time, Dictionary<ECollectable, int> InCollectableQuantities, Dictionary<ECollectable, int> InCollectableCollected)
	{
		Log.Info($"finished in {Time}");

		var TooSlow = false;
		if (Sandbox.Services.Stats.LocalPlayer.TryGet(LeaderboardText.StatName, out var BestTime))
		{
			if (Time > BestTime.LastValue)
			{
				TooSlow = true;
			}
		}

		if (!TooSlow)
		{
			Sandbox.Services.Stats.SetValue(LeaderboardText.StatName, Time);
		}

		if (Networking.IsHost)
		{
			foreach (var LeaderboardText in Scene.GetAllComponents<LeaderboardText>())
			{
				LeaderboardText.RefreshAllText(Time, InCollectableQuantities, InCollectableCollected);
			}

			var XOffset = 50;
			var C = 1;
			foreach (var Player in PlayerStates)
			{
				if (Player == null || Player.PlayerPawn == null)
				{
					continue;
				}

				Player.PlayerPawn.TeleportTo(new(250f + (XOffset * C), -757.7184f, 2218.035f));
				++C;
			}
			// ResetGame_ServerOnly();
		}
	}

	///////////////////////////////////////////////////////////////////////////

	public static bool CreateLobby(string LobbyName = "awesomelobby", LobbyPrivacy Privacy = LobbyPrivacy.Public)
	{
		LobbyConfig Config = new();
		Config.Name = LobbyName;
		Config.DestroyWhenHostLeaves = false;
		Config.MaxPlayers = 4;
		Config.Privacy = Privacy;

		Networking.CreateLobby(Config);

		return true;
	}

	///////////////////////////////////////////////////////////////////////////

	public static Transform GetRandomPlayerSpawn()
	{
		Assert.NotNull(Game.ActiveScene);

		var Spawn = Random.Shared.FromList(Game.ActiveScene.GetAllComponents<PlayerSpawn>().ToList());
		return Spawn.WorldTransform;
	}

	private void StartClient(Connection ConnectionChannel)
	{
		bool CreatedPlayerState = CreatePlayerState_ServerOnly(ConnectionChannel, out GameObject PlayerState, out PlayerState PlayerStateComponent);
		PlayerStateComponent.SpawnPlayerPawn_ServerOnly(GetRandomPlayerSpawn());

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
		Assert.True(Networking.IsHost);

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

		Game.ActiveScene.LoadFromFile("scenes/Level.scene");
	}
}
