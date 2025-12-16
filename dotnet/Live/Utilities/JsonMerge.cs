using System.Text.Json.Nodes;

namespace Live.Utilities;

public static class JsonMerge
{
    public static void Merge(JsonNode? baseNode, JsonNode? update)
    {
        if (baseNode is null || update is null)
            return;

        if (baseNode is JsonObject baseObj && update is JsonObject updateObj)
        {
            foreach (var kvp in updateObj)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (baseObj.ContainsKey(key))
                {
                    var baseValue = baseObj[key];
                    if (baseValue is JsonObject && value is JsonObject)
                    {
                        Merge(baseValue, value);
                    }
                    else if (baseValue is JsonArray baseArr && value is JsonArray updateArr)
                    {
                        foreach (var item in updateArr)
                        {
                            baseArr.Add(item?.DeepClone());
                        }
                    }
                    else if (baseValue is JsonArray arr && value is JsonObject indexedUpdate)
                    {
                        foreach (var indexKvp in indexedUpdate)
                        {
                            if (int.TryParse(indexKvp.Key, out var index))
                            {
                                if (index < arr.Count)
                                {
                                    Merge(arr[index], indexKvp.Value);
                                }
                                else
                                {
                                    arr.Add(indexKvp.Value?.DeepClone());
                                }
                            }
                        }
                    }
                    else
                    {
                        baseObj[key] = value?.DeepClone();
                    }
                }
                else
                {
                    baseObj[key] = value?.DeepClone();
                }
            }
        }
    }

    public static void MergeInto(JsonObject target, string key, JsonNode? value)
    {
        if (value is null)
            return;

        if (target.ContainsKey(key))
        {
            var existing = target[key];
            if (existing is JsonObject && value is JsonObject)
            {
                Merge(existing, value);
            }
            else
            {
                target[key] = value.DeepClone();
            }
        }
        else
        {
            target[key] = value.DeepClone();
        }
    }
}
