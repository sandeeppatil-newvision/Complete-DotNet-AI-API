using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace DotNetAI.API.Plugins
{
    public class DevToolsPlugin
    {
        [KernelFunction]
        [Description("Suggests the best .NET design pattern for a described coding problem")]
        public string SuggestPattern(
        [Description("Description of the coding problem to solve")] string problem)
        {
            return problem.ToLower() switch
            {
                var p when p.Contains("notify") || p.Contains("event")
                    => "Observer Pattern — one change triggers multiple listeners.",
                var p when p.Contains("create") || p.Contains("instantiate")
                    => "Factory Pattern — centralize object creation logic.",
                var p when p.Contains("database") || p.Contains("repository")
                    => "Repository Pattern — abstract data access from business logic.",
                var p when p.Contains("cache")
                    => "Decorator Pattern — wrap expensive calls with a caching layer.",
                _ => "Strategy Pattern — interchangeable algorithms or behaviours."
            };
        }

        [KernelFunction]
        [Description("Estimates code complexity based on line count")]
        public string AnalyzeComplexity(
        [Description("Number of lines of code to evaluate")] int lineCount)
        {
            return lineCount switch
            {
                < 20 => $"{lineCount} lines — Simple. Easy to test.",
                < 60 => $"{lineCount} lines — Moderate. Extract helper methods.",
                < 150 => $"{lineCount} lines — Complex. Strong refactor candidate.",
                _ => $"{lineCount} lines — Very complex. Break into smaller classes."
            };
        }
    }
}
