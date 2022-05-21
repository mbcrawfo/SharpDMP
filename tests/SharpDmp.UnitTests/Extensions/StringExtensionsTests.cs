using System.Collections;
using Bogus.DataSets;
using FluentAssertions;
using SharpDmp.Extensions;
using Xunit;

namespace SharpDmp.UnitTests.Extensions;

public class StringExtensionsTests
{
    public static IEnumerable EncodeUriBasicTestCases =>
        new object[]
        {
            new object[] { "", "" },
            new object[] { "Hello", "Hello" },
            new object[] { "Hello%World", "Hello%25World" },
            new object[] { "\"Test\"", "%22Test%22" },
        };

    [Theory]
    [MemberData(nameof(EncodeUriBasicTestCases))]
    public void EncodeUri_ShouldEncodeText(string input, string expected)
    {
        // arrange
        // act
        var actual = input.EncodeUri();

        // assert
        actual.Should().Be(expected);
    }

    public static IEnumerable EncodeUriCharactersThatShouldNotBeEncoded =>
        new object[]
        {
            new object[] { '+' },
            new object[] { ' ' },
            new object[] { '!' },
            new object[] { '#' },
            new object[] { '$' },
            new object[] { '&' },
            new object[] { '\'' },
            new object[] { '(' },
            new object[] { ')' },
            new object[] { '*' },
            new object[] { '+' },
            new object[] { ',' },
            new object[] { '/' },
            new object[] { ':' },
            new object[] { ';' },
            new object[] { '=' },
            new object[] { '?' },
            new object[] { '@' },
            new object[] { '~' }
        };

    [Theory]
    [MemberData(nameof(EncodeUriCharactersThatShouldNotBeEncoded))]
    public void EncodeUri_ShouldNotEncodeExpectedCharacters(char shouldNotBeEncoded)
    {
        // arrange
        var lorem = new Lorem();
        var input = shouldNotBeEncoded + string.Join(shouldNotBeEncoded, lorem.Words()) + shouldNotBeEncoded;
        var expected = input;

        // act
        var actual = input.EncodeUri();

        // assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void FirstCharOrDefault_ShouldReturnFirstCharacter_WhenInputIsNotEmpty()
    {
        // arrange
        var lorem = new Lorem();
        var input = lorem.Word();
        var expected = input[0];

        // act
        var actual = input.FirstCharOrDefault();

        // assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void FirstCharOrDefault_ShouldReturnNull_WhenInputIsEmpty()
    {
        // arrange
        // act
        var result = "".FirstCharOrDefault();

        // assert
        result.Should().BeNull();
    }

    [Fact]
    public void LastCharOrDefault_ShouldReturnLastCharacter_WhenInputIsNotEmpty()
    {
        // arrange
        var lorem = new Lorem();
        var input = lorem.Word();
        var expected = input[^1];

        // act
        var actual = input.LastCharOrDefault();

        // assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void LastCharOrDefault_ShouldReturnNull_WhenInputIsEmpty()
    {
        // arrange
        // act
        var result = "".LastCharOrDefault();

        // assert
        result.Should().BeNull();
    }
}
