using System.Text;
using System.Text.Json.Nodes;

namespace Live.Utilities;

public static class JsonTransformer
{
    public static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder();
        var capitalizeNext = false;
        var isFirst = true;

        foreach (var c in input)
        {
            if (c == '_' || c == '-' || c == ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (isFirst)
            {
                result.Append(char.ToLowerInvariant(c));
                isFirst = false;
            }
            else if (capitalizeNext)
            {
                result.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }

    public static void Transform(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var keysToProcess = obj.Select(kvp => kvp.Key).ToList();
            var newEntries = new List<(string key, JsonNode? value)>();

            foreach (var key in keysToProcess)
            {
                if (key == "_kf")
                {
                    obj.Remove(key);
                    continue;
                }

                var value = obj[key];
                Transform(value);

                var camelKey = ToCamelCase(key);
                if (camelKey != key)
                {
                    obj.Remove(key);
                    newEntries.Add((camelKey, value));
                }
            }

            foreach (var (key, value) in newEntries)
            {
                obj[key] = value;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                Transform(item);
            }
        }
    }
}
