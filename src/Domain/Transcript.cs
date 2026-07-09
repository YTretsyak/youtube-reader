namespace Domain;

public sealed class Transcript
{
    public string Text { get; }

    public Transcript(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Transcript cannot be empty.", nameof(text));

        Text = text;
    }
}
