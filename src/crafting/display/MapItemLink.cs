using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// An item link that keeps its name and icon hidden until the settlement discovers it, and greys them until someone
/// makes it.
/// </summary>
internal partial class MapItemLink(World world, string item, string hint = "", string? role = null) : ItemSlot {
    public const string QuestionMark = "?";
    // Every look a chip's icon takes, so a chip not made yet stays grey while hovered or pressed.
    private static readonly StringName[] IconColors = [
        "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color",
    ];
    public MapState State { get; private set; } = world.MapDiscovery.Of(item);
    public bool Shown => State != MapState.Undiscovered;
    public Action<string, Texture2D?>? PresentIdentity { get; set; }
    /// <summary>Raised once, when the name and icon first show.</summary>
    public event Action? Discovered;
    /// <summary>Raised whenever the state changes.</summary>
    public event Action? Changed;

    public override void _Ready() => ShowIdentity();

    public override void _Process(double delta) {
        var state = world.MapDiscovery.Of(item);
        if (state == State) return;
        bool discovered = !Shown;
        State = state;
        ShowIdentity();
        if (discovered) Discovered?.Invoke();
        Changed?.Invoke();
    }

    private void ShowIdentity() {
        SetProcess(State != MapState.Made);
        string name = Shown ? world.Data.NameOnMap(item) : QuestionMark;
        var icon = Shown ? Icons.Of(item) : null;
        TooltipText = role == null ? name : $"{name}\n{role}";
        if (hint != "") TooltipText += $"\n{hint}";
        if (Shown) TooltipText += (hint == "" ? "\n" : " · ") + RecipeMap.ItemTreeHint;
        if (PresentIdentity is { } present) {
            present(name, icon);
        } else {
            Text = Shown ? "" : QuestionMark;
            Icon = icon;
            var tint = State == MapState.NotMade ? MapSlot.NotMadeTint : Colors.White;
            foreach (var look in IconColors) AddThemeColorOverride(look, tint);
        }
    }
}
