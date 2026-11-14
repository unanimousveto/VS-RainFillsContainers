using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RainFillsContainers.Api;

public class BEBRainAcceptingGroundStorage : BEBRainAcceptingBlock
{
    public static float ConfigurableFillRate { get; set; } = 0.25f;
    public override float FillRateMultiplier { get { return ConfigurableFillRate; } }

    public BEBRainAcceptingGroundStorage(BlockEntity blockentity) : base(blockentity) {}

    public override float TryToAddWater(float desiredLitres, bool requireWater = false)
    {
        if (Blockentity is not BlockEntityGroundStorage gs)
        {
            modSystem.Mod.Logger.Error($"Block `{Block.GetType().FullName}` does not inherit from `{typeof(BlockGroundStorage).FullName}`.");
            return 0.0f;
        }

        var layout = gs.StorageProps?.Layout;
        int searchSlotCount = layout switch {
            EnumGroundStorageLayout.SingleCenter => 1,
            EnumGroundStorageLayout.Quadrants => 4,
            _ => 0
        };
        
        float totalLitresAdded = 0.0f;

        for (int slotIndex = 0; slotIndex < searchSlotCount; slotIndex++) {
            var stack = gs.Inventory[slotIndex].Itemstack;
            totalLitresAdded += TryToAddWaterToItemStack(stack, desiredLitres, requireWater);
        }

        if (totalLitresAdded > 0.0f) gs.MarkDirty(true);
        return totalLitresAdded;
    }
}
