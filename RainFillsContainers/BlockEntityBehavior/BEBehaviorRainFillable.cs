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
        // Check if it's raining enough
        float precipitation = GetPrecipitation();
        if (precipitation < this.ModSystem.MinimumPrecipitation) return;

        // Get fill rate based on weather type (snow or rain)
        float evaluatedFillRate = this.ModSystem.FillRate;
        if (IsSnowTemp()) evaluatedFillRate = this.ModSystem.SnowFillRate;

        if (evaluatedFillRate == 0.0f) return;

        ItemStack fluidStack = new(this.Api.World.GetItem(new AssetLocation("waterportion")), 1000);

        // Calculate ammount of water to add
        this.partialPortions += precipitation * evaluatedFillRate / 0.4f;

        int portionsToAdd = (int) this.partialPortions;
        this.partialPortions -= portionsToAdd;

        float addLitresAmount = 0.1f * portionsToAdd;

        // Try to add water to the block
        if (this.Blockentity is BlockEntityGroundStorage groundStorage) {
            TryPutLiquidToGroundStorage(groundStorage, fluidStack, addLitresAmount);
        } else {
            TryPutLiquidToBlockEntity(this.Blockentity, fluidStack, addLitresAmount);
        }
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

    private static float TryPutLiquidToGroundStorage(BlockEntityGroundStorage groundStorage, ItemStack fluidStack, float quantityLitres) {
        EnumGroundStorageLayout layout = groundStorage.StorageProps.Layout;

        int searchSlotCount = layout switch {
            EnumGroundStorageLayout.SingleCenter => 1,
            EnumGroundStorageLayout.Quadrants => 4,
            _ => 0
        };
        
        float totalLitresAdded = 0;

        for (int slotIndex = 0; slotIndex < searchSlotCount; slotIndex++) {
            ItemSlot item = groundStorage.Inventory[slotIndex];

            if (item.Itemstack?.Collectible is BlockLiquidContainerBase container &&
                !container.IsFull(item.Itemstack)
            ) {
                totalLitresAdded += container.TryPutLiquid(item.Itemstack, fluidStack, quantityLitres);
            }
        }

        if (totalLitresAdded > 0) groundStorage.MarkDirty(true);

        return totalLitresAdded;
    }

    private static float TryPutLiquidToBlockEntity(BlockEntity blockEntity, ItemStack fluidStack, float quantityLitres) {
        if (blockEntity.Block is BlockLiquidContainerBase container &&
            !container.IsFull(blockEntity.Pos)
        ) {
            return container.TryPutLiquid(blockEntity.Pos, fluidStack, quantityLitres);
        }

        return 0;
    }
}
