using Azure;
using Azure.AI.OpenAI;
using DotNetAI.API.Plugins;
using DotNetAI.API.Services;
using Microsoft.SemanticKernel;
using OpenAI;
using Scalar.AspNetCore;
var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
 
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();

app.Run();
