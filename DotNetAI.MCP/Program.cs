using DotNetAI.MCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// HttpClient → calls DotNetAI.Api for the RAG tool
builder.Services.AddHttpClient("DotNetAI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7036");
});

// Register MCP server + all tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WeatherTools>()
    .WithTools<StockTools>()
    .WithTools<RagTools>();

await builder.Build().RunAsync();