using DotNetAI.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static DotNetAI.API.Models.ConversationModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly ConversationService conversationService;

        public ConversationController(ConversationService conversationService)
        {
            this.conversationService = conversationService;
        }

        /// <summary>Send a message — same SessionId = AI remembers</summary>
        /// 
        [HttpPost("Chat")]
        public async Task<IActionResult> Chat([FromBody] ConversationRequest request)
        {
            var response = await conversationService.ChatAsync(request.SessionId, request.Message);
            return Ok(response);
        }

        /// <summary>Get conversation summary for a session</summary>
        /// 
        [HttpGet("{sessionId}")]
        public async Task<IActionResult> GetConversationSummary(string sessionId)
        {
            var summary = conversationService.GetConversationSummary(sessionId);
            if (summary == null)
            {
                return NotFound();
            }
            return Ok(summary);
        }


        /// <summary>Stream a reply token by token — same SessionId = AI remembers</summary>
        [HttpPost("stream")]
        public async Task Stream([FromBody] ConversationRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = 400;
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            await foreach (var token in conversationService.ChatStreamAsync(request.SessionId, request.Message, ct))
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(token)}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        /// <summary>Start a new chat — forget this session</summary>
        [HttpDelete("{sessionId}")]
        public IActionResult Clear(string sessionId)
        {
            conversationService.clearConversationHistory(sessionId);
            return NoContent();
        }


    }
}
