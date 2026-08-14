using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Collections.Concurrent;
using static DotNetAI.API.Models.ConversationModels;

namespace DotNetAI.API.Services
{
    public class ConversationService
    {
        private readonly AzureOpenAIClient azureOpenAIClient;
        private readonly IConfiguration configuration;

        // One list per SessionId — singleton = survives across HTTP requests
        private readonly ConcurrentDictionary<string, List<OpenAI.Chat.ChatMessage>> conversationHistories = new();

        public ConversationService(AzureOpenAIClient azureOpenAIClient, IConfiguration configuration)
        {
            this.azureOpenAIClient = azureOpenAIClient;
            this.configuration = configuration;
        }

        public async Task<ConversationResponse> ChatAsync(string sessionId, string userMessage)
        {
            // Get or create the conversation history for the session
            var conversationHistory = conversationHistories.GetOrAdd(sessionId, _ => new List<OpenAI.Chat.ChatMessage>{
            new SystemChatMessage(
                 "You are a helpful .NET developer assistant. " +
                 "Remember everything said in this conversation.")
            });

            // Add the user's message to the conversation history
            conversationHistory.Add(userMessage);

            // Create a chat request with the conversation history
            var chatClient = azureOpenAIClient.GetChatClient(
                configuration["AzureOpenAI:DeploymentName"]!);
            var result = await chatClient.CompleteChatAsync(conversationHistory);
            var reply = result.Value.Content[0].Text;

            // Add AI reply to history for next turn
            conversationHistory.Add(new AssistantChatMessage(reply));

            return new ConversationResponse(
                sessionId, reply,
                TurnCount: (conversationHistory.Count - 1) / 2,
                Provider: "Azure OpenAI");
        }

        public bool clearConversationHistory(string sessionId)
        {
            return conversationHistories.TryRemove(sessionId, out _);
        }

        public ConversationSummary GetConversationSummary(string sessionId)
        {
            if (conversationHistories.TryGetValue(sessionId, out var conversationHistory))
            {
                int turnCount = (conversationHistory.Count - 1) / 2;
                string lastMessage = conversationHistory.OfType<UserChatMessage>().LastOrDefault()?.Content.FirstOrDefault()?.Text ?? string.Empty;
                return new ConversationSummary(sessionId, turnCount, lastMessage);
            }
            else
            {
                return new ConversationSummary(sessionId, 0, string.Empty);
            }
        }
    }
}
