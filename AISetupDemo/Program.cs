using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

Console.WriteLine("Hello, World!");


// Load config from appsettings.json
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var endpoint = config["AzureOpenAI:Endpoint"]!;
var apiKey = config["AzureOpenAI:ApiKey"]!;
var modelName = config["AzureOpenAI:DeploymentName"]!;

// Create Azure OpenAI client
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey));

var chatClient = client.GetChatClient(modelName);

// System prompt — sets the AI's personality
var messages = new List<ChatMessage>
{
    new SystemChatMessage("You are a helpful .NET developer assistant. Keep answers concise."),
};

Console.WriteLine("AI Assistant ready! Type your question (or 'exit' to quit)\n");

while (true)
{
    Console.Write("You: ");
    var userInput = Console.ReadLine();

    if (string.IsNullOrEmpty(userInput) || userInput == "exit") break;

    messages.Add(new UserChatMessage(userInput));

    Console.Write("AI: ");
    var result = await chatClient.CompleteChatAsync(messages);
    var response = result.Value.Content[0].Text;

    Console.WriteLine(response);
    Console.WriteLine();

    // Add response to history → enables multi-turn conversation
    messages.Add(new AssistantChatMessage(response));
}