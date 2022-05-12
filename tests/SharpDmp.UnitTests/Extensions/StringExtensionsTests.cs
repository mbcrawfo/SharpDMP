using System;
using FluentAssertions;
using SharpDmp.Extensions;
using Xunit;

namespace SharpDmp.UnitTests.Extensions;

public class StringExtensionsTests
{
    [Fact]
    public void FindCommonPrefix_ShouldThrowArgumentNullException_WhenText1IsNull()
    {
        // arrange
        // act
        Action act = () => _ = StringExtensions.FindCommonPrefix(null!, "");

        // assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("text1");
    }

    [Fact]
    public void FindCommonPrefix_ShouldThrowArgumentNullException_WhenText2IsNull()
    {
        // arrange
        // act
        Action act = () => _ = "".FindCommonPrefix(null!);

        // assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("text2");
    }

    [Theory]
    [InlineData("abc", "xyz")]
    [InlineData("abcdef", "xyz")]
    [InlineData("abc", "uvwxyz")]
    public void FindCommonPrefix_ShouldReturnZero_WhenInputsHaveNoCommonPrefix(string text1, string text2)
    {
        // arrange
        // act
        var actual = text1.FindCommonPrefix(text2);

        // assert
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("1234abcdef", "1234xyz", 4)]
    [InlineData("1234xyz", "1234abcdef", 4)]
    [InlineData("1234", "1234xyz", 4)]
    [InlineData("1234xyz", "1234", 4)]
    public void FindCommonPrefix_ShouldReturnLengthOfPrefix_WhenInputsHaveCommonPrefix(
        string text1,
        string text2,
        int expected
    )
    {
        // arrange
        // act
        var actual = text1.FindCommonPrefix(text2);

        // assert
        actual.Should().Be(expected);
    }
}
