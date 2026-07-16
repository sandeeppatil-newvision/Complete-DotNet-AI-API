using Azure;
using Azure.AI.OpenAI;
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
 
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
