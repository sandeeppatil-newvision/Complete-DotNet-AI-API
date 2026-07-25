using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static DotNetAI.API.Models.ChatModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KernelController : ControllerBase
    {
        private readonly Kernel _kernel;
        public KernelController(Kernel kernel)
        {
            _kernel = kernel;
        }

        [HttpPost("ask")]
        public async Task<ActionResult<ChatResponse>> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message cannot be empty.");

            var settings = new OpenAIPromptExecutionSettings
            {
                // Auto = AI decides whether to call plugins or answer directly
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var result = await _kernel.InvokePromptAsync(request.Message, new KernelArguments(settings));

            return Ok(new ChatResponse(result.ToString(), "Semantic Kernel"));
        }

    }
}
