using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RainFillsContainers;

public class BEBehaviorRainFillable : BlockEntityBehavior {
    /// <summary>Number of milliseconds between fill checks.</summary>
    const int FILL_DELTA_MS = 10000;

    private long fillListener;

    public BEBehaviorRainFillable(BlockEntity blockentity) : base(blockentity) {}

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);
        
        if (api.Side == EnumAppSide.Server) {
            this.fillListener = api.Event.RegisterGameTickListener(Update, FILL_DELTA_MS);

            this.Api.World.Logger.Debug(
                "Block at {0} started waiting for rain",
                [this.Blockentity.Pos]
            );
        }
    }

    private void Update(float deltaTime) {
        this.FillContainer();
    }

    protected void FillContainer() {
        this.Api.World.Logger.Debug(
            "Filling container at {0}",
            [this.Blockentity.Pos]
        );
    }

    public override void OnBlockRemoved() {
        this.Api.Event.UnregisterGameTickListener(this.fillListener);

        this.Api.World.Logger.Debug(
            "Block at {0} stopped waiting for rain",
            [this.Blockentity.Pos]
        );

        base.OnBlockRemoved();
    }
}
