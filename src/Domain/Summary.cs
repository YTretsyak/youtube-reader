namespace Domain;

public sealed class Summary
{
    public string Text { get; }
    public string Provider { get; }
    public string Model { get; }

    public Summary(string text, string provider, string model)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Summary text cannot be empty.", nameof(text));
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty.", nameof(provider));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be empty.", nameof(model));

        Text = text;
        Provider = provider;
        Model = model;
    }
}
