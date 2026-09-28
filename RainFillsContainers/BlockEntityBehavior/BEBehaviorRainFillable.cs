using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RainFillsContainers;

public class BEBehaviorRainFillable : BlockEntityBehavior {
    const int FILL_DELTA_MS = 1000;

    private long fillListener;

    public BEBehaviorRainFillable(BlockEntity blockentity) : base(blockentity) {}

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);

        if (api.Side == EnumAppSide.Server) {
            this.fillListener = api.Event.RegisterGameTickListener(Update, FILL_DELTA_MS);

            this.Api.World.Logger.VerboseDebug(
                "Block at {0} started waiting for rain",
                [this.Blockentity.Pos]
            );
        }
    }

    private void Update(float deltaTime) {
        ItemStack fluidStack = new(this.Api.World.GetItem(new AssetLocation("waterportion")), 1000);
        float addLitresAmount = 0.1f;

        if (this.Blockentity is BlockEntityGroundStorage groundStorage) {
            TryPutLiquidToGroundStorage(groundStorage, fluidStack, addLitresAmount);
        } else {
            TryPutLiquidToBlockEntity(this.Blockentity, fluidStack, addLitresAmount);
        }
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

    public override void OnBlockRemoved() {
        this.Api.Event.UnregisterGameTickListener(this.fillListener);

        this.Api.World.Logger.VerboseDebug(
            "Block at {0} stopped waiting for rain",
            [this.Blockentity.Pos]
        );

        base.OnBlockRemoved();
    }
}
