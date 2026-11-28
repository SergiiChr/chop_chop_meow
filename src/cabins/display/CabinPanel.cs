using Godot;

namespace ChopChopMeow;

/// <summary>A cabin: inside or outside, the key of its door for a player inside, and what a night's sleep would leave of each need.</summary>
public partial class CabinPanel(PageTools tools, Thing subject) : SleepPage(tools, subject) {
    protected override void Body() {
        var tag = Style.Pill("", Style.TagPill, Style.Rim, 13);
        panel.AddToHeader(tag);
        var well = SleepStats();
        if (Barrel.In(thing.Inventory) != null) {
            var waterNote = Style.Text("Water is refilled from the roof barrel.", 14, Style.Ink);
            waterNote.HorizontalAlignment = HorizontalAlignment.Center;
            waterNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            well.AddChild(waterNote);
        }
        var buttons = Row(); buttons.AddThemeConstantOverride("separation", 12);
        var door = kit.BigButton("", Style.Sage, () => { if (world.Sheltered == thing.Id) world.Doorways.Leave(); else panel.Player.ToggleEntry(thing); });
        // The door's key sits under its button, for a player inside to turn.
        var doorSide = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; doorSide.AddThemeConstantOverride("separation", 8);
        var key = kit.Button("", () => world.Doorways.TurnKey()); key.Name = "key"; key.Flat = true; key.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        key.CustomMinimumSize = new Vector2(KeySide, KeySide);
        var keyhole = new TextureRect { Texture = panel.DoorKey.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        keyhole.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); key.AddChild(keyhole);
        doorSide.AddChild(door); doorSide.AddChild(key);
        buttons.AddChild(doorSide); buttons.AddChild(SleepButton());
        panel.DoorKey.Rest(thing.Locked);
        live.Watch(() => thing.Locked, locked => { panel.DoorKey.Turn(locked); key.TooltipText = locked ? "Unlock the door" : "Lock the door · keeps wolves out"; });
        live.Watch(() => panel.Player.Entering(thing), entering => {
            Style.SkinPill(door, entering ? Style.Mustard : Style.Sage);
            door.TooltipText = entering ? "Cancel walk to the cabin" : "";
        });
        live.Watch(() => (world.Sheltered == thing.Id, world.Sleep.SleepingAt == thing.Id), now => {
            var (inside, sleeping) = now;
            tag.Text = sleeping ? "asleep" : inside ? "inside" : "outside";
            tag.AddThemeStyleboxOverride("normal", inside ? Style.TagPill : Style.BlueTag);
            door.Text = inside ? "Step outside" : "Enter";
            door.Disabled = sleeping;
            key.Visible = inside && !sleeping;
        });
        RoofBarrelSlot();
        StoreBagGrid(Barrel.Is);
        JobButtons();
        RedrawOnChange(() => Barrel.In(thing.Inventory) != null);

    }

    /// <summary>The roof barrel's slot and fill, in the Tools window beside the cabin page.</summary>
    private void RoofBarrelSlot() {
        var window = panel.ShowTools(190);
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
}
