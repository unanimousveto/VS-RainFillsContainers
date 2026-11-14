using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RainFillsContainers.Api;

public class BEBRainAcceptingBarrel : BEBRainAcceptingBlock
{
    public override float FillRateMultiplier => 1.0f;

    public BEBRainAcceptingBarrel(BlockEntity blockentity) : base(blockentity) {}

    public override float TryToAddWater(float desiredLitres, bool requireWater = false)
    {
        if (Blockentity is not BlockEntityBarrel barrel)
        {
            modSystem.Mod.Logger.Error($"Block `{Block.GetType().FullName}` does not inherit from `{typeof(BlockBarrel).FullName}`.");
            return 0.0f;
        }

        if (barrel.Sealed) return 0.0f;
        return base.TryToAddWater(desiredLitres, requireWater);
    }
}
