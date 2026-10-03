using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    // General settings
    public float FillRate { get; private set; }
    public int RainCheckDeltaMS { get; private set; }

    // Weather constants
    public float MinimumPrecipitation { get; private set; }
    public float SnowThresholdTemp { get; private set; }

    public override void Start(ICoreAPI api) {
        base.Start(api);

        api.RegisterBlockEntityBehaviorClass(
            "RainFillsContainers.RainFillable",
            typeof(BEBehaviorRainFillable)
        );

        Mod.Logger.Event("Started 'RainFillsContainers' mod");
    }

    public override void AssetsFinalize(ICoreAPI api) {
        base.AssetsFinalize(api);

        // Don't run on the client
        if (api.Side != EnumAppSide.Server) return;

        // Load user config or defaults
        TryLoadConfig(api);

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

                Mod.Logger.VerboseDebug(
                    "Added 'RainFillable' block entity behavior to '{0}'",
                    block.Code.GetName()
                );

                affectedBlocksCount++;
            }
        }

        Mod.Logger.Debug(
            "Added 'RainFillable' block entity behavior to {0} blocks",
            [affectedBlocksCount]
        );
    }

    private void TryLoadConfig(ICoreAPI api) {
        const string CONFIG_NAME = "RainFillsContainers.json";

        RainFillSContainersConfig? config = null;

        try {
            config = api.LoadModConfig<RainFillSContainersConfig>(CONFIG_NAME);
        } catch {
            Mod.Logger.Error(
                "The config file 'RainFillsContainers.json' could not be loaded"
            );
        }

        // No file found (or the file was invalid), use defaults
        if (config is null) {
            config = new RainFillSContainersConfig();

            Mod.Logger.Debug(
                "Using 'RainFillsContainers' default configuration"
            );

            // Create a new config file with the defaults
            api.StoreModConfig<RainFillSContainersConfig>(config, CONFIG_NAME);
        }

        // Set configured values
        this.FillRate = config.fillRate * (config.rainCheckDeltaMS / 5000);
        this.RainCheckDeltaMS = config.rainCheckDeltaMS;
        this.MinimumPrecipitation = config.minimumPrecipitation;
    }
}
