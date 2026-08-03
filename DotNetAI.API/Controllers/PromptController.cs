using Azure.AI.OpenAI;
using DotNetAI.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using static DotNetAI.API.Models.PromptModels;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromptController : ControllerBase
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly IConfiguration _config;

        public PromptController(AzureOpenAIClient azureClient, IConfiguration config)
        {
            _azureClient = azureClient;
            _config = config;
        }

        // Helper — gets the ChatClient
        private ChatClient GetChatClient()
        {
            return _azureClient.GetChatClient(
                _config["AzureOpenAI:DeploymentName"]!);
        }

        [HttpPost("review")]
        public async Task<ActionResult<CodeReviewResponse>> Review([FromBody] CodeReviewRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.code))
                return BadRequest("Code cannot be empty.");

            // Pattern 1 — Role + Constraints
            // ❌ Weak:  "You are a helpful assistant."
            // ✅ Strong: role + rules + exact format
            var systemPrompt = """
                                You are a senior .NET code reviewer with 10 years experience.
                                Rules you MUST follow:
                                - Never use "Great question!" or "Certainly!".
                                - Acceptable code → respond with exactly: APPROVED
                                - Each issue on a new line: ISSUE: [description]
                                - Each fix on a new line:   SUGGEST: [description]
                                - Max response: 200 words. Tone: direct, technical.
                                """;

            // Pattern 2 — Dynamic Context Injection at runtime
            var userContext = $"""
                                Review this {request.language} code.
                                Review date: {DateTime.UtcNow:yyyy-MM-dd}
                                (Adjust feedback depth to match developer level.)

                                Code:
                                {request.code}
                                """;

            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userContext)
            };

            var chatClient = GetChatClient();
            var result = await chatClient.CompleteChatAsync(messages);
            var responseText = result.Value.Content[0].Text;

            // Parse the response into CodeReviewResponse
            return Ok(new CodeReviewResponse(
                Verdict: "APPROVED",
                Issues: Array.Empty<string>(),
                Suggestions: new[] { responseText },
                ComplexityScore: 5,
                Summary: responseText));
        }

        /// <summary>Pattern 3: few-shot + Pattern 4: chain of thought</summary>
        [HttpPost("classify")]
        public async Task<ActionResult<CodeReviewResponse>> Classify([FromBody] BugReportRequest request)
        {
            // Pattern 3 — Few-Shot: examples teach format, not descriptions
            var prompt = $"""
                        Classify the severity of this bug report.
                        Respond with ONLY: LOW, MEDIUM, HIGH, or CRITICAL.

                        Examples:
                        Bug: Button text misaligned by 2px → LOW
                        Bug: Dashboard slow with 1000+ records → MEDIUM
                        Bug: Payment fails for Visa 4111 cards → HIGH
                        Bug: All users logged out, cannot log in → CRITICAL

                        // Pattern 4 — Chain of Thought before answering
                        Think through:
                        1. Does this affect security or data integrity?
                        2. How many users are impacted?
                        3. Is there a workaround?
                        Final answer on the last line only.

                        Bug: {request.description}
                        """;

            var chatClient = GetChatClient();
            var result = await chatClient.CompleteChatAsync(prompt);
            var responseText = result.Value.Content[0].Text;

            // Parse the response into CodeReviewResponse
            return Ok(new CodeReviewResponse(
                Verdict: "APPROVED",
                Issues: Array.Empty<string>(),
                Suggestions: new[] { responseText },
                ComplexityScore: 5,
                Summary: responseText));
        }

        /// <summary>Pattern 5: strict JSON — no markdown, no fences</summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<CodeReviewResponse>> Analyze([FromBody] CodeReviewRequest request)
        {
            // Describe the exact JSON schema in the prompt (plain raw string — no interpolation)
            var schemaPart = """
                            You are a .NET code reviewer. Analyse the following code.

                            Return a JSON object with EXACTLY these fields:
                            {  
                              "verdict": "APPROVED" | "NEEDS_WORK" | "REJECTED",
                              "issues": ["string", ...],
                              "suggestions": ["string", ...],
                              "complexityScore": 1-10 integer,
                              "summary": "one sentence max"
                            }

                            Rules:
                            - Return ONLY the JSON. No markdown. No preamble.
                            - issues and suggestions must be arrays, even if empty.
                            - complexityScore must be an integer, not a string.
                            """;

            // Append interpolated parts (language and code) to avoid brace/escape problems
            var prompt = schemaPart
                + "\n"
                + $"{request.language} code:\n"
                + request.code;

            // API-level constraint — model CANNOT return invalid JSON
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var result = await GetChatClient()
                .CompleteChatAsync([new UserChatMessage(prompt)], options);

            var review = JsonSerializer.Deserialize<CodeReviewResponse>(
                result.Value.Content[0].Text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return review is null
                ? StatusCode(500, "Failed to parse review result.")
                : Ok(review);
        }

    }
}