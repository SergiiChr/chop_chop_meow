namespace ChopChopMeow;

/// <summary>What one of a station's tool slots takes: a tool of <see cref="Tool"/>'s kind at <see cref="Tier"/> or better.</summary>
public readonly record struct ToolSlotRule(string Tool, int Tier = 0) {
    public bool Takes(ItemDef tool) => tool.Tool == Tool && tool.Tier >= Tier;
}
