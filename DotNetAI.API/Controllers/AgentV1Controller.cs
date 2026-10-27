using DotNetAI.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using static DotNetAI.API.Services.AgentService;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AgentV1Controller : ControllerBase
    {
        private readonly AgentService _agent;
        public record AgentRequest(List<AgentMessage> Messages);

        // camelCase, and leave out null fields so each event stays small
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        public AgentV1Controller(AgentService agent) => _agent = agent;

        /// <summary>Run the agent — streams tool calls, tool results and the final answer (SSE)</summary>
        [HttpPost("stream")]
        public async Task Stream([FromBody] AgentRequest request, CancellationToken ct)
        {
            if (request.Messages is null || request.Messages.Count == 0)
            {
                Response.StatusCode = 400;
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            await foreach (var evt in _agent.RunAsync(request.Messages, ct))
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(evt, Json)}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
