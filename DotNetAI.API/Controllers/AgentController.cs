using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AgentController : ControllerBase
    {
        private readonly Kernel kernel;
        public AgentController(Kernel kernel)
        {
            this.kernel = kernel;
        }

        [HttpPost("run")]
        public async Task<IActionResult<Models.ChatModels.ChatResponse>> Run([FromBody] ChatRequest request)
        {
            try
            {
                // Define the agent — role + instruction to use tools proactively
                var agent = new ChatCompletionAgent
                {
                    Name = "DotNetProjectAgent",
                    Instructions = """
                You are a senior .NET project management assistant.
                You have access to tools for sprint data, bug counts,
                developer tasks and design pattern suggestions.
                Proactively call relevant tools before answering.
                Synthesise all tool results into a clear response.
                """,
                    Kernel = kernel,
                    Arguments = new KernelArguments(
                        new OpenAIPromptExecutionSettings
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        })
                };

                var thread = new AgentGroupChat();
                var userMsg = new Microsoft.SemanticKernel.ChatMessageContent(
                    Microsoft.SemanticKernel.ChatMessageContentRole.User,
                    request.Message);
                thread.AddChatMessage(userMsg);

                var sb = new System.Text.StringBuilder();
                await foreach (var msg in thread.InvokeAsync(agent))
                    if (msg.Role.ToString() == "Assistant")
                        sb.Append(msg.Content);

                return Ok(new Models.ChatModels.ChatResponse(sb.ToString(), "SK Agent"));

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
        }
    }
}
