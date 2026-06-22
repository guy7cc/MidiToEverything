using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MidiToEverything.Core.Application;

namespace MidiToEverything.Core.Persistence;

/// <summary>
/// Serializes <see cref="AppConfig"/> to/from the schema JSON (docs/03_ProfileSchema.md),
/// applying enum naming, polymorphic actions, and version migration on read.
/// OS-independent, so it lives in Core and is fully unit-testable.
/// </summary>
public static class ConfigSerializer
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Keep Japanese labels and "+" literal so the config stays human-editable (FR-7.1).
        // Safe here: the file is local config, never embedded in HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(AppConfig config)
        => JsonSerializer.Serialize(ProfileMapper.ToDto(config), Options);

    public static AppConfig Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<ConfigDto>(json, Options)
                  ?? throw new InvalidDataException("Config JSON deserialized to null.");
        ConfigMigrator.Migrate(dto);
        return ProfileMapper.ToDomain(dto);
    }
}
