using System.Text;
using Bogus.DataSets;
using FluentAssertions;
using SharpDmp.Extensions;
using Xunit;

namespace SharpDmp.UnitTests.Extensions;

public class StringBuilderExtensionsTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\n\n", true)]
    [InlineData("\n\r\n", true)]
    public void EndsWithBlankLine_ShouldIdentifyStringsEndingWithBlankLines(string ending, bool expected)
    {
        // arrange
        var lorem = new Lorem();
        var sut = new StringBuilder(lorem.Word() + ending);

        // act
        var actual = sut.EndsWithBlankLine();

        // assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("a")]
    public void EndsWithBlankLine_ShouldReturnFalse_WhenInputIsShort(string text)
    {
        // arrange
        var sut = new StringBuilder(text);

        // act
        var result = sut.EndsWithBlankLine();

        // assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\n\n", true)]
    [InlineData("\r\n\r\n", true)]
    [InlineData("\r\n\n", true)]
    [InlineData("\n\r\n", true)]
    public void StartsWithBlankLine_ShouldIdentifyStringsStartingWithBlankLines(string beginning, bool expected)
    {
        // arrange
        var lorem = new Lorem();
        var sut = new StringBuilder(beginning + lorem.Word());

        // act
        var actual = sut.StartsWithBlankLine();

        // assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("a")]
    public void StartsWithBlankLine_ShouldReturnFalse_WhenInputIsShort(string text)
    {
        // arrange
        var sut = new StringBuilder(text);

        // act
        var result = sut.StartsWithBlankLine();

        // assert
        result.Should().BeFalse();
    }
}
