using Api.Tests.TestDoubles;
using Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public InMemorySummaryRepository Repository { get; } = new();
    public RecordingTranscriptRequestPublisher Publisher { get; } = new();
    public StubSummarizer Summarizer { get; } = new(SummarizerResult.Failure("not configured"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISummaryRepository>();
            services.AddSingleton<ISummaryRepository>(Repository);

            services.RemoveAll<ITranscriptRequestPublisher>();
            services.AddSingleton<ITranscriptRequestPublisher>(Publisher);

            services.RemoveAll<ISummarizer>();
            services.AddSingleton<ISummarizer>(Summarizer);
        });
    }
}
