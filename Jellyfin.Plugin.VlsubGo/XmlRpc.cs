using System.Globalization;
using System.Xml.Linq;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// A minimal XML-RPC codec, ported from vlsub-go. The opensubtitles.org API uses
/// only strings, ints, doubles, booleans, structs and arrays.
/// </summary>
public static class XmlRpc
{
    public static string BuildRequest(string method, params object?[] parameters)
    {
        var paramsElement = new XElement("params",
            parameters.Select(p => new XElement("param", EncodeValue(p))));

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("methodCall", new XElement("methodName", method), paramsElement));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement EncodeValue(object? value)
    {
        var inner = value switch
        {
            null => new XElement("string", string.Empty),
            string s => new XElement("string", s),
            bool b => new XElement("boolean", b ? "1" : "0"),
            int i => new XElement("int", i.ToString(CultureInfo.InvariantCulture)),
            long l => new XElement("int", l.ToString(CultureInfo.InvariantCulture)),
            double d => new XElement("double", d.ToString("G", CultureInfo.InvariantCulture)),
            IReadOnlyDictionary<string, object?> map => new XElement("struct",
                // Sorted so the encoding is deterministic and testable.
                map.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                   .Select(kv => new XElement("member",
                       new XElement("name", kv.Key),
                       EncodeValue(kv.Value)))),
            System.Collections.IEnumerable seq => new XElement("array",
                new XElement("data", seq.Cast<object?>().Select(EncodeValue))),
            _ => throw new NotSupportedException($"cannot encode {value.GetType()}"),
        };

        return new XElement("value", inner);
    }

    /// <summary>
    /// Parses a methodResponse and returns its single value as a
    /// <see cref="Dictionary{TKey,TValue}"/>, <see cref="List{T}"/>, string,
    /// long, double or bool.
    /// </summary>
    /// <exception cref="InvalidOperationException">The response is a fault.</exception>
    public static object? ParseResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("empty XML-RPC response");

        var fault = root.Element("fault");
        if (fault is not null)
        {
            var detail = ParseValue(fault.Element("value"));
            throw new InvalidOperationException($"XML-RPC fault: {Describe(detail)}");
        }

        var value = root.Element("params")?.Element("param")?.Element("value");
        if (value is null)
        {
            throw new InvalidOperationException("XML-RPC response carried no value");
        }

        return ParseValue(value);
    }

    private static object? ParseValue(XElement? value)
    {
        if (value is null)
        {
            return null;
        }

        var typed = value.Elements().FirstOrDefault();
        if (typed is null)
        {
            // An untyped <value> is a string, per the specification.
            return value.Value;
        }

        switch (typed.Name.LocalName)
        {
            case "string":
            case "dateTime.iso8601":
            case "base64":
                return typed.Value;

            case "int":
            case "i4":
            case "i8":
                return long.TryParse(typed.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)
                    ? l
                    : 0L;

            case "double":
                return double.TryParse(typed.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                    ? d
                    : 0d;

            case "boolean":
                var raw = typed.Value.Trim();
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);

            case "array":
                var items = typed.Element("data")?.Elements("value") ?? Enumerable.Empty<XElement>();
                return items.Select(ParseValue).ToList();

            case "struct":
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var member in typed.Elements("member"))
                {
                    var name = member.Element("name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        map[name] = ParseValue(member.Element("value"));
                    }
                }

                return map;

            default:
                return typed.Value;
        }
    }

    private static string Describe(object? value) => value switch
    {
        IReadOnlyDictionary<string, object?> map =>
            string.Join(", ", map.Select(kv => $"{kv.Key}={kv.Value}")),
        _ => value?.ToString() ?? "null",
    };

    // The API returns most numbers as strings, so these accept either form.

    public static string GetString(IReadOnlyDictionary<string, object?> map, string key) =>
        map.TryGetValue(key, out var v) ? v switch
        {
            string s => s,
            long l => l.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => string.Empty,
        } : string.Empty;

    public static int GetInt(IReadOnlyDictionary<string, object?> map, string key) =>
        map.TryGetValue(key, out var v) ? v switch
        {
            long l => (int)l,
            double d => (int)d,
            string s => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0,
            _ => 0,
        } : 0;

    public static float GetFloat(IReadOnlyDictionary<string, object?> map, string key) =>
        map.TryGetValue(key, out var v) ? v switch
        {
            double d => (float)d,
            long l => l,
            string s => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
            _ => 0f,
        } : 0f;
}
