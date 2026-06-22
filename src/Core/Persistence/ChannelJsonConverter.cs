using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MidiToEverything.Core.Persistence;

/// <summary>
/// Reads a channel that may be a JSON number (1..16) or the string "any", and writes it
/// back in the natural form: a number for numeric channels, the string "any" otherwise
/// (docs/03_ProfileSchema.md §1). Applied per-property so it never affects other strings.
/// </summary>
internal sealed class ChannelJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.String => reader.GetString() ?? "any",
            _ => throw new JsonException("channel must be a number or a string"),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (!string.Equals(value, "any", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var channel))
        {
            writer.WriteNumberValue(channel);
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
