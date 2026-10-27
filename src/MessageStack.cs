using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Notifications stacked under the top of the screen, oldest first, each fading out after a few seconds.
/// A repeat of a message still showing restarts its time instead of adding another line.
/// </summary>
public partial class MessageStack : VBoxContainer {
    private const int Lines = 4;
    private const float ShowSeconds = 7, FadeSeconds = 1;
    private readonly Label[] labels = new Label[Lines];
    private readonly string[] texts = new string[Lines];
    private readonly float[] clocks = new float[Lines];
    private int count;

    /// <summary>Messages showing, oldest first.</summary>
    public ReadOnlySpan<string> Shown => texts.AsSpan(0, count);

    /// <summary>Makes the line labels; <paramref name="make"/> styles each one like the rest of the HUD text.</summary>
    public MessageStack(Func<Label> make) {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);
        for (int i = 0; i < Lines; i++) {
            var label = make(); label.Name = $"message_{i}"; label.Visible = false; label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.MouseFilter = MouseFilterEnum.Ignore;
            labels[i] = label; AddChild(label);
        }
    }

    public void Show(string message) {
        for (int i = 0; i < count; i++) if (texts[i] == message) { clocks[i] = ShowSeconds; return; }
        if (count == Lines) Remove(0);
        texts[count] = message; clocks[count] = ShowSeconds; count++;
        Refresh();
    }

    public override void _Process(double delta) {
        for (int i = count - 1; i >= 0; i--) {
            clocks[i] -= (float)delta;
            if (clocks[i] <= 0) { Remove(i); Refresh(); } else labels[i].Modulate = new Color(1, 1, 1, Mathf.Min(1, clocks[i] / FadeSeconds));
        }
    }

    private void Remove(int index) {
        for (int i = index; i < count - 1; i++) { texts[i] = texts[i + 1]; clocks[i] = clocks[i + 1]; }
        count--; texts[count] = "";
    }

    /// <summary>Puts the texts back on the labels after the order changed; only on new or expired messages.</summary>
    private void Refresh() {
        for (int i = 0; i < Lines; i++) {
            labels[i].Visible = i < count;
            if (i < count) labels[i].Text = texts[i];
        }
    }
}
