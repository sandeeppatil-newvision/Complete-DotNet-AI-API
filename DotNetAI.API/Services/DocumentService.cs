using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using static Qdrant.Client.Grpc.Conditions;


namespace DotNetAI.API.Services
{
    public record DocumentInfo(string Id, string FileName, int Pages, int Chunks, DateTime UploadedAt);
    public record PageText(int Page, string Text);
    public record SourceChunk(int Page, string Text, float Score);

    public record DocHit(string FileName, int Page, string Text, float Score);

    public class DocumentService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly QdrantClient _qdrant;
        private readonly IConfiguration _config;
        private readonly string _collection;

        private const ulong VectorSize = 1536;   // text-embedding-ada-002
        private const int ChunkChars = 1000;     // ~200 words per chunk
        private const int OverlapChars = 200;    // repeated text so sentences aren't cut in half

        // Uploaded documents — in memory for now (we move this to a database in Phase 4)
        private readonly ConcurrentDictionary<string, DocumentInfo> _documents = new();

        public DocumentService(AzureOpenAIClient azureClient, IConfiguration config)
        {
            _openAIClient = azureClient;
            _config = config;
            _collection = config["Qdrant:PdfCollectionName"] ?? "pdf-documents";

            // Same setup as QdrantVectorService — gRPC port 6334, no TLS for local Docker
            _qdrant = new QdrantClient(
                host: config["Qdrant:Host"] ?? "localhost",
                port: int.TryParse(config["Qdrant:Port"], out var p) ? p : 6334,
                https: false); 
        }

        public IEnumerable<DocumentInfo> GetDocuments() =>
           _documents.Values.OrderByDescending(d => d.UploadedAt);

        public DocumentInfo? GetDocument(string id) =>
            _documents.TryGetValue(id, out var doc) ? doc : null;


        // ── PART 1: PDF → text, page by page ────────────────────────────────
        public static (List<PageText> Pages, int TotalPages) ExtractPages(Stream pdfStream)
        {
            var pages = new List<PageText>();
            using var pdf = PdfDocument.Open(pdfStream);

            foreach (var page in pdf.GetPages())
            {
                // ContentOrderTextExtractor keeps words and lines in reading order
                var text = ContentOrderTextExtractor.GetText(page);
                if (!string.IsNullOrWhiteSpace(text))
                    pages.Add(new PageText(page.Number, text));
            }
            return (pages, pdf.NumberOfPages);
        }

        // ── PART 2: split every page into overlapping chunks ─────────────────

        // The page number travels with every chunk — that's what makes citations possible
        public static List<PageText> ChunkPages(List<PageText> pages)
        {
            var chunks = new List<PageText>();

            foreach (var page in pages)
            {
                var text = Regex.Replace(page.Text, @"\s+", " ").Trim();
                int start = 0;

                while (start < text.Length)
                {
                    int length = Math.Min(ChunkChars, text.Length - start);

                    // Prefer to end on a sentence, otherwise on a space
                    if (start + length < text.Length)
                    {
                        int cut = text.LastIndexOfAny(['.', '!', '?'], start + length - 1, length);
                        if (cut <= start + ChunkChars / 2)
                            cut = text.LastIndexOf(' ', start + length - 1, length);
                        if (cut > start)
                            length = cut - start + 1;
                    }

                    chunks.Add(new PageText(page.Page, text.Substring(start, length).Trim()));

                    if (start + length >= text.Length) break;
                    start += Math.Max(1, length - OverlapChars);
                }
            }
            return chunks;
        }

        // ── PART 3: chunks → embeddings → Qdrant ─────────────────────────────
        public async Task<DocumentInfo> IngestPdfAsync(string fileName, Stream pdfStream, CancellationToken ct = default)
        {
            var (pages, totalPages) = ExtractPages(pdfStream);
            if (pages.Count == 0)
                throw new InvalidOperationException(
                    "No text found in this PDF. Scanned (image-only) PDFs need OCR first.");

            var chunks = ChunkPages(pages);
            await EnsureCollectionAsync();

            var documentId = Guid.NewGuid().ToString("N")[..8];
            var embClient = _openAIClient.GetEmbeddingClient(_config["AzureOpenAI:EmbeddingDeployment"]!);
            var points = new List<PointStruct>();

            // Batch 16 chunks per call — far fewer round trips than one call per chunk
            foreach (var batch in chunks.Chunk(16))
            {
                var result = await embClient.GenerateEmbeddingsAsync(
                    batch.Select(c => c.Text), cancellationToken: ct);

                for (int i = 0; i < batch.Length; i++)
                {
                    points.Add(new PointStruct
                    {
                        Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                        Vectors = result.Value[i].ToFloats().ToArray(),
                        Payload =
                        {
                            ["documentId"] = documentId,
                            ["fileName"]   = fileName,
                            ["page"]       = (long)batch[i].Page,   // Qdrant numbers must be long
                            ["content"]    = batch[i].Text
                        }
                    });
                }
            }

            await _qdrant.UpsertAsync(_collection, points, cancellationToken: ct);

            var info = new DocumentInfo(documentId, fileName, totalPages, chunks.Count, DateTime.UtcNow);
            _documents[documentId] = info;
            return info;
        }

