using System;
using FluentAssertions;
using SharpDmp.Extensions;
using Xunit;

namespace SharpDmp.UnitTests.Extensions;

public class ReadOnlySpanExtensionsTests
{
    [Theory]
    [InlineData("abc", "xyz")]
    [InlineData("abcdef", "xyz")]
    [InlineData("abc", "uvwxyz")]
    public void FindCommonPrefix_ShouldReturnZero_WhenInputsHaveNoCommonPrefix(string text1, string text2)
    {
        // arrange
        // act
        var actual = text1.AsSpan().FindCommonPrefix(text2);

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
        var actual = text1.AsSpan().FindCommonPrefix(text2);

        // assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", "xyz")]
    [InlineData("abcdef", "xyz")]
    [InlineData("abc", "uvwxyz")]
    public void FindCommonSuffix_ShouldReturnZero_WhenInputsHaveNoCommonSuffix(string text1, string text2)
    {
        // arrange
        // act
        var actual = text1.AsSpan().FindCommonSuffix(text2);

        // assert
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("acdef1234", "xyz1234", 4)]
    [InlineData("xyz1234", "acdef1234", 4)]
    [InlineData("1234", "xyz1234", 4)]
    [InlineData("xyz1234", "1234", 4)]
    public void FindCommonSuffix_ShouldReturnLengthOfSuffix_WhenInputsHaveCommonSuffix(
        string text1,
        string text2,
        int expected
    )
    {
        // arrange
        // act
        var actual = text1.AsSpan().FindCommonSuffix(text2);

        // assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("", "abcd")]
    [InlineData("abcd", "")]
    public void FindCommonOverlap_ShouldReturnZero_WhenOneInputIsEmpty(string text1, string text2)
    {
        // arrange
        // act
        var actual = text1.AsSpan().FindCommonOverlap(text2);

        // assert
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("123456", "abcd", 0)]
    [InlineData("abcd", "123456", 0)]
    [InlineData("fi", "\ufb01i", 0)]
    [InlineData("\ufb01i", "fi", 0)]
    [InlineData("abc", "abcd", 3)]
    [InlineData("123456xxx", "xxxabcd", 3)]
    public void FindCommonOverlap_ShouldReturnNumberOfCommonCharacters(string text1, string text2, int expected)
    {
        // arrange
        // act
        var actual = text1.AsSpan().FindCommonOverlap(text2);

        // assert
        actual.Should().Be(expected);
    }
}
