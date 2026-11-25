namespace ChopChopMeow;

/// <summary>
/// What a station's tool slot takes: a named <see cref="Item"/>, or a tool of its kind at its tier or better.
/// </summary>
public readonly record struct ToolSlotRule(string Tool, int Tier = 0, string? Item = null) {
    public bool Matches(string id, ItemDef tool) => Item is string item ? item == id : tool.Tool == Tool;
    public bool Takes(string id, ItemDef tool) => Matches(id, tool) && (Item != null || tool.Tier >= Tier);
}
