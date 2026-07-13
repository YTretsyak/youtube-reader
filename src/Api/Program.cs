using Api.Endpoints;
using Application;
using Domain;
using Infrastructure.Llm;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using MongoDB.Driver;
using RabbitMQ.Client;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var connectionString = builder.Configuration["MONGO_CONNECTION_STRING"] ?? "mongodb://localhost:27017";
    return new MongoClient(MongoClientSettingsFactory.Create(connectionString));
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("youtube_reader").GetCollection<VideoDocument>("videos"));
builder.Services.AddSingleton<IVideoDocumentStore>(sp =>
    new MongoVideoDocumentStore(sp.GetRequiredService<IMongoCollection<VideoDocument>>()));
builder.Services.AddSingleton<ISummaryRepository>(sp =>
    new MongoSummaryRepository(sp.GetRequiredService<IVideoDocumentStore>()));

const string LlmProviderName = "github-models";
var llmModel = builder.Configuration["LLM_MODEL"] ?? "openai/gpt-4o-mini";
builder.Services.AddHttpClient("llm", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LLM_BASE_URL"] ?? "https://models.github.ai/inference");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["LLM_API_KEY"] ?? string.Empty);
});
builder.Services.AddSingleton<ISummarizer>(sp =>
    new OpenAiCompatibleSummarizer(sp.GetRequiredService<IHttpClientFactory>().CreateClient("llm"), llmModel, LlmProviderName));

builder.Services.AddSingleton<SummarizeVideo>();
builder.Services.AddSingleton<GetSummaryHistory>();

// RabbitMQ (publish side) and Mongo index creation both require a reachable broker/DB, which
// WebApplicationFactory-based tests (Api.Tests) don't have — skip both under the "Testing"
// environment so the app boots on fakes alone. See Task 6 for the results-consumer side.
if (!builder.Environment.IsEnvironment("Testing"))
{
    var rabbitConnectionFactory = new ConnectionFactory
    {
        Uri = new Uri(builder.Configuration["RABBITMQ_URI"] ?? "amqp://guest:guest@localhost:5672")
    };
    var rabbitConnection = await rabbitConnectionFactory.CreateConnectionAsync();
    var rabbitChannel = await rabbitConnection.CreateChannelAsync();
    await RabbitMqTopology.DeclareRequestsTopologyAsync(rabbitChannel);
    await RabbitMqTopology.DeclareResultsTopologyAsync(rabbitChannel);

    builder.Services.AddSingleton(rabbitConnection);
    builder.Services.AddSingleton(rabbitChannel);
    builder.Services.AddSingleton<ITranscriptRequestPublisher>(sp =>
        new RabbitMqTranscriptRequestPublisher(sp.GetRequiredService<IChannel>()));
}

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    var collection = app.Services.GetRequiredService<IMongoCollection<VideoDocument>>();
    await MongoVideoDocumentStore.EnsureIndexesAsync(collection);
}

app.MapHealthChecks("/health");
app.MapSummariesEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.Run();

public partial class Program { }
