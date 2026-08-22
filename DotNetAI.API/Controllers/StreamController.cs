using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using System.Text.Json;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StreamController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly AzureOpenAIClient azureOpenAIClient;

        public StreamController(IConfiguration configuration, AzureOpenAIClient azureOpenAIClient)
        {
            _configuration = configuration;
            this.azureOpenAIClient = azureOpenAIClient;
        }

        [HttpPost("chat")]
        public async Task StreamChat([FromBody] ChatRequest chatrequest)
        {
            if (string.IsNullOrWhiteSpace(chatrequest.Message))
            {
                Response.StatusCode = 400;
                return;
            }

            // 1. Set SSE headers
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            // 2. Get chat client
            var chatClient = azureOpenAIClient.GetChatClient(_configuration["AzureOpenAI:DeploymentName"]!);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant."),
                new UserChatMessage(chatrequest.Message)
            };

            // 3. Stream tokens — KEY DIFFERENCE from CompleteChatAsync

            await foreach (var update in chatClient.CompleteChatStreamingAsync(messages))
            {
                foreach (var part in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        // json parse here
                        var json = JsonSerializer.Serialize(part.Text);
                        await Response.WriteAsync($"data: {json}\n\n");
                        // flush the token here
                        await Response.Body.FlushAsync();
                    }
                }
            }

            // 4. Signal done
            await Response.WriteAsync($"data: [Done]\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
