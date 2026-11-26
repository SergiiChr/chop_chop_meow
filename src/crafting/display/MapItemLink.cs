using System;
using Godot;

namespace ChopChopMeow;

/// <summary>An item link that keeps its name and icon hidden until the settlement discovers it.</summary>
internal partial class MapItemLink(World world, string item, string hint, string? role = null) : ItemSlot {
    public const string QuestionMark = "?";
    public bool Known { get; private set; } = world.Had(item);
    public Action<string, Texture2D?>? PresentIdentity { get; set; }
    public event Action? Discovered;

    public override void _Ready() => ShowIdentity(world.Had(item));

    public override void _Process(double delta) {
        if (!world.Had(item)) return;
        ShowIdentity(true);
        Discovered?.Invoke();
    }

    private void ShowIdentity(bool known) {
        Known = known;
        SetProcess(!known);
        string name = known ? world.Data.Name(item) : QuestionMark;
        var icon = known ? Icons.Of(item) : null;
        TooltipText = role == null ? $"{name}\n{hint}" : $"{name}\n{role}\n{hint}";
        if (PresentIdentity is { } present) {
            present(name, icon);
        } else {
            Text = known ? "" : QuestionMark;
            Icon = icon;
        }
    }
}
