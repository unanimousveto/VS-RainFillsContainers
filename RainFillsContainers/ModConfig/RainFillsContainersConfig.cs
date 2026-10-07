using Vintagestory.API.Util;

namespace RainFillsContainers;

public class RainFillsContainersConfig {
    public int rainCheckDeltaMS = 5000;
    public float minimumPrecipitation = 0.04f;
    public float fillRate = 1.0f;
    public float smallStorageFillRateMultiplier = 0.25f;
    public float snowFillRateMultiplier = 0.5f;
    public bool snowRequiresWater = true;

    public string[] blockBlacklist = [
        "verticalboiler-*"
    ];
    public string[] itemBlacklist = [
        "jug-*"
    ];

    public RainFillsContainersConfig() {}

    // Copy constructor
    public RainFillsContainersConfig(RainFillsContainersConfig config) {
        this.rainCheckDeltaMS = config.rainCheckDeltaMS;
        this.minimumPrecipitation = config.minimumPrecipitation;
        this.fillRate = config.fillRate;
        this.snowFillRateMultiplier = config.snowFillRateMultiplier;
        this.smallStorageFillRateMultiplier = config.smallStorageFillRateMultiplier;
        this.snowRequiresWater = config.snowRequiresWater;
        this.blockBlacklist = config.blockBlacklist.FastCopy(config.blockBlacklist.Length);
        this.itemBlacklist = config.itemBlacklist.FastCopy(config.itemBlacklist.Length);
    }
}
