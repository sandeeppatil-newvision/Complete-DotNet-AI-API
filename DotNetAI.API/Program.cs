using Azure;
using Azure.AI.OpenAI;
using DotNetAI.API.Plugins;
using DotNetAI.API.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.SemanticKernel;
using OpenAI;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);

// CORS — allowed origins come from configuration
// Local: falls back to the Vite dev server. Azure: set Cors__Origins__0 in App Service.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register AzureOpenAIClient — reused across every request
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new AzureOpenAIClient(
        new Uri(config["AzureOpenAI:Endpoint"]!),
        new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!));
});

builder.Services.AddSingleton<OpenAIClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new OpenAIClient(config["OpenAI:ApiKey"]!);
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var kb = Kernel.CreateBuilder();
    kb.AddAzureOpenAIChatCompletion(
        deploymentName: config["AzureOpenAI:DeploymentName"]!,
        endpoint: config["AzureOpenAI:Endpoint"]!,
        apiKey: config["AzureOpenAI:ApiKey"]!);
    var kernel = kb.Build();
    kernel.Plugins.AddFromType<DevToolsPlugin>(); // we build this next
    kernel.Plugins.AddFromType<ProjectPlugin>();
    return kernel;
});
builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<QdrantVectorService>();
builder.Services.AddSingleton<DocumentService>();

builder.Services.AddSingleton<AgentTools>();     // ← new
builder.Services.AddSingleton<AgentService>();   // ← new

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("agent", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// App Service runs behind a proxy: trust X-Forwarded-For / X-Forwarded-Proto
// so the rate limiter sees the real client IP.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();   // first middleware: before anything reads the client IP

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
 
}

app.UseHttpsRedirection();

app.UseCors("ReactApp");   // must come before UseAuthorization and MapControllers
app.UseRateLimiter();

app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
