using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    // General settings
    public float FillRate { get; private set; }
    public bool SnowRequiresWater { get; private set; }
    public float SnowFillRate { get; private set; }
    public float GroundStorageFillRateMultiplier { get; private set; }
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
                    block is BlockGroundStorage
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
        this.SnowRequiresWater = config.snowRequiresWater;
        this.SnowFillRate = config.snowFillRate;
        this.GroundStorageFillRateMultiplier = config.groundStorageFillRateMultiplier;
        this.RainCheckDeltaMS = config.rainCheckDeltaMS;
        this.MinimumPrecipitation = config.minimumPrecipitation;
        
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
