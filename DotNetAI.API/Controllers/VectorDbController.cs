using DotNetAI.API.Models;
using DotNetAI.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotNetAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VectorDbController : ControllerBase
    {
        private readonly QdrantVectorService _qdrant;
        private readonly RagService _azure;   // reuses EP05 RagService This is Azure vector service — no new class needed

        public VectorDbController(
            QdrantVectorService qdrant,
            RagService azure)
        {
            _qdrant = qdrant;
            _azure = azure;
        }

        // ── QDRANT ────────────────────────────────────────────────────────────

        /// <summary>
        /// Ingest a document into Qdrant.
        /// Chunks the text, generates embeddings, stores as PointStructs.
        /// POST /api/vectordb/qdrant/ingest
        /// </summary>
        [HttpPost("qdrant/ingest")]
        public async Task<ActionResult<object>> QdrantIngest(
            [FromBody] VectorIngestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Content cannot be empty." });

            var count = await _qdrant.IngestDocumentAsync(
                request.DocName, request.Content);

            return Ok(new
            {
                message = $"Qdrant: {count} chunk(s) stored.",
                docName = request.DocName,
                chunks = count,
                database = "Qdrant"
            });
        }

        /// <summary>
        /// Ask a question using Qdrant vector search.
        /// POST /api/vectordb/qdrant/ask
        /// </summary>
        [HttpPost("qdrant/ask")]
        public async Task<ActionResult<object>> QdrantAsk(
            [FromBody] VectorAskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Question cannot be empty." });

            var (answer, sources) = await _qdrant.AskAsync(request.Question);

            return Ok(new
            {
                answer,
                sources,
                database = "Qdrant"
            });
        }


        //Azure Vector Search - EP 05 RagService is reused for Azure vector search

        // ── AZURE AI SEARCH (via RagService) ──────────────────────────────────

        /// <summary>
        /// Ingest a document into Azure AI Search.
        /// Reuses RagService from EP05 — same HNSW vector index.
        /// POST /api/vectordb/azure/ingest
        /// </summary>
        [HttpPost("azure/ingest")]
        public async Task<ActionResult<object>> AzureIngest(
            [FromBody] VectorIngestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Content cannot be empty." });

            var count = await _azure.IngestDocumentAsync(
                request.DocName, request.Content);

            return Ok(new
            {
                message = $"Azure AI Search: {count} chunk(s) indexed.",
                docName = request.DocName,
                chunks = count,
                database = "Azure AI Search"
            });
        }

        /// <summary>
        /// Ask a question using Azure AI Search HNSW vector search.
        /// Reuses RagService from EP05.
        /// POST /api/vectordb/azure/ask
        /// </summary>
        [HttpPost("azure/ask")]
        public async Task<ActionResult<object>> AzureAsk(
            [FromBody] VectorAskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Question cannot be empty." });

            var (answer, sources) = await _azure.AskAsync(request.Question);

            return Ok(new
            {
                answer,
                sources,
                database = "Azure AI Search"
            });
        }

    }
}
