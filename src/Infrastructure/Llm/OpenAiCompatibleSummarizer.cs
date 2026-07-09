using System.Net;
using System.Text;
using System.Text.Json;
using Application;
using Domain;

namespace Infrastructure.Llm;

public sealed class OpenAiCompatibleSummarizer : ISummarizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _provider;

    public OpenAiCompatibleSummarizer(HttpClient httpClient, string model, string provider)
    {
        _httpClient = httpClient;
        _model = model;
        _provider = provider;
    }

    public async Task<SummarizerResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest(_model, new[]
        {
            new ChatMessage("system", "Summarize the following YouTube video transcript in 3-5 sentences."),
            new ChatMessage("user", transcriptText)
        });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return SummarizerResult.Failure("LLM provider rate-limited the request (429).");

        if (!response.IsSuccessStatusCode)
            return SummarizerResult.Failure($"LLM provider returned {(int)response.StatusCode}.");

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        ChatCompletionResponse? completion;
        try
        {
            completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions);
        }
        catch (JsonException)
        {
            return SummarizerResult.Failure("LLM provider returned a malformed response.");
        }

        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            return SummarizerResult.Failure("LLM provider returned an empty completion.");

        return SummarizerResult.Success(new Summary(content.Trim(), _provider, _model));
    }

    private sealed record ChatCompletionRequest(string Model, ChatMessage[] Messages);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(ChatChoice[]? Choices);

    private sealed record ChatChoice(ChatMessage? Message);
}
