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

    public void Post(string message) {
        for (int i = 0; i < count; i++) if (texts[i] == message) { clocks[i] = ShowSeconds; return; }
        // A full stack makes room by dropping the message closest to fading, which a repeat may have kept from being the oldest.
        if (count == Lines) {
            int stalest = 0;
            for (int i = 1; i < count; i++) if (clocks[i] < clocks[stalest]) stalest = i;
            Remove(stalest);
        }
        texts[count] = message; clocks[count] = ShowSeconds; count++;
        Refresh();
    }

    public override void _Process(double delta) {
        for (int i = count - 1; i >= 0; i--) {
            clocks[i] -= (float)delta;
            if (clocks[i] <= 0) { Remove(i); Refresh(); } else labels[i].Modulate = Fade(i);
        }
    }

    private void Remove(int index) {
        for (int i = index; i < count - 1; i++) { texts[i] = texts[i + 1]; clocks[i] = clocks[i + 1]; }
        count--; texts[count] = "";
    }

    private Color Fade(int index) => new(1, 1, 1, Mathf.Min(1, clocks[index] / FadeSeconds));

    /// <summary>Puts the texts back on the labels after the order changed; only on new or expired messages.</summary>
    /// <remarks>Each label takes its new message's fade too, or it would show the next one with the faded alpha of the last for a frame.</remarks>
    private void Refresh() {
        for (int i = 0; i < Lines; i++) {
            labels[i].Visible = i < count;
            if (i < count) { labels[i].Text = texts[i]; labels[i].Modulate = Fade(i); }
        }
    }
}
