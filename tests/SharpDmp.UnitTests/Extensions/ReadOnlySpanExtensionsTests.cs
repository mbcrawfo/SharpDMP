using System;
using System.Collections;
using System.Linq;
using System.Text;
using FluentAssertions;
using FluentAssertions.Execution;
using SharpDmp.Data;
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

    [Theory]
    [InlineData("a", "")]
    [InlineData("", "a")]
    [InlineData("abc", "")]
    [InlineData("", "abc")]
    [InlineData("a", "1234567890")]
    [InlineData("1234567890", "a")]
    public void FindHalfMatch_ShouldReturnNull_WhenInputsAreShort(string text1, string text2)
    {
        // arrange
        var result = new HalfMatch();

        // act
        text1.AsSpan().FindHalfMatch(text2, ref result);

        // assert
        result.HasValue.Should().BeFalse();
    }

    [Theory]
    [InlineData("1234567890", "abcdef")]
    [InlineData("abcdef", "1234567890")]
    public void FindHalfMatch_ShouldReturnNull_WhenInputsDoNotShareAHalfLengthSubstring(string text1, string text2)
    {
        // arrange
        var result = new HalfMatch();

        // act
        text1.AsSpan().FindHalfMatch(text2, ref result);

        // assert
        result.HasValue.Should().BeFalse();
    }

    public static IEnumerable FindHalfMatchSingleTestCases =>
        new object[]
        {
            // Single match.
            new object[] { "1234567890", "a345678z", "12", "90", "a", "z", "345678" },
            new object[] { "a345678z", "1234567890", "a", "z", "12", "90", "345678" },
            new object[] { "abc56789z", "1234567890", "abc", "z", "1234", "0", "56789" },
            new object[] { "a23456xyz", "1234567890", "a", "xyz", "1", "7890", "23456" },
            // Multiple matches
            new object[]
            {
                "121231234123451234123121",
                "a1234123451234z",
                "12123",
                "123121",
                "a",
                "z",
                "1234123451234"
            },
            new object[]
            {
                "x-=-=-=-=-=-=-=-=-=-=-=-=",
                "xx-=-=-=-=-=-=-=",
                "",
                "-=-=-=-=-=",
                "x",
                "",
                "x-=-=-=-=-=-=-="
            },
            new object[]
            {
                "-=-=-=-=-=-=-=-=-=-=-=-=y",
                "-=-=-=-=-=-=-=yy",
                "-=-=-=-=-=",
                "",
                "",
                "y",
                "-=-=-=-=-=-=-=y"
            },
            // Non-optimal half match
            new object[] { "qHilloHelloHew", "xHelloHeHulloy", "qHillo", "w", "x", "Hulloy", "HelloHe" },
        };

    [Theory]
    [MemberData(nameof(FindHalfMatchSingleTestCases))]
    public void FindHalfMatch_ShouldReturnExpectedHalfMatch_WhenInputsShareAHalfLengthSubstring(
        string text1,
        string text2,
        string expectedText1Prefix,
        string expectedText1Suffix,
        string expectedText2Prefix,
        string expectedText2Suffix,
        string expectedCommonMiddle
    )
    {
        // arrange
        var result = new HalfMatch();

        // act
        text1.AsSpan().FindHalfMatch(text2, ref result);

        // assert
        using var _ = new AssertionScope();
        result.Text1Prefix.ToString().Should().Be(expectedText1Prefix);
        result.Text1Suffix.ToString().Should().Be(expectedText1Suffix);
        result.Text2Prefix.ToString().Should().Be(expectedText2Prefix);
        result.Text2Suffix.ToString().Should().Be(expectedText2Suffix);
        result.CommonMiddle.ToString().Should().Be(expectedCommonMiddle);
    }

    public static IEnumerable LinesToCharsEncodingTestCases =>
        new object[]
        {
            new object[]
            {
                "alpha\nbeta\nalpha\n",
                "beta\nalpha\nbeta\n",
                new[] { 1, 2, 1 },
                new[] { 2, 1, 2 },
                new[] { "", "alpha\n", "beta\n" }
            },
            new object[]
            {
                "",
                "alpha\r\nbeta\r\n\r\n\r\n",
                Array.Empty<int>(),
                new[] { 1, 2, 3, 3 },
                new[] { "", "alpha\r\n", "beta\r\n", "\r\n" }
            },
            new object[] { "a", "b", new[] { 1 }, new[] { 2 }, new[] { "", "a", "b" } },
        };

    [Theory]
    [MemberData(nameof(LinesToCharsEncodingTestCases))]
    public void LinesToChars_ShouldEncodeInputsByTheirUniqueStrings(
        string text1,
        string text2,
        int[] expectedEncodedText1,
        int[] expectedEncodedText2,
        string[] expectedUniqueStrings
    )
    {
        // arrange
        // act
        var (actualEncodedText1, actualEncodedText2, actualUniqueStrings) = text1.AsSpan().LinesToChars(text2);

        // assert
        using var _ = new AssertionScope();
        actualEncodedText1.Should().ContainInOrder(expectedEncodedText1);
        actualEncodedText2.Should().ContainInOrder(expectedEncodedText2);
        actualUniqueStrings.Should().ContainInOrder(expectedUniqueStrings);
    }

    [Fact]
    public void LinesToChars_ShouldEncodeInputs_WhenBothInputsHaveTooManyLines()
    {
        // arrange
        const int numberOfLines = 65540;
        const int text1MaxLines = 40000;
        const int text2MaxLines = 65535;
        var sb = new StringBuilder(numberOfLines * 2);
        foreach (var i in Enumerable.Range(1, numberOfLines))
        {
            sb.Append(i).Append('\n');
        }

        var text = sb.ToString();
        var expectedLastStringText1 =
            string.Join('\n', Enumerable.Range(text1MaxLines, numberOfLines - text1MaxLines + 1)) + "\n";
        var expectedLastStringText2 =
            string.Join('\n', Enumerable.Range(text2MaxLines - 1, numberOfLines - text2MaxLines + 2)) + "\n";

        // act
        var (actualEncodedText1, actualEncodedText2, actualUniqueStrings) = text.AsSpan().LinesToChars(text);

        // assert
        using var _ = new AssertionScope();
        actualEncodedText1.Should().HaveCount(text1MaxLines).And.Subject.Should().EndWith(text1MaxLines);
        actualEncodedText2.Should().HaveCount(text2MaxLines - 1).And.Subject.Should().EndWith(text2MaxLines);
        actualUniqueStrings
            .Should()
            .HaveCount(text2MaxLines + 1)
            .And.Subject.Should()
            .HaveElementAt(actualEncodedText1[^1], expectedLastStringText1)
            .And.Subject.Should()
            .EndWith(expectedLastStringText2);
    }

    [Fact]
    public void LinesToChars_ShouldEncodeInputs_WhenText1HasTooManyLines()
    {
        // arrange
        const int numberOfLines = 40005;
        const int maxLines = 40000;
        var sb = new StringBuilder(numberOfLines * 2);
        foreach (var i in Enumerable.Range(1, numberOfLines))
        {
            sb.Append(i).Append('\n');
        }

        var text1 = sb.ToString();
        var expectedLastUniqueString =
            string.Join('\n', Enumerable.Range(maxLines, numberOfLines - maxLines + 1)) + "\n";

        // act
        var (actualEncodedText1, _, actualUniqueStrings) = text1.AsSpan().LinesToChars("");

        // assert
        using var _ = new AssertionScope();
        actualEncodedText1.Should().HaveCount(maxLines).And.Subject.Should().EndWith(maxLines);
        actualUniqueStrings.Should().HaveCount(maxLines + 1).And.Subject.Should().EndWith(expectedLastUniqueString);
    }

    [Fact]
    public void LinesToChars_ShouldEncodeInputs_WhenText2HasTooManyLines()
    {
        // arrange
        const int numberOfLines = 65540;
        const int maxLines = 65535;
        var sb = new StringBuilder(numberOfLines * 2);
        foreach (var i in Enumerable.Range(1, numberOfLines))
        {
            sb.Append(i).Append('\n');
        }

        var text2 = sb.ToString();
        var expectedLastUniqueString =
            string.Join('\n', Enumerable.Range(maxLines, numberOfLines - maxLines + 1)) + "\n";

        // act
        var (_, actualEncodedText2, actualUniqueStrings) = "".AsSpan().LinesToChars(text2);

        // assert
        using var _ = new AssertionScope();
        actualEncodedText2.Should().HaveCount(maxLines).And.Subject.Should().EndWith(maxLines);
        actualUniqueStrings.Should().HaveCount(maxLines + 1).And.Subject.Should().EndWith(expectedLastUniqueString);
    }

    [Fact]
    public void LinesToChars_ShouldEncodeInputsByTheirUniqueStrings_WhenInputContainsMoreThan256Lines()
    {
        // arrange
        const int numberOfLines = 300;
        var text1 = string.Join('\n', Enumerable.Range(1, numberOfLines)) + "\n";
        var expectedUniqueStrings = Enumerable.Range(1, numberOfLines).Select(i => i + "\n").Prepend("");

        // act
        var (actualEncodedText1, actualEncodedText2, actualUniqueStrings) = text1.AsSpan().LinesToChars("");

        // assert
        using var _ = new AssertionScope();
        actualEncodedText1.Should().ContainInOrder(Enumerable.Range(1, numberOfLines));
        actualEncodedText2.Should().BeEmpty();
        actualUniqueStrings.Should().ContainInOrder(expectedUniqueStrings);
    }
}
