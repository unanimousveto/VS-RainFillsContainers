using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace RainFillsContainers.Api;

public class BEBRainAcceptingOffsetBlock : BEBRainAcceptingBlock
{
    private BlockPos offset = new(Dimensions.WillSetLater);

    public int XOffset { get => offset.X; set => offset.X = value; }
    public int YOffset { get => offset.Y; set => offset.Y = value; }
    public int ZOffset { get => offset.Z; set => offset.Z = value; }

    public BEBRainAcceptingOffsetBlock(BlockEntity blockentity) : base(blockentity) {}

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        XOffset = properties["xOffset"].AsInt();
        YOffset = properties["yOffset"].AsInt();
        ZOffset = properties["zOffset"].AsInt();
    }

    public override float GetEffectivePrecipitation()
    {
        if (!IsExposed(Blockentity.Pos + offset)) return 0.0f;
        return GetPrecipitationAt(Blockentity.Pos);
    }
}
