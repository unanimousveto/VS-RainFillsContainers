using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    public override void Start(ICoreAPI api) {
        base.Start(api);

        api.RegisterBlockEntityBehaviorClass(
            "RainFillsContainers.RainFillable",
            typeof(BEBehaviorRainFillable)
        );

        api.World.Logger.Event("Started 'RainFillsContainers' mod");
    }

    public override void AssetsFinalize(ICoreAPI api) {
        base.AssetsFinalize(api);

        int affectedBlocksCount = 0;

        foreach (Block block in api.World.Blocks) {
            if (block is BlockLiquidContainerBase || block is BlockGroundStorage) {
                BlockEntityBehaviorType behavior = new() {
                    Name = "RainFillsContainers.RainFillable",
                    properties = null
                };

                block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(behavior);

                affectedBlocksCount++;
            }
        }

        api.World.Logger.Debug(
            "Added 'RainFillable' block entity behavior to {0} blocks",
            [affectedBlocksCount]
        );
    }
}
