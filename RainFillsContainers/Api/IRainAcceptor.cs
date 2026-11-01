namespace RainFillsContainers.Api;

public interface IRainAcceptor
{
    /// <summary>
    /// Automatically used by the mod system to adjust rainwater ammounts
    /// added to this container.
    /// </summary>
    float FillRateMultiplier { get; }

    /// <summary>
    /// Get the precipitation this container is currently experiencing,
    /// adjusting for cover if applicable.
    /// </summary>
    /// <returns>The effective precipitation rate.</returns>
    float GetEffectivePrecipitation();

    /// <summary>
    /// Check if this container is receiving snow instead of rain.
    /// </summary>
    /// <returns>True if it's snowing, false otherwise.</returns>
    bool IsPrecipitationSnow();

    /// <summary>
    /// Attempt to add water to this container, respecting any applicable
    /// "sealed" states. Implementations of this method should treat
    /// <paramref name="desiredLitres"/> as a fully calculated value (they
    /// should not multiply it by the class' fill rate).
    /// </summary>
    /// <param name="desiredLitres">The maximum ammount of water to add.</param>
    /// <param name="requireWater">
    /// If true, the container must already contain some water before more
    /// can be added. By default this is used to prevent snow from melting
    /// in conditions where it should not.
    /// </param>
    /// <returns>The ammount of water sucessfully added.</returns>
    float TryToAddWater(float desiredLitres, bool requireWater = false);
}
