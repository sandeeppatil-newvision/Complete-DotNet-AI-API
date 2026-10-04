using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DotNetAI.MCP.Tools
{

    [McpServerToolType]
    public   class WeatherTools
    {
        [McpServerTool(Name = "get_weather_mcp_aiwithsandeep")]
        [Description("Gets the current weather for a given city.")]
        public static string GetWeather(
            [Description("The city name, e.g. London")] string city)
        {
            // Real app: call a weather API
            return $"{city}: 18°C, partly cloudy (tested By sandeep). Wind 12 km/h.";
        }
    }
}
