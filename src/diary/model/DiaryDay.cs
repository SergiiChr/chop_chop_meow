using System;

namespace ChopChopMeow;

/// <summary>One day the diary's writer wrote about: the day's number and its paragraphs.</summary>
/// <param name="Lines">
/// Paragraphs in order: <see cref="Illegible"/> inside one stands for a word nobody can read, <see cref="FallsApart"/> for where the writing starts to fall apart,
/// and a paragraph that is only <see cref="Scribbles"/> for a line of scribbles.
/// </param>
public sealed record DiaryDay(int Number, params string[] Lines) {
    /// <summary>Marks a word in a paragraph that is drawn as a scribble.</summary>
    public const string Illegible = "[illegible]";
    /// <summary>A paragraph drawn as a line of scribbles.</summary>
    public const string Scribbles = "[scribbles]";
    /// <summary>Marks where the writing in a paragraph starts to fall apart, as if written in the dark, getting worse to the paragraph's end.</summary>
    public const string FallsApart = "[falls apart]";

    private static readonly DiaryDay[] all = [
        new(1,
            "I finally escaped the grind. No more coding, no more office, no more commute. I'll just start a farm and live a peaceful self-sustaining life.",
            "Nature will provide. I saw a freshwater pond and it looks like the water is safe to drink.",
            "Maybe some solar panels to finally start my PC? I'll need to earn some money first I guess. I'll have to make do for now."),
        new(2,
            "Feeling great. Fresh air and quiet life. I found some berry bushes.",
            "Should I try selling them? Do people still buy fresh produce? I definitely need some money. Bringing an axe was definitely not good enough."),
        new(6,
            "I've met some dude hiking. He said he'd love to help me around the farm... for money of course. Yikes.",
            "I do need some help though, so I'll consider it."),
        new(10,
            "Berries are dirt cheap and I have to spend the whole fucking day looking for more to fill even a couple of crates. Maybe office was not that bad after all.",
            "Still can't afford any help so I'll have to improvise."),
        new(12, "Trader offers me less and less money the more berries I sell. What a scam."),
        new(35, "I FUCKING HATE BERRIES"),
        new(36,
            "Found a dead wolf. Not sure if it's a natural death or a disease but looked fresh. Fire kills all the bad things right?",
            "Risky, but if I have to eat another berry I'm going to kill myself."),
        new(37, "Diarrhea hits different when you don't have a toilet. Or a toilet paper."),
        new(40, "It's getting cold and I still did not manage to build anything that looks like a house. I keep hearing wolves and they keep getting closer every night. I have to do something. I saw some caves in the valley."),
        new(42, "Oh well. I got lost in the caves. I have some supplies (yeah, it's just berries) to last me a few days and wolves don't seem to bother me here, so it's not all bad. Fire keeps me warm."),
        new(45,
            "I guess that's it. I keep going in circles. One way is a dead end. I hear growling from the other tunnel so I'm not going there.",
            "My last log is almost done burning and I'm definitely not getting out in the dark. " + FallsApart + "I miss my " + Illegible,
            Scribbles),
    ];

    /// <summary>Every day of the diary, in the order of their numbers.</summary>
    public static ReadOnlySpan<DiaryDay> All => all;

    /// <summary>The day with a number, which the table has.</summary>
    public static DiaryDay Of(int number) => Array.Find(all, day => day.Number == number) ?? throw new ArgumentOutOfRangeException(nameof(number), number, "No such diary day.");
}
