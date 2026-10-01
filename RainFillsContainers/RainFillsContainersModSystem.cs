using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    // General settings
    public float FillRate;
    public int RainCheckDeltaMS;

    // Weather constants
    public float MinimumPrecipitation;
    public float SnowThresholdTemp;

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

        // Don't run on the client
        if (api.Side != EnumAppSide.Server) return;

        // Set constants
        this.FillRate = 1.0f;
        this.RainCheckDeltaMS = 5000;

        this.MinimumPrecipitation = 0.04f;
        
        WeatherSystemServer weatherSystem = api.ModLoader.GetModSystem<WeatherSystemServer>();
        WeatherDataReader weatherData = weatherSystem.getWeatherDataReader();

        this.SnowThresholdTemp = weatherData.BlendedWeatherData.snowThresholdTemp;

        // Add the RainFillable behavior to blocks
        int affectedBlocksCount = 0;

        foreach (Block block in api.World.Blocks) {
            if (
                block is not BlockPitkiln &&
                block is not BlockBoiler &&
                (
                    block is BlockLiquidContainerBase ||
                    block is BlockGroundStorage
                )
            ) {
                BlockEntityBehaviorType behavior = new() {
                    Name = "RainFillsContainers.RainFillable",
                    properties = null
                };

                block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(behavior);

                api.World.Logger.VerboseDebug(
                    "Added 'RainFillable' block entity behavior to '{0}'",
                    block.Code.GetName()
                );

                affectedBlocksCount++;
            }
        }

        api.World.Logger.Debug(
            "Added 'RainFillable' block entity behavior to {0} blocks",
            [affectedBlocksCount]
        );
    }
}
