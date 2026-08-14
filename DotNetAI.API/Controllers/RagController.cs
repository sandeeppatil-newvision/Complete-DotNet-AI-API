using DotNetAI.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static DotNetAI.API.Models.RagModels;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RagController : ControllerBase
    {
        private readonly RagService ragService;

        public RagController(RagService ragService)
        {
            this.ragService = ragService;
        }

        /// <summary>Ingest doc → chunk → embed → store in Azure AI Search</summary>
        [HttpPost("Ingest")]
        public async Task<IActionResult> IngestDocument([FromBody] IngestRequest request)
        {
            try
            {
                int ingestedCount = await ragService.IngestDocumentAsync(request.DocumentName, request.Content);
                return Ok(new { Message = $"Successfully ingested {ingestedCount} chunks for document '{request.DocumentName}'." });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
        }

        /// <summary>Ask a question — AI uses ONLY your documents</summary>
        /// 
        [HttpPost ("AskQuestion")]
        public async Task<IActionResult> AskQuestion([FromBody] AskRequest request)
        {
            try
            {
                var (answer, sources) = await ragService.AskAsync(request.Question);
                return Ok(new AskResponse(answer, sources));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Error = ex.Message });
            }
        }

    }
}
