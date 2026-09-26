using Azure.AI.OpenAI;
using DotNetAI.API.Models;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Text.Json;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ToolController : ControllerBase
    {
        private readonly AzureOpenAIClient azureOpenAIClient;
        private readonly IConfiguration configuration;
        public ToolController(AzureOpenAIClient azureOpenAIClient, IConfiguration configuration)
        {
            this.azureOpenAIClient = azureOpenAIClient;
            this.configuration = configuration;
        }

        // ── TOOL DEFINITIONS (JSON schema — what SK built for you in EP08) ──
        private static readonly ChatTool GetWeatherTool =
            ChatTool.CreateFunctionTool(
                functionName: "GetWeather",
                functionDescription: "Gets the current weather for a given city",
                functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "city": {
                        "type": "string",
                        "description": "The city name, e.g. London"
                    }
                },
                "required": ["city"]
            }
            """));


        private static readonly ChatTool GetStockPriceTool =
        ChatTool.CreateFunctionTool(
            functionName: "GetStockPrice",
            functionDescription: "Gets the current stock price for a ticker",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "ticker": {
                        "type": "string",
                        "description": "Stock ticker, e.g. MSFT"
                    }
                },
                "required": ["ticker"]
            }
            """));

        // ── POST /api/tool/run ──────────────────────────────────────────────
        [HttpPost("Run")]
        public async Task<ActionResult<ChatModels.ChatResponse>> RunTool([FromBody] ChatRequest request)
        {
            var message = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage("You are helpful with Get Weather + Get StockPrice"),
                new UserChatMessage(request.Message)
            };

            var options = new ChatCompletionOptions
            {
                Tools = { GetWeatherTool, GetStockPriceTool },
            };
            var chatClient = azureOpenAIClient.GetChatClient(configuration["AzureOpenAI:DeploymentName"]);
            // ── THE TOOL-CALL LOOP ───────────────────────────────────────────
            while (true)
            {
                var response = chatClient.CompleteChatAsync(message, options);
                message.Add(new AssistantChatMessage(response.Result.Value));

                if (response.Result.Value.FinishReason == OpenAI.Chat.ChatFinishReason.Stop)
                    return Ok(new ChatModels.ChatResponse(
                        response.Result.Value.Content[0].Text,
                        "Azure OpenAI Tool Calling"));

                if (response.Result.Value.FinishReason == OpenAI.Chat.ChatFinishReason.ToolCalls)
                {
                    foreach (var toolCall in response.Result.Value.ToolCalls)
                        message.Add(new ToolChatMessage(
                            toolCall.Id, ExecuteTool(toolCall)));
                }
            }
        }

        // ── ROUTE CALLS TO YOUR FUNCTIONS ───────────────────────────────────
        private static string ExecuteTool(ChatToolCall toolCall)
        {
            using var args = JsonDocument.Parse(toolCall.FunctionArguments);
            return toolCall.FunctionName switch
            {
                "GetWeather" => GetWeather(
                    args.RootElement.GetProperty("city").GetString()!),
                "GetStockPrice" => GetStockPrice(
                    args.RootElement.GetProperty("ticker").GetString()!),
                _ => $"Tool '{toolCall.FunctionName}' not found."
            };
        }

        // ── FUNCTION IMPLEMENTATIONS ─────────────────────────────────────────
        // Real app: replace with database calls, API calls, file reads etc.
        private static string GetWeather(string city) =>
            $"{city}: 18°C, partly cloudy. Wind 12 km/h.";

        private static string GetStockPrice(string ticker) =>
            $"{ticker}: $342.50 (+1.2% today). Market cap: $2.54T.";

    }
}
