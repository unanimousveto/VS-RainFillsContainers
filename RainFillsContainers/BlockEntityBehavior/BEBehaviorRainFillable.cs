using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class BEBehaviorRainFillable : BlockEntityBehavior {
    private RainFillsContainersModSystem ModSystem;

    private WeatherSystemServer WeatherSystem;
    private long rainListener;
    private float partialPortions = 0;

    public BEBehaviorRainFillable(BlockEntity blockentity) : base(blockentity) {}

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);

        this.ModSystem = this.Api.ModLoader.GetModSystem<RainFillsContainersModSystem>();
        this.WeatherSystem = this.Api.ModLoader.GetModSystem<WeatherSystemServer>();

        // Don't run on the client
        if (api.Side != EnumAppSide.Server) return;

        BeginWaitingForRain();

        this.ModSystem.Mod.Logger.Debug(
            "Block at ({0}) started waiting for rain",
            [this.Blockentity.Pos]
        );
    }

    public override void OnBlockUnloaded() {
        StopWaitingForRain();

        this.ModSystem.Mod.Logger.Debug(
            "Block at ({0}) stopped waiting for rain (unloaded)",
            [this.Blockentity.Pos]
        );

        base.OnBlockUnloaded();
    }

    public override void OnBlockRemoved() {
        StopWaitingForRain();

        this.ModSystem.Mod.Logger.Debug(
            "Block at ({0}) stopped waiting for rain (removed)",
            [this.Blockentity.Pos]
        );

        base.OnBlockRemoved();
    }

    private void BeginWaitingForRain() {
        if (this.Api.Side == EnumAppSide.Server) {
            this.rainListener = this.Api.Event.RegisterGameTickListener(
                Update,
                this.ModSystem.RainCheckDeltaMS
            );
        }
    }

    private void StopWaitingForRain() {
        if (this.Api.Side == EnumAppSide.Server) {
            this.Api.Event.UnregisterGameTickListener(this.rainListener);
        }
    }

    private void Update(float deltaTime) {
        bool requireWater = false;

        // For barrels, check the seal state
        if (this.Blockentity is BlockEntityBarrel barrel && barrel.Sealed) return;

        // Check if it's raining enough
        float precipitation = GetPrecipitation();
        if (precipitation < this.ModSystem.MinimumPrecipitation) return;

        // Get fill rate based on weather type (snow or rain), adjusted for tick rate
        float evaluatedFillRate = this.ModSystem.FillRate * (this.ModSystem.RainCheckDeltaMS / 5000);

        if (IsSnowTemp()) {
            evaluatedFillRate *= this.ModSystem.SnowFillRateMultiplier;

            // If this is true, water must already be present in the container
            // before snow will melt and further fill the container
            requireWater = this.ModSystem.SnowRequiresWater;
        }

        if (evaluatedFillRate == 0.0f) return;

        // Adjust fill rate for small storages
        bool isGroundStorage = this.Blockentity is BlockEntityGroundStorage;
        bool isShelf = this.Blockentity is BlockEntityShelf;
        
        if (isGroundStorage || isShelf) evaluatedFillRate *= this.ModSystem.SmallStorageFillRateMultiplier;

        // Evaluate amount of rain added
        this.partialPortions += precipitation * evaluatedFillRate / 0.4f;

        // Too little to add
        if (this.partialPortions < 1.0f) return;

        int portionsToAdd = (int) this.partialPortions;
        this.partialPortions -= portionsToAdd;

        float addLitresAmount = 0.1f * portionsToAdd;

        // Try to add water to the block
        ItemStack fluidStack = new(this.Api.World.GetItem(new AssetLocation("waterportion")), 1000);
        float litresAdded = 0.0f;

        if (isGroundStorage) {
            litresAdded = TryPutLiquidToGroundStorage(
                (BlockEntityGroundStorage) this.Blockentity,
                fluidStack,
                addLitresAmount,
                requireWater
            );
        } else if (isShelf) {
            litresAdded = TryPutLiquidToTopShelf(
                (BlockEntityShelf) this.Blockentity,
                fluidStack,
                addLitresAmount,
                requireWater
            );
        } else {
            litresAdded = TryPutLiquidToBlockEntity(
                this.Blockentity,
                fluidStack,
                addLitresAmount,
                requireWater
            );
        }

        // Don't let partial portions accumulate if we aren't filling the container
        if (litresAdded == 0) this.partialPortions = 0.0f;
    }

    public bool IsSnowTemp() {
        BlockPos position = this.Blockentity.Pos;

        float localTemp = this.Api.World.BlockAccessor.GetClimateAt(
            position,
            EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
            Api.World.Calendar.TotalDays
        ).Temperature;

        return localTemp < this.ModSystem.SnowThresholdTemp;
    }

    public float GetPrecipitation() {
        BlockPos position = this.Blockentity.Pos;

        // Check for cover
        int localRainHeight = this.Api.World.BlockAccessor.GetRainMapHeightAt(
            position.X,
            position.Z
        );

        if (localRainHeight > position.Y) return 0.0f;

        // Check for sufficient rain
        float precipitationRate = this.WeatherSystem.GetPrecipitation(position.ToVec3d());

        return precipitationRate;
    }

    private static float TryPutLiquidToItemstack(ItemStack itemStack, ItemStack fluidStack, float quantityLitres, bool requireWater = false) {
        if (itemStack?.Collectible is BlockLiquidContainerBase container &&
            !container.IsFull(itemStack)
        ) {
            // For snow, make sure this container already holds water to melt into
            if (requireWater &&
                container.GetContent(itemStack)?.Id != fluidStack.Id
            ) return 0.0f;

            return container.TryPutLiquid(itemStack, fluidStack, quantityLitres);
        }

        return 0.0f;
    }

    private static float TryPutLiquidToGroundStorage(BlockEntityGroundStorage groundStorage, ItemStack fluidStack, float quantityLitres, bool requireWater = false) {
        EnumGroundStorageLayout layout = groundStorage.StorageProps.Layout;

        int searchSlotCount = layout switch {
            EnumGroundStorageLayout.SingleCenter => 1,
            EnumGroundStorageLayout.Quadrants => 4,
            _ => 0
        };
        
        float totalLitresAdded = 0.0f;

        for (int slotIndex = 0; slotIndex < searchSlotCount; slotIndex++) {
            ItemStack? stack = groundStorage.Inventory[slotIndex].Itemstack;
            if (stack == null) continue;

            totalLitresAdded += TryPutLiquidToItemstack(stack, fluidStack, quantityLitres, requireWater);
        }

        if (totalLitresAdded > 0) groundStorage.MarkDirty(true);

        return totalLitresAdded;
    }

    private static float TryPutLiquidToTopShelf(BlockEntityShelf shelf, ItemStack fluidStack, float quantityLitres, bool requireWater = false) {
        // Only check the top shelf (indices 4-7, inclusive)
        int startIndex = 4;

        // For the halved layout, only check every other slot
        int stride = 1;
        if (BlockEntityShelf.GetShelvableLayout(shelf.Inventory[4].Itemstack) == EnumShelvableLayout.Halves) stride = 2;

        float totalLitresAdded = 0.0f;

        for (int slotIndex = startIndex; slotIndex < 8; slotIndex += stride) {
            ItemStack? stack = shelf.Inventory[slotIndex].Itemstack;
            if (stack == null) continue;

            totalLitresAdded += TryPutLiquidToItemstack(stack, fluidStack, quantityLitres, requireWater);
        }

        if (totalLitresAdded > 0) shelf.MarkDirty(true);

        return totalLitresAdded;
    }

    private static float TryPutLiquidToBlockEntity(BlockEntity blockEntity, ItemStack fluidStack, float quantityLitres, bool requireWater = false) {
        if (blockEntity.Block is BlockLiquidContainerBase container &&
            !container.IsFull(blockEntity.Pos)
        ) {
            // For snow, make sure this container already holds water to melt into
            if (requireWater &&
                container.GetContent(blockEntity.Pos)?.Id != fluidStack.Id
            ) return 0.0f;

            return container.TryPutLiquid(blockEntity.Pos, fluidStack, quantityLitres);
        }

        return 0.0f;
    }
}
