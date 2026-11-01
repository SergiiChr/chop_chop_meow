using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChopChopMeow;

/// <summary>
/// Writers generated at build time for the entries of world updates; unlike the reflection serializer, they allocate nothing for nested objects.
/// They leave out every field that holds its type's default, and write an inventory as fresh as a new one as {}, so a tree's entry is a few names and numbers.
/// A guest reads them back with <see cref="GameData.Json"/>, where a missing field keeps what a new object starts with.
/// So a field whose new object starts with anything but its type's default must always be written: it carries <c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c>.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault, Converters = [typeof(FreshInventory)])]
[JsonSerializable(typeof(Thing))]
[JsonSerializable(typeof(Worker))]
[JsonSerializable(typeof(Wolf))]
[JsonSerializable(typeof(TradeOrder))]
[JsonSerializable(typeof(PlayerState))]
internal partial class UpdateJson : JsonSerializerContext;

/// <summary>Every inventory that is not fresh, written as <see cref="UpdateJson"/> writes the rest.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(Inventory))]
internal partial class InventoryJson : JsonSerializerContext;

/// <summary>Writes an inventory as fresh as a new one as {}, which reads back as a new one; every tree, rock and bush carries one.</summary>
internal sealed class FreshInventory : JsonConverter<Inventory> {
    public override Inventory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("Guests read updates with GameData.Json.");

    public override void Write(Utf8JsonWriter writer, Inventory value, JsonSerializerOptions options) {
        if (!value.Fresh) { JsonSerializer.Serialize(writer, value, InventoryJson.Default.Inventory); return; }
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}
