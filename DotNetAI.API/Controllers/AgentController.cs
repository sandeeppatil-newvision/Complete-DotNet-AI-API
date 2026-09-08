#pragma warning disable SKEXP01
using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly Kernel _kernel;

    public AgentController(Kernel kernel)
    {
        this._kernel = kernel;
    }

    [HttpPost("run")]
    public async Task<ActionResult<ChatResponse>> RunDetails([FromBody] ChatRequest chatRequest)
    {
        try
        {
            var agent = new ChatCompletionAgent
            {
                Name = "DotNetProjectAgent",
                Instructions = """
                You are a senior .NET project management assistant.
                You have tools for sprint data, bug counts,
                developer tasks and design pattern suggestions.
                Proactively call relevant tools before answering.
                Synthesise all results into a clear response.
                """,
                Kernel = _kernel,
                Arguments = new KernelArguments(
                new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior =
                        FunctionChoiceBehavior.Auto()
                })
            };

            #pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var thread = new AgentGroupChat();
            #pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            // Fix 3 — AuthorRole.User not ChatMessageContentRole.User
            var userMsg = new Microsoft.SemanticKernel.ChatMessageContent(
                AuthorRole.User, chatRequest.Message);
            thread.AddChatMessage(userMsg);

            var sb = new System.Text.StringBuilder();
            await foreach (var msg in thread.InvokeAsync(agent))
                if (msg.Role == AuthorRole.Assistant)
                    sb.Append(msg.Content);

            return Ok(new ChatResponse(sb.ToString(), "SK Agent"));

        }
        catch (Exception ex)
        {
            return StatusCode(500,
           new ChatResponse($"Agent error: {ex.Message}", "SK Agent"));
        }
    }

}