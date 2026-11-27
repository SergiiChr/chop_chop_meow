using System;

namespace ChopChopMeow;

/// <summary>What a find says when it turns up, and the diary days it holds.</summary>
/// <param name="Line">The pop-up's line.</param>
/// <param name="Days">The numbers of the days it holds, in order.</param>
public sealed record DiaryFindDef(DiaryFind Find, string Line, params int[] Days) {
    // The diary in a cave and the half burned page in a cave's ashes wait for the caves: nothing gives them out yet.
    private static readonly DiaryFindDef[] all = [
        new(DiaryFind.Book, "You find a diary. It's missing some pages.", 1, 12, 42, 45),
        new(DiaryFind.Lake, "A piece of paper floating in the water.", 2),
        new(DiaryFind.Sand, "You find a piece of paper in the sand.", 6),
        new(DiaryFind.Leaves, "You find a piece of paper in the leaves.", 10),
        new(DiaryFind.Berries, "You see a piece of paper stuck in the bush.", 40),
        new(DiaryFind.Tree, "A piece of paper fell off a tree.", 36, 37),
        new(DiaryFind.Ashes, "You find a half burned piece of paper in ashes.", 35),
    ];

    public static ReadOnlySpan<DiaryFindDef> All => all;

    /// <summary>A find's line and days; a loop rather than a search with a lambda, since an open diary asks on every refresh.</summary>
    public static DiaryFindDef Of(DiaryFind find) {
        foreach (var def in all) if (def.Find == find) return def;
        throw new ArgumentOutOfRangeException(nameof(find), find, "Not a find.");
    }
}
