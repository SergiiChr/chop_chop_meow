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
        live.Watch(() => world.Lanterns.FirebugStack(Item), bugs => {
            headingTitle.Text = world.Lanterns.Name(bugs.Count);
            count.Text = $"{bugs.Count}/{Lanterns.Capacity}";
            cell.AddThemeColorOverride(ItemSlot.IconNormal, bugs.Count > 0 ? Colors.White : Style.Ghost);
            string deposit = bugs.Count == Lanterns.Capacity ? "The lantern is full."
                : "Left click to add the held stack · Right click to add one";
            cell.TooltipText = bugs.Count > 0 ? $"{PageText.FirebugLife(bugs.AverageMinutes)}\n{deposit}" : deposit;
        });
        if (placed == null) AddButton("Put it down", () => { if (Item is string item) panel.Player.PlaceContainer(item); });
        else AddButton("Pick up", () => world.Lanterns.PickUp(placed));
        BagGrid(item => {
            if (item != Firebugs.Item) return;
            int amount = ItemMoves.Amount(world.S.Backpack, item);
            // Ctrl draws from several stacks, so it takes the oldest firebugs across those stacks, not the clicked stack first.
            if (Input.IsKeyPressed(Key.Ctrl)) panel.Hotbar.FromStack(-1, () => world.Lanterns.Refill(lantern, placed, amount));
            else world.Lanterns.Refill(lantern, placed, amount);
        }, $"{BagHint}\nShift: add a stack of firebugs · Ctrl in the backpack: add firebugs not on the hotbar");
    }

    private bool CanDeposit() => world.Lanterns.BugCount(Item) < Lanterns.Capacity && (placed == null || world.Reaches(placed));
}
