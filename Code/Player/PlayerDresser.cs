using Sandbox;
using Sandbox.Diagnostics;
using System.Numerics;

namespace KOTH;

public sealed class PlayerDresser : Component, Component.INetworkSpawn
{
	[Property] public SkinnedModelRenderer ModelRenderer { get; set; }
	[Property] public List<ClothingContainer.ClothingEntry> Clothing { get; set; }

	public ClothingContainer EquippedClothes { get; private set; } = null;
	string ClotheJSON { get; set; }

	public void OnNetworkSpawn(Connection Owner)
	{
		ClotheJSON = Owner.GetUserData("avatar");

		ApplyClothing();
	}

	public void ApplyClothing()
	{
		Assert.IsValid(ModelRenderer);

		EquippedClothes = ClotheJSON == "" ? new ClothingContainer() : ClothingContainer.CreateFromJson(ClotheJSON);

		EquippedClothes.AddRange(Clothing);
		EquippedClothes.Normalize();
		EquippedClothes.Apply(ModelRenderer);
	}
}
