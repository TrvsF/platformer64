using Sandbox;
using System;

public enum EAIState
{
	Idle,
	Spawn,
	Wander,
	Chase,
}

public sealed class WorldUtil
{
	public static void KnockbackPlayer(PlayerPawn Player, float Magnatude, bool IsStomp = false)
	{
		if (Player.IsInvunrable())
		{
			Log.Info("aaa");
			return;
		}

		var VelocityInverse = Player.CharacterController.Velocity * -1f;
		var VelocityInverseNormal = VelocityInverse.Normal;
		var Knockback = VelocityInverseNormal * Magnatude;

		if (IsStomp)
		{
			Knockback = Knockback.WithZ(650f);
		}
		else
		{
			Player.CharacterController.Velocity = 0;
		}

		Player.CharacterController.Punch(Knockback);
	}

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
