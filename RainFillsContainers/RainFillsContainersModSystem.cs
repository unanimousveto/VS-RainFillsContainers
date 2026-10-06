using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    const string CONFIG_NAME = "RainFillsContainers.json";

    // General settings
    public float FillRate { get; private set; }
    public bool SnowRequiresWater { get; private set; }
    public float SnowFillRateMultiplier { get; private set; }
    public float SmallStorageFillRateMultiplier { get; private set; }
    public int RainCheckDeltaMS { get; private set; }

    // Weather constants
    public float MinimumPrecipitation { get; private set; }
    public float SnowThresholdTemp { get; private set; }

    // Block blacklist
    private string[] BlockBlacklist = [];

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
            this.BlockBlacklist.Length
        );
    }

    private void TryLoadConfig(ICoreAPI api) {
        RainFillSContainersConfig defaultConfig = new();
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
            config = defaultConfig;

            Mod.Logger.Debug(
                "Using 'RainFillsContainers' default configuration"
            );
        }

        // Validate configuration values
        if (config.fillRate > 0.0f) {
            this.FillRate = config.fillRate;
        } else {
            this.FillRate = defaultConfig.fillRate;
            config.fillRate = defaultConfig.fillRate;

            this.Mod.Logger.Error(
                "Configuration 'fillRate' must be a float greater than 0, " +
                "switching to default value {0}",
                defaultConfig.fillRate
            );
        }

        if (config.snowFillRateMultiplier > 0.0f) {
            this.SnowFillRateMultiplier = config.snowFillRateMultiplier;
        } else {
            this.SnowFillRateMultiplier = defaultConfig.snowFillRateMultiplier;
            config.snowFillRateMultiplier = defaultConfig.snowFillRateMultiplier;

            this.Mod.Logger.Error(
                "Configuration 'snowFillRateMultiplier' must be greater than 0, " +
                "switching to default value {0}",
                defaultConfig.snowFillRateMultiplier
            );
        }

        if (config.smallStorageFillRateMultiplier > 0.0f) {
            this.SmallStorageFillRateMultiplier = config.smallStorageFillRateMultiplier;
        } else {
            this.SmallStorageFillRateMultiplier = defaultConfig.smallStorageFillRateMultiplier;
            config.smallStorageFillRateMultiplier = defaultConfig.smallStorageFillRateMultiplier;

            this.Mod.Logger.Error(
                "Configuration 'smallStorageFillRateMultiplier' must be greater than 0, " +
                "switching to default value {0}",
                defaultConfig.smallStorageFillRateMultiplier
            );
        }

        if (config.rainCheckDeltaMS > 0) {
            this.RainCheckDeltaMS = config.rainCheckDeltaMS;
        } else {
            this.RainCheckDeltaMS = defaultConfig.rainCheckDeltaMS;
            config.rainCheckDeltaMS = defaultConfig.rainCheckDeltaMS;

            this.Mod.Logger.Error(
                "Configuration 'rainCheckDeltaMS' must be greater than 0, " +
                "switching to default value {0}",
                defaultConfig.rainCheckDeltaMS
            );
        }

        if (config.minimumPrecipitation >= 0.0f) {
            this.MinimumPrecipitation = config.minimumPrecipitation;
        } else {
            this.MinimumPrecipitation = defaultConfig.minimumPrecipitation;
            config.minimumPrecipitation = defaultConfig.minimumPrecipitation;

            this.Mod.Logger.Error(
                "Configuration 'minimumPrecipitation' must be greater than or equal to 0, " +
                "switching to default value {0}",
                defaultConfig.minimumPrecipitation
            );
        }

        this.SnowRequiresWater = config.snowRequiresWater;

        // Write the validated config to prevent future errors,
        // and add any previously unset options
        api.StoreModConfig<RainFillSContainersConfig>(config, CONFIG_NAME);
        
        // Transcribe block blacklist, taking care to add domains to vanilla blocks
        List<string> tempBlocklist = [];

        foreach (string blockName in config.blockBlacklist) {
            if (!blockName.Contains(':')) {
                tempBlocklist.Add("game:" + blockName);
            } else {
                tempBlocklist.Add(blockName);
            }
        }

        this.BlockBlacklist = tempBlocklist.ToArray();
    }

    private bool IsBlockBlacklisted(Block block) {
        foreach (string entry in this.BlockBlacklist) {
            if (WildcardUtil.Match(AssetLocation.Create(entry), block.Code)) return true;
        }

        return false;
    }
}
