using DotNetAI.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    }
}
