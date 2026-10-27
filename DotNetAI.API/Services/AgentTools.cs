using OpenAI.Chat;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotNetAI.API.Services
{
    public class AgentTools
    {
        private readonly DocumentService _docs;
        public AgentTools(DocumentService docs) => _docs = docs;

        // ── 1. What the model sees: name + description + JSON schema ─────────
        public IReadOnlyList<ChatTool> Definitions { get; } = new[]
        {
            ChatTool.CreateFunctionTool(
                functionName: "list_documents",
                functionDescription: "List the PDF documents the user has uploaded (file names and page counts).",
                functionParameters: BinaryData.FromString("""{"type":"object","properties":{}}""")),

            ChatTool.CreateFunctionTool(
                functionName: "search_documents",
                functionDescription: "Search the uploaded PDF documents and return the most relevant passages with file name and page number. Use this for any question about the content of the user's documents.",
                functionParameters: BinaryData.FromString("""
                    {
                      "type": "object",
                      "properties": {
                        "query": { "type": "string", "description": "What to look for, as a short search query" },
                        "document_name": { "type": "string", "description": "Optional: part of a file name, to search only that document" }
                      },
                      "required": ["query"]
                    }
                    """)),

            ChatTool.CreateFunctionTool(
                functionName: "calculate",
                functionDescription: "Evaluate an arithmetic expression using + - * / and parentheses. Always use this for maths instead of calculating yourself.",
                functionParameters: BinaryData.FromString("""
                    {
                      "type": "object",
                      "properties": {
                        "expression": { "type": "string", "description": "For example: 6000*5+1500*5" }
                      },
                      "required": ["expression"]
                    }
                    """))
        };

        // ── 2. What runs when the model asks for a tool ──────────────────────

        public async Task<string> ExecuteAsync(string name, string argsJson, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
            var args = doc.RootElement;

            return name switch
            {
                "list_documents" => ListDocuments(),
                "search_documents" => await SearchDocumentsAsync(args, ct),
                "calculate" => Calculate(args),
                _ => $"Unknown tool: {name}"
            };
        }
        private string ListDocuments()
        {
            var docs = _docs.GetDocuments().ToList();
            if (docs.Count == 0) return "No documents have been uploaded yet.";
            return string.Join("\n", docs.Select(d => $"- {d.FileName} ({d.Pages} pages)"));
        }
        private async Task<string> SearchDocumentsAsync(JsonElement args, CancellationToken ct)
        {
            var query = args.GetProperty("query").GetString() ?? "";
            string? documentName = args.TryGetProperty("document_name", out var d) ? d.GetString() : null;

            var hits = await _docs.SearchAllAsync(query, documentName, top: 5, ct: ct);
            if (hits.Count == 0) return "No matching passages found.";

            return string.Join("\n\n", hits.Select(h => $"[{h.FileName}, p. {h.Page}]\n{h.Text}"));
        }
        private static string Calculate(JsonElement args)
        {
            var expression = args.GetProperty("expression").GetString() ?? "";

            // Never trust model-written input: only digits, operators, brackets and spaces
            if (!Regex.IsMatch(expression, @"^[0-9+\-*/(). ]+$"))
                return "Only numbers and + - * / ( ) are allowed.";

            var result = new DataTable().Compute(expression, null);
            return $"{expression} = {result}";
        }

    }
}
