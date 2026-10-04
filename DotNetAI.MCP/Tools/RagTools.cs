using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DotNetAI.MCP.Tools
{

    [McpServerToolType]
    public class RagTools(IHttpClientFactory factory)
    {
        [McpServerTool(Name = "ask_documents")]
        [Description("Ask a question about documents indexed in the knowledge base.")]
        public async Task<string> AskDocuments(
   [Description("The question to ask")] string question,
   CancellationToken ct)
        {
            var client = factory.CreateClient("DotNetAI");

            var response = await client.PostAsJsonAsync(
                "/api/Rag/AskQuestion",
                new RagRequest(question), ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return $"DotNetAI.Api returned error sandeep- {(int)response.StatusCode}: {body}";
            }

            return await response.Content.ReadAsStringAsync(ct);
        }

        private record RagRequest(string Question);
        //private record RagResponse(string Answer, List<string>? Sources);
    }
}
