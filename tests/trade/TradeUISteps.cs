using Godot;

namespace ChopChopMeow;

/// <summary>Reads the trading post's page for its UI checks.</summary>
public partial class Game {
    /// <summary>What the bank shows under its wages: today's sale coins and their colour.</summary>
    private (string Text, Color Color) BankRevenue() => GetTree().Root.FindChild("bank_revenue", true, false) is Label line ? (line.Text, line.GetThemeColor("font_color")) : ("", default);
}
