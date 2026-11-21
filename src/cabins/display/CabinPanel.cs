using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A cabin: inside or outside, the key of its door for a player inside, and what a night's sleep would leave of each need.</summary>
public partial class CabinPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var tag = Style.Pill("", Style.TagPill, Style.Rim, 13);
        panel.AddToHeader(tag);
        // What a night's sleep would leave of each need at dawn; the corner plate shows them now.
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 12);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); well.AddChild(top);
        top.AddChild(Style.Glyph("line_moon", 20, Style.Ink));
        var until = Style.Text("", 14, Style.Ink); until.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(until);
        var block = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 13); top.AddChild(block);
        var after = Style.Text("Stats after sleep", 14, Style.Ink); after.HorizontalAlignment = HorizontalAlignment.Center; well.AddChild(after);
        var vials = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vials.AddThemeConstantOverride("separation", 36); well.AddChild(vials);
        var dawn = NeedColumn.All();
        foreach (var need in dawn) vials.AddChild(need);
        var buttons = Row(); buttons.AddThemeConstantOverride("separation", 12);
        var door = kit.BigButton("", Style.Sage, () => { if (world.Sheltered == thing.Id) world.Doorways.Leave(); else panel.Player.WalkInto(thing); });
        var sleep = kit.BigButton("Sleep", Style.Enamel, () => world.Sleep(thing)); sleep.Name = "sleep"; sleep.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        // The door's key sits under its button, for a player inside to turn.
        var doorSide = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; doorSide.AddThemeConstantOverride("separation", 8);
        var key = kit.Button("", () => world.Doorways.TurnKey()); key.Name = "key"; key.Flat = true; key.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        key.CustomMinimumSize = new Vector2(KeySide, KeySide);
        var keyhole = new TextureRect { Texture = panel.DoorKey.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        keyhole.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); key.AddChild(keyhole);
        doorSide.AddChild(door); doorSide.AddChild(key);
        buttons.AddChild(doorSide); buttons.AddChild(sleep);
        panel.DoorKey.Rest(thing.Locked);
        live.Watch(() => thing.Locked, locked => { panel.DoorKey.Turn(locked); key.TooltipText = locked ? "Unlock the door" : "Lock the door · keeps wolves out"; });
        live.Watch(() => (world.Sheltered == thing.Id, world.SleepBlock(thing), world.Data.GameMinutes(world.SecondsToDawn), Barrel.In(thing.Inventory)), now => {
            var (inside, blocked, _, _) = now;
            tag.Text = inside ? "inside" : "outside";
            tag.AddThemeStyleboxOverride("normal", inside ? Style.TagPill : Style.BlueTag);
            door.Text = inside ? "Step outside" : "Enter";
            key.Visible = inside;
            sleep.TooltipText = blocked ?? "Sleep until dawn. Production, hunger, trade and wages advance.";
            sleep.Modulate = blocked == null ? Colors.White : Style.Unready;
            block.Visible = blocked != null; block.Text = blocked == null ? "" : ShortBlock(blocked);
            until.Text = LiveText.Of(text => PageText.AppendGameHours(world, text.Append("until dawn · +"), world.SecondsToDawn));
            float seconds = world.SecondsToDawn;
            Show(0, "Health", world.HealthAtDawn(thing));
            Show(1, "Hunger", world.Hunger - seconds * world.Data.HungerPerSecond);
            Show(2, "Thirst", world.ThirstAtDawn(thing));
        });
        BarrelWindow();
        StoreBagGrid(Barrel.Is);
        JobButtons();
        RedrawOnChange(() => world.Jobs(thing).Length);

        void Show(int i, string need, float after) {
            var vial = dawn[i].Vial;
            vial.Level = Math.Max(0, after); vial.Warn = after <= 25; vial.TooltipText = $"{need} at dawn {vial.Level:0}%";
            vial.QueueRedraw();
        }
    }

    /// <summary>The roof barrel's slot and fill, beside the cabin page.</summary>
    private void BarrelWindow() {
        var window = panel.ShowAside(190);
        window.AddChild(Style.Heading("Roof barrel", 19));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        window.AddChild(Style.InWell(row));
        var grid = StorageGrid(1);
        var slot = grid.GetChild<ItemSlot>(0);
        slot.Name = "cabin_barrel";
        slot.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(grid);
        var label = Style.Text("", 13, Style.Ink, wrap: true);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(label);
        live.Watch(() => Barrel.In(thing.Inventory), item => {
            label.Text = item == null ? "Empty" : world.Data.Name(Barrel.Item);
            slot.AddThemeColorOverride(ItemSlot.IconNormal, item == null ? Style.Ghost : Colors.White);
            if (item != null) return;
            slot.Icon = Icons.Of(Barrel.Item);
            slot.TooltipText = "Empty barrel slot\nPut a steel barrel here to mount it on the roof";
        });
    }

    /// <summary>Pixels along each side of the door's key.</summary>
    private const float KeySide = 120;
    /// <summary>A sleep refusal cut to a tag: what stops it, in a word or two.</summary>
    private static string ShortBlock(string reason) => reason.Contains("thirsty") && reason.Contains("hungry") ? "too hungry and thirsty" : reason.Contains("thirsty") ? "too thirsty" : reason.Contains("hungry") ? "too hungry" : reason.Contains("night") ? "only at night" : "not yet";
}
