using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Anything that walks the map on its own: it steps straight at where it is going, or follows a route around what blocks it.</summary>
public interface IWalker {
    Vector2 Pos { get; set; }
    List<Vector2> Route { get; }
    Vector2 RouteTarget { get; set; }
    /// <summary>When a walker that found no way to its destination looks again.</summary>
    float RouteRetry { get; set; }
}
