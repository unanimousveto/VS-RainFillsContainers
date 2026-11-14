using System;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RainFillsContainers.Api;

public class BEBRainAcceptingBlock : BlockEntityBehavior, IRainAcceptor
{
    protected RainFillsContainersModSystem modSystem = null!;
    protected WeatherSystemBase weatherSystem = null!;

    public virtual float FillRateMultiplier => 1.0f;

    public BEBRainAcceptingBlock(BlockEntity blockentity) : base(blockentity) {}

    /// <summary>
    /// Check if a block position is exposed to rain.
    /// </summary>
    /// <param name="pos">The position to check.</param>
    /// <returns>True if the given position may recieve rain, false otherwise.</returns>
    protected bool IsExposed(BlockPos pos)
    {
        return Api.World.BlockAccessor.GetRainMapHeightAt(pos) <= pos.Y;
    }

    /// <summary>
    /// Gets the current precipitation rate above the given coordinates.
    /// Most implementations will want to call <seealso cref="IsExposed"/>
    /// before calling this method.
    /// </summary>
    /// <param name="pos">The position to check.</param>
    /// <returns>The current precipitation rate above the given position.</returns>
    protected float GetPrecipitationAt(BlockPos pos)
    {
        return weatherSystem.GetPrecipitation(pos.X, pos.Y, pos.Z);
    }

    public virtual float GetEffectivePrecipitation()
    {
        if (!IsExposed(Blockentity.Pos)) return 0.0f;
        return GetPrecipitationAt(Blockentity.Pos);
    }

    public virtual bool IsPrecipitationSnow()
    {
        var weatherData = weatherSystem.getWeatherDataReader().BlendedWeatherData;
        var precipitationType = weatherData.BlendedPrecType;

        if (precipitationType == EnumPrecipitationType.Snow)
        {
            return true;
        }
        else if (precipitationType == EnumPrecipitationType.Auto)
        {
            return Api.World.BlockAccessor.GetClimateAt(Blockentity.Pos).Temperature < weatherData.snowThresholdTemp;
        }
        else return false;
    }

    /// <summary>
    /// Attempt to add water to the given container block.
    /// </summary>
    /// <param name="container">The container to add to.</param>
    /// <param name="desiredLitres">The ammount of water to add.</param>
    /// <param name="requireWater">
    /// Whether water must be present in the container before more can be
    /// added.
    /// </param>
    /// <returns>The ammount of water sucessfully added.</returns>
    protected float TryToAddWaterToBlock(BlockLiquidContainerBase container, float desiredLitres, bool requireWater)
    {
        var waterItem = Api.World.GetItem(new AssetLocation("waterportion"));
        if (requireWater && container.GetContent(Blockentity.Pos)?.Id != waterItem?.Id) return 0.0f;

        var waterStack = new ItemStack(waterItem, 1000);
        return container.TryPutLiquid(Blockentity.Pos, waterStack, desiredLitres);
    }

    /// <summary>
    /// Attempt to add water to the given container item stack.
    /// </summary>
    /// <param name="itemStack">The item stack with the container.</param>
    /// <param name="desiredLitres">The ammount of water to add.</param>
    /// <param name="requireWater">
    /// Whether water must be present in the container before more can be
    /// added.
    /// </param>
    /// <returns>The ammount of water sucessfully added.</returns>
    protected float TryToAddWaterToItemStack(ItemStack? itemStack, float desiredLitres, bool requireWater)
    {
        if (itemStack?.Collectible is BlockLiquidContainerBase container)
        {
            var waterItem = Api.World.GetItem(new AssetLocation("waterportion"));
            if (requireWater && container.GetContent(Blockentity.Pos)?.Id != waterItem?.Id) return 0.0f;

            var waterStack = new ItemStack(waterItem, 1000);
            return container.TryPutLiquid(itemStack, waterStack, desiredLitres);
        }

        return 0.0f;
    }

    public virtual float TryToAddWater(float desiredLitres, bool requireWater = false)
    {
        if (Block is not BlockLiquidContainerBase container)
        {
            modSystem.Mod.Logger.Error($"Block `{Block.GetType().FullName}` does not qualify as a generic, rain-accepting container.");
            return 0.0f;
        }

        return TryToAddWaterToBlock(container, desiredLitres, requireWater);
    }

    [MemberNotNull(nameof(modSystem), nameof(weatherSystem))]
    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        modSystem = this.Api.ModLoader.GetModSystem<RainFillsContainersModSystem>();
        if (modSystem is null) throw new InvalidOperationException("Unable to get mod system.");

        weatherSystem = Api.ModLoader.GetModSystem<WeatherSystemBase>();
        if (weatherSystem is null) throw new InvalidOperationException("Unable to get weather system.");

        if (Api.Side != EnumAppSide.Server) return;
        modSystem.AddContainer(this);
        modSystem.Mod.Logger.Debug($"Block of type `{Block.GetType().FullName}` at ({Blockentity.Pos}) started waiting for rain.");
    }
    
    public override void OnBlockRemoved()
    {
        if (Api.Side != EnumAppSide.Server) return;

        modSystem.RemoveContainer(this);
        modSystem.Mod.Logger.Debug($"Block of type `{Block.GetType().FullName}` at ({Blockentity.Pos}) stopped waiting for rain (removed).");

        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        if (Api.Side != EnumAppSide.Server) return;

        modSystem.RemoveContainer(this);
        modSystem.Mod.Logger.Debug($"Block of type `{Block.GetType().FullName}` at ({Blockentity.Pos}) stopped waiting for rain (unloaded).");

        base.OnBlockUnloaded();
    }
}
