using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace DotNetAI.API.Plugins
{
    public class ProjectPlugin
    {
        [KernelFunction]
        [Description("Gets the current sprint status including open tasks and progress")]
        public string GetSprintStatus(
       [Description("The sprint number to check")] int sprintNumber)
        {
            // Real app: query Azure DevOps / Jira API here
            return $"Sprint {sprintNumber}: 8 tasks total, 5 open, 3 completed. " +
                   "2 tasks blocked. Sprint ends in 4 days. " +
                   "Velocity: 21 points. Remaining: 18 points.";
        }

        [KernelFunction]
        [Description("Gets the count of open bugs by severity for the current release")]
        public string GetBugCount()
        {
            // Real app: query your bug tracker here
            return "Open bugs: 2 Critical, 5 High, 12 Medium, 8 Low. " +
                   "Critical: payment timeout, session expiry. Both in progress.";
        }

        [KernelFunction]
        [Description("Gets all tasks assigned to a specific developer")]
        public string GetTasksByDeveloper(
            [Description("The developer name to look up")] string developerName)
        {
            return $"{developerName}'s tasks: " +
                   "1. Refactor OrderController (High, 8pts, In Progress), " +
                   "2. Add unit tests for PaymentService (Medium, 3pts, Open), " +
                   "3. Fix null ref in UserRepository (Critical, 2pts, Open).";
        }
    }
}