        private async Task EnsureCollectionAsync()
        {
            var collections = await _qdrant.ListCollectionsAsync();
            if (collections.Contains(_collection)) return;

            await _qdrant.CreateCollectionAsync(_collection, new VectorParams
            {
                Size = VectorSize,
                Distance = Distance.Cosine
            });

            // Index documentId so "search only inside this PDF" stays fast
            await _qdrant.CreatePayloadIndexAsync(_collection, "documentId");
        }

        // ── PART 4: question → embedding → search ONLY this document ────────
        public async Task<List<SourceChunk>> SearchAsync(string documentId, string question, int top = 5, CancellationToken ct = default)
        {
            var embClient = _openAIClient.GetEmbeddingClient(_config["AzureOpenAI:EmbeddingDeployment"]!);
            var q = await embClient.GenerateEmbeddingAsync(question, cancellationToken: ct);

            var results = await _qdrant.SearchAsync(
                collectionName: _collection,
                vector: q.Value.ToFloats().ToArray(),
                filter: MatchKeyword("documentId", documentId),   // ← the key line
                limit: (ulong)top,
                cancellationToken: ct);

            return results.Select(r => new SourceChunk(
                (int)r.Payload["page"].IntegerValue,
                r.Payload["content"].StringValue,
                r.Score)).ToList();
        }

        // ── PART 5: stream a grounded answer with page citations ─────────────
        public async IAsyncEnumerable<string> StreamAnswerAsync(
            string question,
            List<SourceChunk> sources,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var context = string.Join("\n\n", sources.Select(s => $"[Page {s.Page}]\n{s.Text}"));

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("""
                    You answer questions about ONE uploaded PDF document.
                    Use ONLY the sources provided. Each source starts with its page number.
                    After every fact, cite the page like this: [p. 3].
                    If the answer is not in the sources, reply exactly:
                    "I couldn't find that in this document."
                    Never use outside knowledge.
                    """),
                new UserChatMessage($"Sources:\n{context}\n\nQuestion: {question}")
            };

            var chatClient = _openAIClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, cancellationToken: ct))
                foreach (var part in update.ContentUpdate)
                    if (!string.IsNullOrEmpty(part.Text))
                        yield return part.Text;
        }

        // ── PART 6: delete a document and all its vectors ────────────────────
        public async Task<bool> DeleteAsync(string documentId)
        {
            if (!_documents.TryRemove(documentId, out _)) return false;
            await _qdrant.DeleteAsync(_collection, MatchKeyword("documentId", documentId));
            return true;
        }


        // ── AGENT: search every uploaded PDF, or just one by (part of) its file name ──
        public async Task<List<DocHit>> SearchAllAsync(string question, string? fileName = null, int top = 5, CancellationToken ct = default)
        {
            Filter? filter = null;

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var match = _documents.Values.FirstOrDefault(d =>
                    d.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));
                if (match is null) return new List<DocHit>();

                // documentId has a payload index (EP14), so this stays fast
                filter = new Filter { Must = { MatchKeyword("documentId", match.Id) } };
            }

            var embClient = _openAIClient.GetEmbeddingClient(_config["AzureOpenAI:EmbeddingDeployment"]!);
            var q = await embClient.GenerateEmbeddingAsync(question, cancellationToken: ct);

            var results = await _qdrant.SearchAsync(
                collectionName: _collection,
                vector: q.Value.ToFloats().ToArray(),
                filter: filter,                       // null = search everything
                limit: (ulong)top,
                cancellationToken: ct);

            return results.Select(r => new DocHit(
                r.Payload["fileName"].StringValue,
                (int)r.Payload["page"].IntegerValue,
                r.Payload["content"].StringValue,
                r.Score)).ToList();
        }
    }
}
