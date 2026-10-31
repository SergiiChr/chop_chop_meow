using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A pile: fourteen stacks of the one resource it holds, filled left to right and top to bottom, and a switch that turns it into compost.
/// Compost shows the same grid; under its bar is what it is composting and how long that lasts, and the fertilizer it makes.
/// </summary>
public partial class PilePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        headingTitle.Text = thing.Compost ? "Compost" : thing.Inventory.First() is string held ? $"Pile · {world.Data.Name(held).ToLowerInvariant()}" : "Pile";
        if (thing.Compost) { headingIcon.Texture = Icons.Of("leaves"); headingIcon.Visible = true; }
        var compost = new CheckButton { ButtonPressed = thing.Compost, FocusMode = Control.FocusModeEnum.None, TooltipText = "Compost: turns leaves, grass and food into fertilizer", Name = "compost" };
        compost.Toggled += on => { if (!world.SetCompost(thing, on)) compost.SetPressedNoSignal(thing.Compost); else Redraw(); };
        panel.AddToHeader(compost);
        if (thing.Compost) FilterButton();
        else HeaderAction("stash", "Store everything this pile takes", () => StoreAll(thing), Style.ActionRound, "deposit");
        Well(new VBoxContainer()).AddChild(StorageGrid(7, 44));
        if (!thing.Compost) {
            Dynamic(text => text.Append(thing.Inventory.UsedSlots).Append(" of ").Append(thing.Inventory.Slots).Append(" stacks"), 14, Style.Beige);
        } else {
            var item = Style.Icon(Icons.Of("leaves"), 40); item.MouseFilter = Control.MouseFilterEnum.Pass;
            var lasts = Style.Text("", 13, Style.Tan);
            var making = Style.Icon(Icons.Of("fertilizer"), 52); making.TooltipText = "Makes fertilizer"; making.MouseFilter = Control.MouseFilterEnum.Pass;
            var main = new VBoxContainer(); main.AddThemeConstantOverride("separation", 10); main.AddChild(making); main.AddChild(Style.PerBatch("×1"));
            float hour = world.Data.DaySeconds / 24;
            AddChild(new ProcessRow(live, Array.Empty<Control>(), "1 h", () => thing.Progress / hour, new FuelRow("line_leaf", new Color("8fae7e"), item, lasts), main, Array.Empty<Control>()));
            live.Watch(() => {
                string? rotting = null; int units = 0;
                foreach (var pair in thing.Inventory.Items) if (world.Compostable(pair.Key)) { rotting ??= pair.Key; units += pair.Value; }
                return (rotting, units / World.CompostBatch);
            }, now => {
                var (rotting, hours) = now;
                item.Texture = Icons.Of(rotting ?? "leaves"); item.Modulate = rotting == null ? Style.Unready : Colors.White;
                item.TooltipText = rotting == null ? "Nothing to compost" : $"Composting {world.Data.Name(rotting).ToLowerInvariant()}";
                lasts.Text = $"×{World.CompostBatch} · {hours} h";
            });
            var skip = new CheckBox { Text = "Skip food delivery", ButtonPressed = thing.SkipFood, FocusMode = Control.FocusModeEnum.None, Name = "skip_food", TooltipText = "Workers bring no food here; food you put in yourself still composts" };
            skip.Toggled += on => { world.SetSkipFood(thing, on); skip.SetPressedNoSignal(thing.SkipFood); };
            AddChild(skip);
            JobButtons();
        }
        StoreBagGrid("Click: store one · Ctrl: store all · Drag to move", id => world.Holds(thing, id));
    }
}
