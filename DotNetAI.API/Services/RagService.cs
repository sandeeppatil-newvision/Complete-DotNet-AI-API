using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using OpenAI;
using OpenAI.Chat;

namespace DotNetAI.API.Services
{
    public class RagService
    {
        //constructor + index creation 
        private readonly OpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly SearchIndexClient _searchIndexClient;
        private readonly IConfiguration _configuration;
        private const int chunkSize = 500; // Define the chunk size for splitting text

        //constructor
        public RagService(AzureOpenAIClient azureOpenAIClient, IConfiguration configuration)
        {
            _openAIClient = azureOpenAIClient;
            _configuration = configuration;

            // Initialize the SearchIndexClient and SearchClient
            string searchServiceEndpoint = _configuration["AzureSearch:Endpoint"];
            string searchServiceApiKey = _configuration["AzureSearch:ApiKey"];
            string indexName = _configuration["AzureSearch:IndexName"];


            Uri serviceEndpoint = new Uri(searchServiceEndpoint);
            AzureKeyCredential credential = new AzureKeyCredential(searchServiceApiKey);
            _searchIndexClient = new SearchIndexClient(serviceEndpoint, credential);
            _searchClient = new SearchClient(serviceEndpoint, indexName, credential);
        }

        // Part 1- Create vector search index if it doesn't already exist

        // Create vector search index if it doesn't already exist
        // Create vector search index if it doesn't already exist
        public async Task EnsureIndexExistsAsync()
        {
            var indexName = _configuration["AzureSearch:IndexName"]!;
            var fields = new SearchField[]
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                new SimpleField("docName", SearchFieldDataType.String) { IsFilterable = true },
                new SearchableField("content"),
                new VectorSearchField("contentVector", 1536, "my-vector-profile")
            };

            // Create the HNSW algorithm configuration
            var algorithmConfig = new HnswAlgorithmConfiguration("my-hnsw-config");

            var vectorSearch = new VectorSearch();
            vectorSearch.Algorithms.Add(algorithmConfig);

            // Create a profile that connects the field to the algorithm
            vectorSearch.Profiles.Add(
                new VectorSearchProfile("my-vector-profile", "my-hnsw-config"));


            await _searchIndexClient.CreateOrUpdateIndexAsync(
                new SearchIndex(indexName, fields) { VectorSearch = vectorSearch });
        }

        // Part 2- Chunk the document, embed each chunk, store in Azure AI Search

        public async Task<int> IngestDocumentAsync(string docName, string content)
        {
            await EnsureIndexExistsAsync();

            var chunks = ChunkText(content, chunkSize);
            var embClient = _openAIClient.GetEmbeddingClient(_configuration["AzureOpenAI:EmbeddingDeployment"]!);

            var docs = new List<SearchDocument>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var embedding = await embClient
                    .GenerateEmbeddingAsync(chunks[i]);
                docs.Add(new SearchDocument
                {
                    ["id"] = $"{docName}-chunk-{i}",
                    ["docName"] = docName,
                    ["content"] = chunks[i],
                    ["contentVector"] = embedding.Value.ToFloats().ToArray()
                });
            }
            await _searchClient.UploadDocumentsAsync(docs);
            return chunks.Count; // return how many chunks were stored
        }

        private static List<string> ChunkText(string text, int size)
        {
            var chunks = new List<string>();
            var words = text.Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i += size - 50)
                chunks.Add(string.Join(" ", words.Skip(i).Take(size)));
            return chunks;
        }

        //Part 3 - Search for relevant chunks, generate answer using OpenAI

        public async Task<(string Answer, string[] Sources)> AskAsync(string question)
        {
            var embClient = _openAIClient.GetEmbeddingClient(
                _configuration["AzureOpenAI:EmbeddingDeployment"]!);

            // 1. Embed the question — same model used for document chunks
            var qEmbedding = await embClient.GenerateEmbeddingAsync(question);
            var vector = qEmbedding.Value.ToFloats().ToArray();

            // 2. Vector search → top 3 relevant chunks
            var vQuery = new VectorizedQuery(vector)
            {
                KNearestNeighborsCount = 3,
                Fields = { "contentVector" }
            };
            var options = new SearchOptions { Size = 3 };
            options.VectorSearch ??= new VectorSearchOptions();
            options.VectorSearch.Queries.Add(vQuery);

            var results = await _searchClient
                 .SearchAsync<SearchDocument>(options);
            var chunks = new List<string>();
            await foreach (var r in results.Value.GetResultsAsync())
                if (r.Document.TryGetValue("content", out var c))
                    chunks.Add(c?.ToString() ?? "");

            // 3. Build context prompt — AI uses ONLY document content
            var context = string.Join("\n\n", chunks);
            var prompt = $"""
            Answer using ONLY the document context below.
            If the answer is not in the context, say:
            "I don't have enough information in the provided documents."
            Do not use outside knowledge.

            Context: {context}
            Question: {question}
            """;

            // 4. GPT-4.1 generates the grounded answer
            var chatClient = _openAIClient.GetChatClient(
                _configuration["AzureOpenAI:DeploymentName"]!);
            var result = await chatClient
                .CompleteChatAsync([new UserChatMessage(prompt)]);

            return (result.Value.Content[0].Text,
                    chunks.Take(3).Select((_, i) =>
                        $"Chunk {i + 1}").ToArray());
        }
    }
}