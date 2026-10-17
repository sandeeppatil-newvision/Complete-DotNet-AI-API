using DotNetAI.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        public record AskDocumentRequest(string Question);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly DocumentService documentService;
        public DocumentsController(DocumentService documentService)
        {
            this.documentService = documentService;
        }

        /// <summary>Upload a PDF — extract, chunk, embed and store it</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20 * 1024 * 1024)]   // 20 MB
        public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest("Please upload a PDF file.");
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only PDF files are supported.");

            // PdfPig needs a seekable stream, so copy the upload into memory
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            try
            {
                var info = await documentService.IngestPdfAsync(file.FileName, ms, ct);
                return Ok(info);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>List uploaded documents</summary>
        [HttpGet]
        public IActionResult List() => Ok(documentService.GetDocuments());


        /// <summary>Delete a document and its vectors</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id) =>
            await documentService.DeleteAsync(id) ? NoContent() : NotFound();

        /// <summary>Ask a question about one document — streamed answer with page citations</summary>
        [HttpPost("{id}/ask/stream")]
        public async Task AskStream(string id, [FromBody] AskDocumentRequest request, CancellationToken ct)
        {
            if (documentService.GetDocument(id) is null) { Response.StatusCode = 404; return; }
            if (string.IsNullOrWhiteSpace(request.Question)) { Response.StatusCode = 400; return; }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            // 1. Find the best chunks inside this PDF
            var sources = await documentService.SearchAsync(id, request.Question, ct: ct);

            // 2. Send the sources FIRST, so React can show the page chips
            var sourcesEvent = new
            {
                type = "sources",
                items = sources
                    .OrderBy(s => s.Page)
                    .Select(s => new
                    {
                        page = s.Page,
                        snippet = s.Text.Length > 160 ? s.Text[..160] + "…" : s.Text,
                        score = Math.Round(s.Score, 3)
                    })
            };
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(sourcesEvent, Json)}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            // 3. Stream the answer token by token
            await foreach (var token in documentService.StreamAnswerAsync(request.Question, sources, ct))
            {
                var tokenEvent = new { type = "token", text = token };
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(tokenEvent, Json)}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
