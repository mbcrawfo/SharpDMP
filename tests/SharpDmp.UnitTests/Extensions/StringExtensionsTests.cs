using Bogus.DataSets;
using FluentAssertions;
using SharpDmp.Extensions;
using Xunit;

namespace SharpDmp.UnitTests.Extensions;

public class StringExtensionsTests
{
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
