using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RainFillsContainers.Api;

public class BEBRainAcceptingShelf : BEBRainAcceptingBlock
{
    public static float ConfigurableFillRate { get; set; } = 0.25f;
    public override float FillRateMultiplier { get { return ConfigurableFillRate; } }

    public BEBRainAcceptingShelf(BlockEntity blockentity) : base(blockentity) {}

    public override float TryToAddWater(float desiredLitres, bool requireWater = false)
    {
        if (Blockentity is not BlockEntityShelf shelf)
        {
            modSystem.Mod.Logger.Error($"Block `{Block.GetType().FullName}` does not inherit from `{typeof(BlockShelf).FullName}`.");
            return 0.0f;
        }

        float totalLitresAdded = 0.0f;

        for (int slotIndex = 4; slotIndex < 8; slotIndex++)
        {
            var stack = shelf.Inventory[slotIndex].Itemstack;
            totalLitresAdded += TryToAddWaterToItemStack(stack, desiredLitres, requireWater);

            var layout = BlockEntityShelf.GetShelvableLayout(stack);
            if (slotIndex == 4 && layout == EnumShelvableLayout.SingleCenter)
            {
                break;
            }
            else if (slotIndex % 2 == 0 && layout == EnumShelvableLayout.Halves)
            {
                slotIndex++;
            }
        }

        if (totalLitresAdded > 0.0f) shelf.MarkDirty(true);
        return totalLitresAdded;
    }
}
