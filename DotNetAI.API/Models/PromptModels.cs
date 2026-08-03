namespace DotNetAI.API.Models
{
    public class PromptModels
    {
        public record CodeReviewRequest(string code, string language = "csharp");

        public record CodeReviewResponse(
        string Verdict,        // APPROVED | NEEDS_WORK | REJECTED
        string[] Issues,         // specific issues found
        string[] Suggestions,    // improvement suggestions
        int ComplexityScore, // 1–10
        string Summary);

        public record BugReportRequest(string description);

        public record BugSeverityResult(string severity, string summary);

    }
}
