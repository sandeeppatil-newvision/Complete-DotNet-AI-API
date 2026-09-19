using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DotNetAI.API.Services
{
    public class QdrantVectorService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly QdrantClient _qdrant;
        private readonly IConfiguration _config;
        private const int ChunkSize = 500;
        private const ulong VectorSize = 1536;

        public QdrantVectorService(
            AzureOpenAIClient azureClient,
            IConfiguration config)
        {
            _openAIClient = azureClient;
            _config = config;
            // Qdrant .NET client uses gRPC — port 6334 (NOT the REST port 6333)
            //_qdrant = new QdrantClient(
            //    host: config["Qdrant:Host"] ?? "localhost",
            //    port: int.TryParse(config["Qdrant:Port"], out var p) ? p : 6334);

            _qdrant = new QdrantClient(
     host: config["Qdrant:Host"] ?? "localhost",
     port: int.TryParse(config["Qdrant:Port"], out var p) ? p : 6334,
     https: false);   // ← plain HTTP, no TLS for local Docker
        }

        // ── PART 1: Create Qdrant collection if it doesn't exist ─────────────
        public async Task EnsureCollectionAsync()
        {
            var name = _config["Qdrant:CollectionName"] ?? "documents";
            var collections = await _qdrant.ListCollectionsAsync();

            if (!collections.Contains(name))
            {
                await _qdrant.CreateCollectionAsync(name, new VectorParams
                {
                    Size = VectorSize,       // 1536 = text-embedding-ada-002 dimensions
                    Distance = Distance.Cosine   // cosine similarity for text embeddings
                });
            }
        }

        // ── PART 2: Chunk → embed → upsert into Qdrant ──────────────────────
        public async Task<int> IngestDocumentAsync(string docName, string content)
        {
            await EnsureCollectionAsync();

            var collName = _config["Qdrant:CollectionName"] ?? "documents";
            var chunks = ChunkText(content, ChunkSize);
            var embClient = _openAIClient
                .GetEmbeddingClient(_config["AzureOpenAI:EmbeddingDeployment"]!);
            var points = new List<PointStruct>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var result = await embClient.GenerateEmbeddingAsync(chunks[i]);
                var floats = result.Value.ToFloats().ToArray();

                points.Add(new PointStruct
                {
                    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                    Vectors = floats,   // float[] → Vectors via implicit operator
                    Payload =
                {
                    ["content"]    = chunks[i],
                    ["docName"]    = docName,
                    ["chunkIndex"] = (long)i   // Qdrant payload numbers must be long
                }
                });
            }

            await _qdrant.UpsertAsync(collName, points);
            return chunks.Count;
        }


        // ── PART 3: Embed question → search Qdrant → GPT-4o answer ──────────
        public async Task<(string Answer, string[] Sources)> AskAsync(string question)
        {
            var collName = _config["Qdrant:CollectionName"] ?? "documents";
            var embClient = _openAIClient
                .GetEmbeddingClient(_config["AzureOpenAI:EmbeddingDeployment"]!);

            // 1. Embed the question
            var qResult = await embClient.GenerateEmbeddingAsync(question);
            var qVector = qResult.Value.ToFloats().ToArray();

            // 2. Vector search — Qdrant finds top 3 by cosine similarity
            var results = await _qdrant.SearchAsync(
                collectionName: collName,
                vector: qVector,
                limit: 3);

            var chunks = results
                .Select(r => r.Payload.TryGetValue("content", out var v)
                    ? v.StringValue
                    : string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();

            if (chunks.Count == 0)
                return ("No relevant documents found. Please ingest documents first.", []);

            // 3. Grounded GPT-4o answer — only uses document content
            var context = string.Join("\n\n", chunks);
            var prompt = $"""
            Answer using ONLY the document context below.
            If the answer is not in the context, say:
            "I don't have enough information in the provided documents."
            Do not use outside knowledge.
 
            Context:
            {context}
 
            Question: {question}
            """;

            var chatClient = _openAIClient
                .GetChatClient(_config["AzureOpenAI:DeploymentName"]!);
            var chatResult = await chatClient
                .CompleteChatAsync([new UserChatMessage(prompt)]);

            return (
                chatResult.Value.Content[0].Text,
                chunks.Select((_, i) => $"Qdrant Chunk {i + 1}").ToArray()
            );
        }


        // ── HELPERS ──────────────────────────────────────────────────────────
        private static List<string> ChunkText(string text, int maxWords)
        {
            var chunks = new List<string>();
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i += maxWords - 50)
                chunks.Add(string.Join(" ", words.Skip(i).Take(maxWords)));
            return chunks;
        }

    }
}
