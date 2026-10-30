using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>
/// Writers generated at build time for the entries of world updates; unlike the reflection serializer, they allocate nothing for nested objects.
/// They write the same JSON as <see cref="GameData.Json"/> without indents, which a guest reads back with it.
/// </summary>
[JsonSerializable(typeof(Thing))]
[JsonSerializable(typeof(Worker))]
[JsonSerializable(typeof(Wolf))]
[JsonSerializable(typeof(TradeOrder))]
[JsonSerializable(typeof(PlayerState))]
internal partial class UpdateJson : JsonSerializerContext;
