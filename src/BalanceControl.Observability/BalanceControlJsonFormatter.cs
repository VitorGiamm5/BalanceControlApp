using System.Globalization;
using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;

namespace BalanceControl.Observability;

public sealed class BalanceControlJsonFormatter : ITextFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    public void Format(LogEvent logEvent, TextWriter output)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = logEvent.Timestamp.UtcDateTime,
            ["level"] = logEvent.Level.ToString(),
            ["message"] = logEvent.RenderMessage(CultureInfo.InvariantCulture)
        };

        foreach (var property in logEvent.Properties)
            payload[property.Key] = ConvertValue(property.Value);

        if (logEvent.Exception is not null)
        {
            payload["error.type"] = logEvent.Exception.GetType().FullName;
            payload["error.message"] = logEvent.Exception.Message;
            payload["exception"] = logEvent.Exception.ToString();
        }

        output.Write(JsonSerializer.Serialize(payload, SerializerOptions));
        output.WriteLine();
    }

    private static object? ConvertValue(LogEventPropertyValue value)
        => value switch
        {
            ScalarValue scalar => scalar.Value,
            SequenceValue sequence => sequence.Elements.Select(ConvertValue).ToArray(),
            StructureValue structure => structure.Properties.ToDictionary(
                property => property.Name,
                property => ConvertValue(property.Value)),
            DictionaryValue dictionary => dictionary.Elements.ToDictionary(
                pair => pair.Key.Value?.ToString() ?? string.Empty,
                pair => ConvertValue(pair.Value)),
            _ => value.ToString()
        };
}
