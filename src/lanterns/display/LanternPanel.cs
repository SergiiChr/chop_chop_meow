using Godot;

namespace ChopChopMeow;

/// <summary>A lantern opened from the backpack or the ground, with a slot for adding firebugs.</summary>
public partial class LanternPanel(PageTools tools, int lantern, Thing? placed = null) : PagePanel(tools) {
    private string? Item => placed?.LanternItem ?? world.Lanterns.Carried(lantern);
    public override bool Current => placed != null ? world.Find(placed.Id) == placed : Item is string item && world.Held?.Item != item;
    public override Page GivesWayTo => placed == null ? Page.Backpack : Page.None;
    protected override bool StoresFromHotbar => true;

    protected override void Build() {
        headingIcon.Texture = Icons.Of(Lanterns.Item); headingIcon.Visible = true;
        headingIcon.TooltipText = world.Data.Items[Lanterns.Item].Description;
        var fill = Row(); fill.AddThemeConstantOverride("separation", 12);
        var (cell, count) = ItemSlot.Create("lantern_firebugs");
        count.Name = "lantern_firebug_count";
        cell.Icon = Icons.Of(Firebugs.Item);
        cell.Controls = new SlotInput(cursor) {
            Takes = held => held.Stack is { Box: 0, Item: Firebugs.Item } && CanDeposit(),
            Put = (_, units) => kit.Run(() => world.FromHeldStack(() => world.Lanterns.Refill(lantern, placed, units))),
        };
        fill.AddChild(cell);
        var hint = Style.Wrapped("Put a held firebug stack here. Right-click to add one.", 15);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fill.AddChild(hint);
        live.Watch(() => world.Lanterns.BugCount(Item), held => {
            headingTitle.Text = Item is string item ? world.Describe(item) : world.Data.Name(Lanterns.Item);
            count.Text = $"{held}/{Lanterns.Capacity}";
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            cell.TooltipText = held >= Lanterns.Capacity ? "The lantern is full."
                : "Left click to add the held stack · Right click to add one";
        });
        if (placed == null) AddButton("Put it down", () => { if (Item is string item) panel.Player.PlaceContainer(item); });
        else AddButton("Pick up", () => world.Lanterns.PickUp(placed));
        BagGrid(item => {
            if (item == Firebugs.Item) world.Lanterns.Refill(lantern, placed, ItemMoves.Amount(world.S.Backpack, item));
        }, $"{BagHint}\nShift: add a stack of firebugs · Ctrl: add all firebugs");
    }

    private bool CanDeposit() => world.Lanterns.BugCount(Item) < Lanterns.Capacity && (placed == null || world.Reaches(placed));
}
