using RabbitMQ.Client;
using TranscriptService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    Uri = new Uri(builder.Configuration["RABBITMQ_URI"] ?? "amqp://guest:guest@localhost:5672")
});

builder.Services.AddHttpClient<IYoutubeVideoClient, YoutubeExplodeVideoClient>();
builder.Services.AddSingleton<ITranscriptFetcher, YoutubeExplodeTranscriptFetcher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
