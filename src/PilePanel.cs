using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A pile: fourteen stacks of the one resource it holds, filled left to right and top to bottom, and a switch that turns it into compost.
/// Compost shows the same grid; under its bar is what it is composting and how long that lasts, and the fertilizer it makes.
/// </summary>
public partial class PilePanel(SidePanel side, Thing t) : PagePanel(side) {
    public override void _Ready() {
        headingTitle.Text = t.Compost ? "Compost" : t.Inventory.First() is string held ? $"Pile · {world.Data.Name(held).ToLowerInvariant()}" : "Pile";
        if (t.Compost) { headingIcon.Texture = Icons.Of("leaves"); headingIcon.Visible = true; }
        var compost = new CheckButton { ButtonPressed = t.Compost, FocusMode = Control.FocusModeEnum.None, TooltipText = "Compost: turns leaves, grass and food into fertilizer", Name = "compost" };
        compost.Toggled += on => { if (!world.SetCompost(t, on)) compost.SetPressedNoSignal(t.Compost); else panel.Open("inspect"); };
        heading.AddChild(compost); heading.MoveChild(compost, heading.GetChildCount() - 2);
        if (t.Compost) FilterButton(t);
        else HeaderAction("stash", "Store everything this pile takes", () => StoreAll(t), Style.ActionRound, "deposit");
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 7, 44));
        if (!t.Compost) {
            Dynamic(text => text.Append(t.Inventory.UsedSlots).Append(" of ").Append(t.Inventory.Slots).Append(" stacks"), 14, Style.Beige);
        } else {
            var item = Style.Icon(Icons.Of("leaves"), 40); item.MouseFilter = Control.MouseFilterEnum.Pass;
            var lasts = Style.Text("", 13, Style.Tan);
            var making = Style.Icon(Icons.Of("fertilizer"), 52); making.TooltipText = "Makes fertilizer"; making.MouseFilter = Control.MouseFilterEnum.Pass;
            var main = new VBoxContainer(); main.AddThemeConstantOverride("separation", 10); main.AddChild(making); main.AddChild(PerBatch("×1"));
            float hour = world.Data.DaySeconds / 24;
            AddChild(new ProcessRow(live, Array.Empty<Control>(), "1 h", () => t.Progress / hour, new FuelRow("line_leaf", new Color("8fae7e"), item, lasts), main, Array.Empty<Control>()));
            var lastsText = new LiveText(shown => lasts.Text = shown);
            (string? Item, int Hours) shown = ("", -1);
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(lasts)) return;
                string? rotting = null; int units = 0;
                foreach (var pair in t.Inventory.Items) if (world.Compostable(pair.Key)) { rotting ??= pair.Key; units += pair.Value; }
                var now = (rotting, units / World.CompostBatch);
                if (now == shown) return;
                shown = now;
                item.Texture = Icons.Of(rotting ?? "leaves"); item.Modulate = rotting == null ? Style.Unready : Colors.White;
                item.TooltipText = rotting == null ? "Nothing to compost" : $"Composting {world.Data.Name(rotting).ToLowerInvariant()}";
                lastsText.Clear().Append('×').Append(World.CompostBatch).Append(" · ").Append(now.Item2).Append(" h"); lastsText.Show();
            });
            var skip = new CheckBox { Text = "Skip food delivery", ButtonPressed = t.SkipFood, FocusMode = Control.FocusModeEnum.None, Name = "skip_food", TooltipText = "Workers bring no food here; food you put in yourself still composts" };
            skip.Toggled += on => { world.SetSkipFood(t, on); skip.SetPressedNoSignal(t.SkipFood); };
            AddChild(skip);
            JobButtons(t);
        }
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move", wanted: id => world.Holds(t, id));
    }
}
