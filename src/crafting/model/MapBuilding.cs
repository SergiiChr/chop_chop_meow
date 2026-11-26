using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>A building and the materials its construction takes, including the materials delivered after marking a mine.</summary>
internal readonly record struct MapBuilding(ThingKind Kind, IReadOnlyDictionary<string, int> Materials) {
    public string Id => Kind.Id();

    /// <summary>The largest cost determines where the building appears. A tie keeps the construction list's order.</summary>
    public string? MainMaterial => Materials.Count == 0 ? null : Materials.MaxBy(cost => cost.Value).Key;

    public static MapBuilding Of(GameData data, ThingKind kind) =>
        new(kind, kind == ThingKind.Mine ? data.Mine.Materials : data.Buildings[kind].Cost);

    public static IEnumerable<MapBuilding> All(GameData data) => data.Buildings.Keys.Select(kind => Of(data, kind));
}
