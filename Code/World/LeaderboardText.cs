using Sandbox;

public enum ETextMode
{
	Leaderboard,
	Overview,
}

public sealed class LeaderboardText : Component
{
	public static string StatName { get; set; } = "RunTime";
	
	[Property] public ETextMode TextMode { get; set; } = ETextMode.Leaderboard;
	[RequireComponent] public TextRenderer LeaderboardTextRenderer { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		RefreshAllText();
	}

	public void RefreshAllText(double Time = 0, Dictionary<ECollectable, int> InCollectableQuantities = default, Dictionary<ECollectable, int> InCollectableCollected = default)
	{
		switch (TextMode)
		{
			case ETextMode.Leaderboard:
				RefreshLeaderboardText();
				break;
			case ETextMode.Overview:
				RefreshOverviewText(Time, InCollectableQuantities, InCollectableCollected);
				break;
		}
	}

	public void RefreshOverviewText(double Time, Dictionary<ECollectable, int> InCollectableQuantities, Dictionary<ECollectable, int> InCollectableCollected)
	{
		if (InCollectableQuantities == default)
		{
			return;
		}

		LeaderboardTextRenderer.Text = "";
		LeaderboardTextRenderer.Text += $"Finished in {Time:0.00}s\n";
		foreach (var Quantity in InCollectableQuantities)
		{
			LeaderboardTextRenderer.Text += $"{Quantity.Key}s {InCollectableCollected[Quantity.Key]}/{Quantity.Value}\n";
		}
	}

	public async void RefreshLeaderboardText()
	{
		const string HeaderText = "FASTEST TIMES";
		LeaderboardTextRenderer.Text = "";

		var Fullboard = Sandbox.Services.Leaderboards.GetFromStat(Game.Ident, StatName);
		Fullboard.SetSortAscending();
		Fullboard.SetAggregationLast();
		await Fullboard.Refresh();

		LeaderboardTextRenderer.Text = $"{HeaderText}\n";
		foreach (var TimeEntry in Fullboard.Entries)
		{
			LeaderboardTextRenderer.Text += $"{TimeEntry.Rank}. {TimeEntry.DisplayName} : {TimeEntry.Value:0.00}s\n";
		}
	}
}
