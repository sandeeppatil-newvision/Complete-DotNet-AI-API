using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI;
using OpenAI.Chat;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly OpenAIClient _openAIClient;
        private readonly IConfiguration _config;

        public ChatController(AzureOpenAIClient azureClient, OpenAIClient openAIClient, IConfiguration config)
        {
            _azureClient = azureClient;
            _openAIClient = openAIClient;
            _config = config;
        }

        /// <summary>Send a message and get an AI response</summary>
        [HttpPost]
        public async Task<ActionResult<ChatResponse>> Post(
            [FromBody] ChatRequest request, [FromQuery] string provider = "azure")
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message cannot be empty.");
        

            (ChatClient chatClient, string providerName) = provider.ToLower() switch
            {
                "openai" => (_openAIClient.GetChatClient(
                                 _config["OpenAI:Model"] ?? "gpt-4o"),
                             "OpenAI Direct"),
                _ => (_azureClient.GetChatClient(
                                 _config["AzureOpenAI:DeploymentName"]!),
                             "Azure OpenAI")
            };

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
