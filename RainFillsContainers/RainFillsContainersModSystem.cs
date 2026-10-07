using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class RainFillsContainersModSystem : ModSystem {
    const string CONFIG_NAME = "RainFillsContainers.json";

    private ICoreAPI Api;

    private RainFillsContainersConfig config = new();
    private RainFillsContainersConfig defaultConfig = new();

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
        this.Api = api;

        // Load user config or defaults
        TryLoadConfig();

        WeatherSystemServer weatherSystem = this.Api.ModLoader.GetModSystem<WeatherSystemServer>();
        WeatherDataReader weatherData = weatherSystem.getWeatherDataReader();

        this.SnowThresholdTemp = weatherData.BlendedWeatherData.snowThresholdTemp;

        // Add the RainFillable behavior to blocks
        int affectedBlocksCount = 0;
        int blacklistedBlocksCount = 0;

        foreach (Block block in this.Api.World.Blocks) {
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

        RegisterCommands();
    }

    private void TryLoadConfig() {
        try {
            this.config = this.Api.LoadModConfig<RainFillsContainersConfig>(CONFIG_NAME);
        } catch {
            Mod.Logger.Error(
                "The config file 'RainFillsContainers.json' could not be loaded"
            );
        }

        // No file found (or the file was invalid), use defaults
        if (this.config is null) {
            this.config = new();

            Mod.Logger.Debug(
                "Using 'RainFillsContainers' default configuration"
            );
        }

        // Validate configuration values
        if (this.config.rainCheckDeltaMS <= 0) {
            this.config.rainCheckDeltaMS = this.defaultConfig.rainCheckDeltaMS;

            this.Mod.Logger.Warning(
                "Configuration 'rainCheckDeltaMS' must be greater than 0, " +
                "switching to default value {0}",
                this.defaultConfig.rainCheckDeltaMS
            );
        }

        if (this.config.minimumPrecipitation < 0.0f) {
            this.config.minimumPrecipitation = this.defaultConfig.minimumPrecipitation;

            this.Mod.Logger.Warning(
                "Configuration 'minimumPrecipitation' must be greater than or equal to 0, " +
                "switching to default value {0}",
                this.defaultConfig.minimumPrecipitation
            );
        }

        if (this.config.fillRate < 0.0f) {
            this.config.fillRate = this.defaultConfig.fillRate;

            this.Mod.Logger.Warning(
                "Configuration 'fillRate' must be a float greater than 0, " +
                "switching to default value {0}",
                this.defaultConfig.fillRate
            );
        }

        if (this.config.smallStorageFillRateMultiplier < 0.0f) {
            this.config.smallStorageFillRateMultiplier = this.defaultConfig.smallStorageFillRateMultiplier;

            this.Mod.Logger.Warning(
                "Configuration 'smallStorageFillRateMultiplier' must be greater than 0, " +
                "switching to default value {0}",
                this.defaultConfig.smallStorageFillRateMultiplier
            );
        }

        if (this.config.snowFillRateMultiplier < 0.0f) {
            this.config.snowFillRateMultiplier = this.defaultConfig.snowFillRateMultiplier;

            this.Mod.Logger.Warning(
                "Configuration 'snowFillRateMultiplier' must be greater than 0, " +
                "switching to default value {0}",
                this.defaultConfig.snowFillRateMultiplier
            );
        }

        // Write the validated config to prevent future errors,
        // and add any previously unset options
        // (also ensures that the stored defaults match the file)
        UpdateConfigFile();
    }

    private bool IsBlockBlacklisted(Block block) {
        foreach (string entry in this.config.blockBlacklist) {
            if (WildcardUtil.Match(AssetLocation.Create(entry), block.Code)) return true;
        }

        return false;
    }
    
    private void RegisterCommands() {
        this.Api.ChatCommands.Create("rainfillscontainers")
        .WithDescription(
            "Running without a subcommand will show active configuration values."
        )
        .RequiresPrivilege(Privilege.controlserver)
        .HandleWith(CmdGetInfo)

        // Sub-commands to override values
        .BeginSubCommand("minprecip")
            .WithDescription(
                "Running with a negative argument resets the value to match the config file. " +
                "Otherwise, the minimum precipitation level will be overridden " +
                "(note that fill rate scales linearly with precipitation rate, " +
                "so modifications to this cutoff value will not frequently be apparent)."
            )
            .WithArgs(this.Api.ChatCommands.Parsers.Float("precipitation-level"))
            .HandleWith(CmdOverrideMinimumPrecipitation)
        .EndSubCommand()

        .BeginSubCommand("fillrate")
            .WithDescription(
                "Running with a negative argument resets the value to match the config file. " +
                "Otherwise, the fill rate will be overridden."
            )
            .WithArgs(this.Api.ChatCommands.Parsers.Float("fill-rate"))
            .HandleWith(CmdOverrideFillRate)
        .EndSubCommand()

        .BeginSubCommand("smallfillrate")
            .WithDescription(
                "Running with a negative argument resets the value to match the config file. " +
                "Otherwise, the small storage fill rate multiplier will be overridden."
            )
            .WithArgs(this.Api.ChatCommands.Parsers.Float("small-storage-fill-multiplier"))
            .HandleWith(CmdOverrideSmallStorageFillRate)
        .EndSubCommand()

        .BeginSubCommand("snowfillrate")
            .WithDescription(
                "Running with a negative argument resets the value to match the config file. " +
                "Otherwise, the snow fill rate multiplier will be overridden."
            )
            .WithArgs(this.Api.ChatCommands.Parsers.Float("snow-fill-multiplier"))
            .HandleWith(CmdOverrideSnowFillRate)
        .EndSubCommand()

        .BeginSubCommand("snowrequireswater")
            .WithDescription(
                "Overrides the requirement of water before snow will melt into a container."
            )
            .WithArgs(this.Api.ChatCommands.Parsers.Bool("require-water"))
            .HandleWith(CmdOverrideSnowRequiresWater)
        .EndSubCommand()

        // Sub-command to restore all overrides
        .BeginSubCommand("restoresettings")
            .WithDescription(
                "Restores all overridden configuration settings to the values originally loaded from the config file."
            )
            .HandleWith(CmdRestoreAllConfigSettings)
        .EndSubCommand()

        // Sub-command to update the config file with current override values
        .BeginSubCommand("updateconfig")
            .WithDescription(
                "Updates the 'rainfillscontainers' mod configuration file with the current game configuration."
            )
            .HandleWith(CmdUpdateConfig)
        .EndSubCommand();
    }

    private TextCommandResult CmdGetInfo(TextCommandCallingArgs args) {
        StringBuilder sb = new();
        sb.AppendLine("Current configuration");
        sb.AppendLine("---------------------");
        sb.AppendLine(string.Format("Rain check every {0}ms", this.config.rainCheckDeltaMS));

        bool overridden = this.config.minimumPrecipitation != this.defaultConfig.minimumPrecipitation;
        sb.AppendLine(string.Format(
            "Minimum precipitation: {0}{1}",
            this.config.minimumPrecipitation,
            overridden ? " [overridden]" : ""
        ));

        overridden = this.config.fillRate != this.defaultConfig.fillRate;
        sb.AppendLine(string.Format(
            "Fill rate: {0}{1}",
            this.config.fillRate,
            overridden ? " [overridden]" : ""
        ));

        overridden = this.config.smallStorageFillRateMultiplier != this.defaultConfig.smallStorageFillRateMultiplier;
        sb.AppendLine(string.Format(
            "Small storage fill rate multiplier: {0}{1}",
            this.config.smallStorageFillRateMultiplier,
            overridden ? " [overridden]" : ""
        ));

        overridden = this.config.snowFillRateMultiplier != this.defaultConfig.snowFillRateMultiplier;
        sb.AppendLine(string.Format(
            "Snow fill rate multiplier: {0}{1}",
            this.config.snowFillRateMultiplier,
            overridden ? " [overridden]" : ""
        ));

        overridden = this.config.snowRequiresWater != this.defaultConfig.snowRequiresWater;
        sb.AppendLine(string.Format(
            "Snow requires water: {0}{1}",
            this.config.snowRequiresWater,
            overridden ? " [overridden]" : ""
        ));

        sb.AppendLine();

        return TextCommandResult.Success(sb.ToString());
    }

    private TextCommandResult CmdOverrideMinimumPrecipitation(TextCommandCallingArgs args) {
        float newValue = (float) args.Parsers[0].GetValue();
        bool wasReset = newValue < 0.0f;
        string verb = wasReset ? "reset" : "set";

        if (newValue < 0.0f) {
            RestoreMinimumPrecipitation();
        } else {
            OverrideMinimumPrecipitation(newValue);
        }

        return TextCommandResult.Success(string.Format(
            "Minimum precipitation level {0} to {1}", verb, this.config.minimumPrecipitation
        ));
    }

    private TextCommandResult CmdOverrideFillRate(TextCommandCallingArgs args) {
        float newValue = (float) args.Parsers[0].GetValue();
        bool wasReset = newValue < 0.0f;
        string verb = wasReset ? "reset" : "set";

        if (wasReset) {
            RestoreFillRate();
        } else {
            OverrideFillRate(newValue);
        }

        return TextCommandResult.Success(string.Format(
            "Fill rate {0} to {1}", verb, this.config.fillRate
        ));
    }

    private TextCommandResult CmdOverrideSmallStorageFillRate(TextCommandCallingArgs args) {
        float newValue = (float) args.Parsers[0].GetValue();
        bool wasReset = newValue < 0.0f;
        string verb = wasReset ? "reset" : "set";

        if (wasReset) {
            RestoreSmallStorageFillRateMultiplier();
        } else {
            OverrideSmallStorageFillRateMultiplier(newValue);
        }

        return TextCommandResult.Success(string.Format(
            "Small storage fill rate multiplier {0} to {1}", verb, this.config.smallStorageFillRateMultiplier
        ));
    }

    private TextCommandResult CmdOverrideSnowFillRate(TextCommandCallingArgs args) {
        float newValue = (float) args.Parsers[0].GetValue();
        bool wasReset = newValue < 0.0f;
        string verb = wasReset ? "reset" : "set";

        if (wasReset) {
            RestoreSnowFillRateMultiplier();
        } else {
            OverrideSnowFillRateMultiplier(newValue);
        }

        return TextCommandResult.Success(string.Format(
            "Snow fill rate multiplier {0} to {1}", verb, this.config.snowFillRateMultiplier
        ));
    }

    private TextCommandResult CmdOverrideSnowRequiresWater(TextCommandCallingArgs args) {
        bool newValue = (bool) args.Parsers[0].GetValue();

        OverrideSnowRequiresWater(newValue);

        return TextCommandResult.Success(string.Format(
            "Snow {0}requires water already in the container to begin filling containers",
            newValue == this.defaultConfig.snowRequiresWater ? "now " : "no longer "
        ));
    }

    private TextCommandResult CmdRestoreAllConfigSettings(TextCommandCallingArgs args) {
        RestoreMinimumPrecipitation();
        RestoreFillRate();
        RestoreSmallStorageFillRateMultiplier();
        RestoreSnowFillRateMultiplier();
        RestoreSnowRequiresWater();

        return TextCommandResult.Success("Restored configurations to match the config file.");
    }

    private TextCommandResult CmdUpdateConfig(TextCommandCallingArgs args) {
        UpdateConfigFile();

        return TextCommandResult.Success("Updated configuration");
    }

    public float OverrideMinimumPrecipitation(float minimumPrecipitation) {
        if (minimumPrecipitation >= 0.0f) this.config.minimumPrecipitation = minimumPrecipitation;

        return this.config.minimumPrecipitation;
    }

    public void RestoreMinimumPrecipitation() {
        this.config.minimumPrecipitation = this.defaultConfig.minimumPrecipitation;
    }

    public float OverrideFillRate(float fillRate) {
        if (fillRate >= 0.0f) this.config.fillRate = fillRate;

        return this.config.fillRate;
    }

    public void RestoreFillRate() {
        this.config.fillRate = this.defaultConfig.fillRate;
    }

    public float OverrideSmallStorageFillRateMultiplier(float smallStorageFillRateMultiplier) {
        if (smallStorageFillRateMultiplier >= 0.0f) this.config.smallStorageFillRateMultiplier = smallStorageFillRateMultiplier;

        return this.config.smallStorageFillRateMultiplier;
    }

    public void RestoreSmallStorageFillRateMultiplier() {
        this.config.smallStorageFillRateMultiplier = this.defaultConfig.smallStorageFillRateMultiplier;
    }

    public float OverrideSnowFillRateMultiplier(float snowFillRateMultiplier) {
        if (snowFillRateMultiplier >= 0.0f) this.config.snowFillRateMultiplier = snowFillRateMultiplier;

        return this.config.snowFillRateMultiplier;
    }

    public void RestoreSnowFillRateMultiplier() {
        this.config.snowFillRateMultiplier = this.defaultConfig.snowFillRateMultiplier;
    }

    public bool OverrideSnowRequiresWater(bool snowRequiresWater) {
        this.config.snowRequiresWater = snowRequiresWater;

        return this.config.snowRequiresWater;
    }

    public void RestoreSnowRequiresWater() {
        this.config.snowRequiresWater = this.defaultConfig.snowRequiresWater;
    }

    private void UpdateConfigFile() {
        this.Api.StoreModConfig<RainFillsContainersConfig>(this.config, CONFIG_NAME);

        // Set defaults to match new config file
        this.defaultConfig = new RainFillsContainersConfig(this.config);
    }
}
