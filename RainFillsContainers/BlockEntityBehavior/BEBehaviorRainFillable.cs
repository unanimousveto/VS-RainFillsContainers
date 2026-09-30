using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class BEBehaviorRainFillable : BlockEntityBehavior {
    const int FILL_DELTA_MS = 5000;
    const float MINIMUM_PRECIPITATION = 0.4f;

    private WeatherSystemServer weatherSystem => Api.ModLoader.GetModSystem<WeatherSystemServer>();

    private long rainListener;

    public BEBehaviorRainFillable(BlockEntity blockentity) : base(blockentity) {}

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);

        BeginWaitingForRain();

        api.World.Logger.Debug(
            "Block at ({0}) started waiting for rain",
            [this.Blockentity.Pos]
        );
    }

    private void BeginWaitingForRain() {
        if (this.Api.Side == EnumAppSide.Server) {
            this.rainListener = this.Api.Event.RegisterGameTickListener(Update, FILL_DELTA_MS);
        }
    }

    private void StopWaitingForRain() {
        if (this.Api.Side == EnumAppSide.Server) {
            this.Api.Event.UnregisterGameTickListener(this.rainListener);
        }
    }

    private void Update(float deltaTime) {
        // Check if the block is receiving rain
        if (!IsRainingAt(this.Blockentity.Pos)) return;

        // Try to add water to the block
        ItemStack fluidStack = new(this.Api.World.GetItem(new AssetLocation("waterportion")), 1000);
        float addLitresAmount = 0.1f;

        if (this.Blockentity is BlockEntityGroundStorage groundStorage) {
            TryPutLiquidToGroundStorage(groundStorage, fluidStack, addLitresAmount);
        } else {
            TryPutLiquidToBlockEntity(this.Blockentity, fluidStack, addLitresAmount);
        }
    }

    public bool IsRainingAt(BlockPos position) {
        // Check for cover
        int localRainHeight = this.Api.World.BlockAccessor.GetRainMapHeightAt(
            position.X,
            position.Z
        );

        if (localRainHeight > position.Y) return false;

        // Check for sufficient rain
        float precipitationRate = this.weatherSystem.GetPrecipitation(position.ToVec3d());

        if (precipitationRate < MINIMUM_PRECIPITATION) return false;

        return true;
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

    public override void OnBlockUnloaded() {
        StopWaitingForRain();

        this.Api.World.Logger.Debug(
            "Block at ({0}) stopped waiting for rain (unloaded)",
            [this.Blockentity.Pos]
        );

        base.OnBlockUnloaded();
    }

    public override void OnBlockRemoved() {
        StopWaitingForRain();

        this.Api.World.Logger.Debug(
            "Block at ({0}) stopped waiting for rain (removed)",
            [this.Blockentity.Pos]
        );

        base.OnBlockRemoved();
    }
}
