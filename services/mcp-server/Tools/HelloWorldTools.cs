using System.ComponentModel;
using ModelContextProtocol.Server;

namespace F1McpServer.Tools;

[McpServerToolType]
public static class HelloWorldTools
{
    [McpServerTool, Description("Returns a friendly hello world greeting message.")]
    public static string HelloWorld(
        [Description("Optional name to greet. If not provided, defaults to 'World'.")] string? name = null)
    {
        var greeting = string.IsNullOrWhiteSpace(name) ? "World" : name;
        return $"Hello, {greeting}!";
    }
}
