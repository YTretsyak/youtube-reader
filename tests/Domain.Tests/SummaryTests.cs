using Domain;
using Xunit;

namespace Domain.Tests;

public class SummaryTests
{
    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var summary = new Summary("a short summary", "github-models", "openai/gpt-4o-mini");

        Assert.Equal("a short summary", summary.Text);
        Assert.Equal("github-models", summary.Provider);
        Assert.Equal("openai/gpt-4o-mini", summary.Model);
    }

    [Theory]
    [InlineData(null, "github-models", "openai/gpt-4o-mini")]
    [InlineData("", "github-models", "openai/gpt-4o-mini")]
    [InlineData("a short summary", null, "openai/gpt-4o-mini")]
    [InlineData("a short summary", "", "openai/gpt-4o-mini")]
    [InlineData("a short summary", "github-models", null)]
    [InlineData("a short summary", "github-models", "")]
    public void Constructor_MissingArgument_Throws(string? text, string? provider, string? model)
    {
        Assert.Throws<ArgumentException>(() => new Summary(text!, provider!, model!));
    }
}
