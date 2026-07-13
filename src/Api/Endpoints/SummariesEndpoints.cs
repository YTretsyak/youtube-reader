using System.Text.Json;
using Api.Contracts;
using Application;
using Domain;

namespace Api.Endpoints;

public static class SummariesEndpoints
{
    public static RouteGroupBuilder MapSummariesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/summaries").WithTags("Summaries").RequireRateLimiting("summaries");

        group.MapPost("/", PostSummaryAsync)
            .WithName("SubmitSummary")
            .WithSummary("Submit a YouTube video URL for summarization.")
            .Produces<SummaryResponse>(StatusCodes.Status200OK)
            .Produces<SummaryResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/", GetHistoryAsync)
            .WithName("GetSummaryHistory")
            .WithSummary("List previously submitted videos, newest first.")
            .Produces<IReadOnlyList<SummaryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id}", GetSummaryByIdAsync)
            .WithName("GetSummaryById")
            .WithSummary("Get a single video's record and current processing status.")
            .Produces<SummaryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetHistoryAsync(GetSummaryHistory getSummaryHistory, CancellationToken cancellationToken)
    {
        var history = await getSummaryHistory.ExecuteAsync(cancellationToken);
        return Results.Ok(history.Select(SummaryResponse.From).ToList());
    }

    private static async Task<IResult> GetSummaryByIdAsync(string id, ISummaryRepository repository, CancellationToken cancellationToken)
    {
        var video = await repository.GetByVideoIdAsync(new VideoId(id), cancellationToken);
        return video is null ? Results.NotFound() : Results.Ok(SummaryResponse.From(video));
    }

    private static async Task<IResult> PostSummaryAsync(
        HttpContext context, SummarizeVideo summarizeVideo, CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
            return Results.BadRequest(new { error = "Content-Type must be application/json." });

        SubmitSummaryRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<SubmitSummaryRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Request body is not valid JSON." });
        }

        if (request is null || !VideoUrl.TryParse(request.Url, out var videoUrl))
            return Results.BadRequest(new { error = "url is missing or is not a valid YouTube link." });

        Video video;
        try
        {
            video = await summarizeVideo.ExecuteAsync(videoUrl!, cancellationToken);
        }
        catch (Exception)
        {
            // SummarizeVideo's only downstream calls are the Mongo save and the RabbitMQ publish;
            // either being unreachable at submit time maps to 503 per docs/specification.md §5.
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "The service is temporarily unavailable.");
        }

        var response = SummaryResponse.From(video);
        return video.Status == VideoStatus.Processed ? Results.Ok(response) : Results.Accepted(value: response);
    }
}
