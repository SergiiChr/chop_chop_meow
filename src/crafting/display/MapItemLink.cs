using System;
using Godot;

namespace ChopChopMeow;

/// <summary>An item link that keeps its name and icon hidden until the settlement discovers it.</summary>
internal partial class MapItemLink(World world, string item, string hint = "", string? role = null) : ItemSlot {
    public const string QuestionMark = "?";
    public bool Known { get; private set; } = KnownTo(world, item);
    public Action<string, Texture2D?>? PresentIdentity { get; set; }
    public event Action? Discovered;

    /// <summary>Building plans are discovered by building them, and items by having them.</summary>
    public static bool KnownTo(World world, string item) => world.Data.IsBuilding(item) ? world.Reached(item)
        : world.Data.Items.ContainsKey(item) && world.Had(item);

    public override void _Ready() => ShowIdentity(KnownTo(world, item));

    public override void _Process(double delta) {
        if (!KnownTo(world, item)) return;
        ShowIdentity(true);
        Discovered?.Invoke();
    }

    private void ShowIdentity(bool known) {
        Known = known;
        SetProcess(!known);
        string name = known ? world.Data.NameOnMap(item) : QuestionMark;
        var icon = known ? Icons.Of(item) : null;
        TooltipText = role == null ? name : $"{name}\n{role}";
        if (hint != "") TooltipText += $"\n{hint}";
        if (known) TooltipText += (hint == "" ? "\n" : " · ") + RecipeMap.ItemTreeHint;
        if (PresentIdentity is { } present) {
            present(name, icon);
        } else {
            Text = known ? "" : QuestionMark;
            Icon = icon;
        }
    }
}
