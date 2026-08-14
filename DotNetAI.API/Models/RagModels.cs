using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.SemanticKernel.Services;
using OpenAI;
using OpenAI.Chat;

namespace DotNetAI.API.Models
{
    public class RagModels
    {

        public record IngestRequest(string DocumentName,string Content);      // document text to ingest

        public record AskRequest(string Question);

        public record AskResponse(string Answer,string[] Sources);
    }
}

