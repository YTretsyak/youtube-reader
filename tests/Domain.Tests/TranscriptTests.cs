using Domain;
using Xunit;

namespace Domain.Tests;

public class TranscriptTests
{
    [Fact]
    public void Constructor_NonEmptyText_SetsText()
    {
        var transcript = new Transcript("hello world");

        Assert.Equal("hello world", transcript.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceText_Throws(string? text)
    {
        Assert.Throws<ArgumentException>(() => new Transcript(text!));
    }
}
