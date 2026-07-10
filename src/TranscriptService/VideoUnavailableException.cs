namespace TranscriptService;

public sealed class VideoUnavailableException : Exception
{
    public VideoUnavailableException(string message) : base(message)
    {
    }
}
