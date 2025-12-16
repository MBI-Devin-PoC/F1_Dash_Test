using System.Text.Json.Nodes;

namespace Live.Models;

public abstract record Message
{
    public record Initial(JsonNode Data) : Message;
    public record Updates(List<(string Topic, JsonNode Data)> Items) : Message;
}
