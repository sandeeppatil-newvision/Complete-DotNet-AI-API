using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace DotNetAI.API.Services
{
    public class AgentService
    {
        private const int MaxSteps = 6;   // safety limit: an agent must never loop forever (or burn tokens)
        public record AgentMessage(string Role, string Content);
        // One small event type for everything React needs to show
        public record AgentEvent(string Type, string? Id = null, string? Name = null, string? Args = null, string? Text = null);


        private const string SystemPrompt = """
            You are a helpful assistant with tools.
            - For any question about the user's uploaded documents, call search_documents and answer ONLY from the results.
              Cite the file and page after each fact, like [Handbook.pdf, p. 4].
            - Call list_documents if you need to know which documents exist.
            - For ANY arithmetic, call calculate. Never do maths yourself.
            - You can call several tools one after another before answering.
            - If the search results do not contain the answer, reply exactly: "I couldn't find that in your documents."
            - If no tool is needed (greetings, general questions), just answer briefly.
            Keep answers short and clear.
            """;

        private readonly OpenAIClient _openAI;
        private readonly IConfiguration _config;
        private readonly AgentTools _tools;

        public AgentService(AzureOpenAIClient azureClient, IConfiguration config, AgentTools tools)
        {
            _openAI = azureClient;
            _config = config;
            _tools = tools;
        }

        public async IAsyncEnumerable<AgentEvent> RunAsync(
            IEnumerable<AgentMessage> history,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // 1. Build the conversation: system prompt + history (the last item is the new question)
            var messages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt) };
            foreach (var m in history)
            {
                if (m.Role == "assistant") messages.Add(new AssistantChatMessage(m.Content));
                else messages.Add(new UserChatMessage(m.Content));
            }

            // 2. Give the model the tool list
            var options = new ChatCompletionOptions();
            foreach (var tool in _tools.Definitions) options.Tools.Add(tool);
            var chat = _openAI.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            // 3. THE AGENT LOOP
            for (var step = 0; step < MaxSteps; step++)
            {
                var toolCalls = new ToolCallAccumulator();
                ChatFinishReason? finish = null;

                await foreach (var update in chat.CompleteChatStreamingAsync(messages, options, cancellationToken: ct))
                {
                    // Plain text → straight to the user
                    foreach (var part in update.ContentUpdate)
                        if (!string.IsNullOrEmpty(part.Text))
                            yield return new AgentEvent("token", Text: part.Text);

                    // Tool calls arrive in small pieces → collect them
                    foreach (var piece in update.ToolCallUpdates)
                        toolCalls.Append(piece);

                    if (update.FinishReason is { } reason) finish = reason;
                }

                // No tool requested → the text we just streamed WAS the final answer
                if (finish != ChatFinishReason.ToolCalls) yield break;

                // The model asked for tools: remember its request…
                var calls = toolCalls.Build();
                messages.Add(new AssistantChatMessage(calls));

                // …run each tool, show it in the UI, and hand the result back
                foreach (var call in calls)
                {
                    var argsJson = call.FunctionArguments.ToString();
                    yield return new AgentEvent("tool_call", call.Id, call.FunctionName, argsJson);

                    string result;
                    try
                    {
                        result = await _tools.ExecuteAsync(call.FunctionName, argsJson, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        result = $"Tool error: {ex.Message}";   // the model can read this and recover
                    }

                    messages.Add(new ToolChatMessage(call.Id, result));
                    yield return new AgentEvent("tool_result", call.Id, call.FunctionName, Text: Preview(result));
                }
                // → loop again: the model now sees the tool results
            }
            yield return new AgentEvent("token", Text: "\n\nI hit my step limit before finishing. Try a simpler question.");
        }
        private static string Preview(string s)
        {
            var oneLine = Regex.Replace(s, @"\s+", " ").Trim();
            return oneLine.Length > 160 ? oneLine[..160] + "…" : oneLine;
        }

        // Streaming sends each tool call in pieces (id + name first, then the JSON arguments in bits).
        // This glues the pieces back together, per tool-call index.
        private sealed class ToolCallAccumulator
        {
            private sealed class Part
            {
                public string Id = "";
                public string Name = "";
                public StringBuilder Args = new();
            }

            private readonly SortedDictionary<int, Part> _parts = new();

            public void Append(StreamingChatToolCallUpdate update)
            {
                if (!_parts.TryGetValue(update.Index, out var part))
                    _parts[update.Index] = part = new Part();

                if (!string.IsNullOrEmpty(update.ToolCallId)) part.Id = update.ToolCallId;
                if (!string.IsNullOrEmpty(update.FunctionName)) part.Name = update.FunctionName;
                if (update.FunctionArgumentsUpdate is not null)
                    part.Args.Append(update.FunctionArgumentsUpdate.ToString());
            }

            public List<ChatToolCall> Build() =>
                _parts.Values.Select(p => ChatToolCall.CreateFunctionToolCall(
                    p.Id,
                    p.Name,
                    BinaryData.FromString(p.Args.Length == 0 ? "{}" : p.Args.ToString()))).ToList();
        }



    }
}
