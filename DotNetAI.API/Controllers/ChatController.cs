using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly IConfiguration _config;

        public ChatController(AzureOpenAIClient azureClient, IConfiguration config)
        {
            _azureClient = azureClient;
            _config = config;
        }

        /// <summary>Send a message and get an AI response</summary>
        [HttpPost]
        public async Task<ActionResult<ChatResponse>> Post(
            [FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message cannot be empty.");

            var chatClient = _azureClient
                .GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful .NET developer assistant."),
            new UserChatMessage(request.Message)
        };

            var result = await chatClient.CompleteChatAsync(messages);
            return Ok(new ChatResponse(
                result.Value.Content[0].Text, "Azure OpenAI"));
        }
    }
}
