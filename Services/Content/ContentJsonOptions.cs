using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChastiMaid.Services.Content;

public static class ContentJsonOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
