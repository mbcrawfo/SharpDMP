using FluentAssertions;
using SharpDmp.Data;
using Xunit;

namespace SharpDmp.UnitTests.Data;

public class DiffTests
{
    [Fact]
    public void ToString_ShouldFormatDataMembers()
    {
        // arrange
        var sut = new Diff(Operation.Delete, "abc");

        // act
        var result = sut.ToString();

        // assert
        result
            .Should()
            .Contain($"{nameof(Diff.Operation)} = {sut.Operation.ToString()}")
            .And.Subject.Should()
            .Contain($"{nameof(Diff.Text)} = {sut.Text}");
    }

    [Fact]
    public void ToString_ShouldReplaceTextNewLines_WhenTextContainsNewLines()
    {
        // arrange
        var sut = new Diff(Operation.Delete, "abc\ndef\nghi");
        var expectedText = sut.Text.Replace('\n', '\u00b6');

        // act
        var result = sut.ToString();

        // assert
        result.Should().Contain($"{nameof(Diff.Text)} = {expectedText}");
    }
}
