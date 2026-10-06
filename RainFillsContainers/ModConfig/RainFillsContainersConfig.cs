namespace RainFillsContainers;

public class RainFillSContainersConfig {
    public int rainCheckDeltaMS = 5000;
    public float minimumPrecipitation = 0.04f;
    public float fillRate = 1.0f;
    public float snowFillRateMultiplier = 0.5f;
    public float smallStorageFillRateMultiplier = 0.25f;
    public bool snowRequiresWater = true;

    public string[] blockBlacklist = [
        "verticalboiler-*"
    ];
}
