using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    const string CONFIG_NAME = "RainFillsContainers.json";

    private RainFillSContainersConfig config = new();

    // General settings
    public int RainCheckDeltaMS => config.rainCheckDeltaMS;
    public float FillRate => config.fillRate;
    public float SmallStorageFillRateMultiplier => config.smallStorageFillRateMultiplier;
    public float SnowFillRateMultiplier => config.snowFillRateMultiplier;
    public bool SnowRequiresWater => config.snowRequiresWater;

    // Weather constants
    public float MinimumPrecipitation => config.minimumPrecipitation;
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
        int blacklistedBlocksCount = 0;

        foreach (Block block in api.World.Blocks) {
            if (block is not BlockPitkiln &&
                (
                    block is BlockLiquidContainerBase ||
                    block is BlockGroundStorage ||
                    block is BlockShelf
                )
            ) {
                string blockName = block.Code.ToString();

                if (IsBlockBlacklisted(block)) {
                    this.Mod.Logger.VerboseDebug(
                        "Skipped adding 'RainFillable' block entity behavior to '{0}' (blacklisted)",
                        blockName
                    );

                    blacklistedBlocksCount++;
                    continue;
                }

                BlockEntityBehaviorType behavior = new() {
                    Name = "RainFillsContainers.RainFillable",
                    properties = null
                };

                block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(behavior);

                Mod.Logger.VerboseDebug(
                    "Added 'RainFillable' block entity behavior to '{0}'",
                    blockName
                );

                affectedBlocksCount++;
            }
        }

        Mod.Logger.Debug(
            "Added 'RainFillable' block entity behavior to {0} blocks",
            [affectedBlocksCount]
        );

        Mod.Logger.Debug(
            "Blacklist blocked {0} ({1} listed) blocks from receiving the 'RainFillable' block entity behavior",
            blacklistedBlocksCount,
            this.config.blockBlacklist.Length
        );
    }

    private void TryLoadConfig(ICoreAPI api) {
        RainFillSContainersConfig defaultConfig = new();

        try {
            this.config = api.LoadModConfig<RainFillSContainersConfig>(CONFIG_NAME);
        } catch {
            Mod.Logger.Error(
                "The config file 'RainFillsContainers.json' could not be loaded"
            );
        }

        // No file found (or the file was invalid), use defaults
        if (this.config is null) {
            this.config = defaultConfig;

            Mod.Logger.Debug(
                "Using 'RainFillsContainers' default configuration"
            );
        }

        // Validate configuration values
        if (this.config.rainCheckDeltaMS <= 0) {
            this.config.rainCheckDeltaMS = defaultConfig.rainCheckDeltaMS;

            this.Mod.Logger.Warning(
                "Configuration 'rainCheckDeltaMS' must be greater than 0, " +
                "switching to default value {0}",
                defaultConfig.rainCheckDeltaMS
            );
        }

        if (this.config.minimumPrecipitation < 0.0f) {
            this.config.minimumPrecipitation = defaultConfig.minimumPrecipitation;

            this.Mod.Logger.Warning(
                "Configuration 'minimumPrecipitation' must be greater than or equal to 0, " +
                "switching to default value {0}",
                defaultConfig.minimumPrecipitation
            );
        }

        if (this.config.fillRate <= 0.0f) {
            this.config.fillRate = defaultConfig.fillRate;

            this.Mod.Logger.Warning(
                "Configuration 'fillRate' must be a float greater than 0, " +
                "switching to default value {0}",
                defaultConfig.fillRate
            );
        }

        if (this.config.smallStorageFillRateMultiplier <= 0.0f) {
            this.config.smallStorageFillRateMultiplier = defaultConfig.smallStorageFillRateMultiplier;

            this.Mod.Logger.Warning(
                "Configuration 'smallStorageFillRateMultiplier' must be greater than 0, " +
                "switching to default value {0}",
                defaultConfig.smallStorageFillRateMultiplier
            );
        }

        if (this.config.snowFillRateMultiplier <= 0.0f) {
            this.config.snowFillRateMultiplier = defaultConfig.snowFillRateMultiplier;

            this.Mod.Logger.Warning(
                "Configuration 'snowFillRateMultiplier' must be greater than 0, " +
                "switching to default value {0}",
                defaultConfig.snowFillRateMultiplier
            );
        }

        // Write the validated config to prevent future errors,
        // and add any previously unset options
        api.StoreModConfig<RainFillSContainersConfig>(this.config, CONFIG_NAME);
    }

    private bool IsBlockBlacklisted(Block block) {
        foreach (string entry in this.config.blockBlacklist) {
            if (WildcardUtil.Match(AssetLocation.Create(entry), block.Code)) return true;
        }

        return false;
    }
}
