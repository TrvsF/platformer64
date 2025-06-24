using Sandbox;
using System;

public sealed class WorldUtil
{
	public static bool GetClosestPlayerInRange(Scene Scene, Vector3 SearchPoint, float Radius, out PlayerPawn PlayerPawn)
	{
		var BestDistance = float.PositiveInfinity;
		PlayerPawn = null;

		foreach (var FoundPlayerPawn in Scene.GetAllComponents<PlayerPawn>())
		{
			var Distance = Math.Abs((SearchPoint - FoundPlayerPawn.GameObject.WorldPosition).Length);
			if (Distance < Radius && Distance < BestDistance)
			{
				PlayerPawn = FoundPlayerPawn;
				BestDistance = Distance;
			}
		}

		return PlayerPawn.IsValid();
	}
}
