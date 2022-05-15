using System;
using FluentAssertions;
using SharpDmp.Data;
using SharpDmp.Extensions;
using Xunit;

namespace SharpDmp.UnitTests.Extensions;

public class DiffCollectionExtensionsTests
{
    [Theory]
    [InlineData(Operation.Delete, Operation.Insert)]
    [InlineData(Operation.Insert, Operation.Delete)]
    public void CleanupAndMerge_ShouldOptimizeCommonPrefixesForDeletionsAndInserts(
        Operation operationToRemove,
        Operation operationToRemain
    )
    {
        // arrange
        var diffs = new[] { new Diff(operationToRemove, "a"), new Diff(operationToRemain, "abc"), };

        var expected = new[] { new Diff(Operation.Equal, "a"), new Diff(operationToRemain, "bc"), };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Slide edit left
    [Theory]
    [InlineData(Operation.Delete)]
    [InlineData(Operation.Insert)]
    public void CleanupAndMerge_ShouldMergeEqualities_WhenSeparatedByAnOperationThatEndsWithTheFirstEquality(
        Operation operation
    )
    {
        // arrange
        var diffs = new[] { new Diff(Operation.Equal, "a"), new Diff(operation, "ba"), new Diff(Operation.Equal, "c") };

        var expected = new[] { new Diff(operation, "ab"), new Diff(Operation.Equal, "ac") };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Slide edit right
    [Theory]
    [InlineData(Operation.Delete)]
    [InlineData(Operation.Insert)]
    public void CleanupAndMerge_ShouldMergeEqualities_WhenSeparatedByAnOperationThatStartsWithTheTrailingEquality(
        Operation operation
    )
    {
        // arrange
        var diffs = new[] { new Diff(Operation.Equal, "c"), new Diff(operation, "ab"), new Diff(Operation.Equal, "a") };

        var expected = new[] { new Diff(Operation.Equal, "ca"), new Diff(operation, "ba") };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Slide edit left recursive
    [Theory]
    [InlineData(Operation.Delete)]
    [InlineData(Operation.Insert)]
    public void CleanupAndMerge_ShouldMergeEqualitiesAfterAnOperation_WhenMultiplePassesAreRequiredToFullyOptimize(
        Operation operation
    )
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "a"),
            new Diff(operation, "b"),
            new Diff(Operation.Equal, "c"),
            new Diff(operation, "ac"),
            new Diff(Operation.Equal, "x")
        };

        var expected = new[] { new Diff(operation, "abc"), new Diff(Operation.Equal, "acx") };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Slide edit right recursive
    [Theory]
    [InlineData(Operation.Delete)]
    [InlineData(Operation.Insert)]
    public void CleanupAndMerge_ShouldMergeEqualitiesBeforeAnOperation_WhenMultiplePassesAreRequiredToFullyOptimize(
        Operation operation
    )
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "x"),
            new Diff(operation, "ca"),
            new Diff(Operation.Equal, "c"),
            new Diff(operation, "b"),
            new Diff(Operation.Equal, "a")
        };

        var expected = new[] { new Diff(Operation.Equal, "xca"), new Diff(operation, "cba") };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Merge interweave
    [Fact]
    public void CleanupAndMerge_ShouldMergeOperationsTogether_WhenOperationsAreInterwoven()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Delete, "a"),
            new Diff(Operation.Insert, "b"),
            new Diff(Operation.Delete, "c"),
            new Diff(Operation.Insert, "d"),
            new Diff(Operation.Equal, "e"),
            new Diff(Operation.Equal, "f")
        };

        var expected = new[]
        {
            new Diff(Operation.Delete, "ac"),
            new Diff(Operation.Insert, "bd"),
            new Diff(Operation.Equal, "ef")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Merge deletions
    [Fact]
    public void CleanupAndMerge_ShouldMergeSequentialOperationsTogether_WhenOperationsAreDelete()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Delete, "a"),
            new Diff(Operation.Delete, "b"),
            new Diff(Operation.Delete, "c")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(1).And.Contain(new Diff(Operation.Delete, "abc"));
    }

    // diff_cleanupMerge: Merge equalities
    [Fact]
    public void CleanupAndMerge_ShouldMergeSequentialOperationsTogether_WhenOperationsAreEqual()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "a"),
            new Diff(Operation.Equal, "b"),
            new Diff(Operation.Equal, "c")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(1).And.Contain(new Diff(Operation.Equal, "abc"));
    }

    // diff_cleanupMerge: Merge insertions
    [Fact]
    public void CleanupAndMerge_ShouldMergeSequentialOperationsTogether_WhenOperationsAreInsert()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Insert, "a"),
            new Diff(Operation.Insert, "b"),
            new Diff(Operation.Insert, "c")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(1).And.Contain(new Diff(Operation.Insert, "abc"));
    }

    // diff_cleanupMerge: No change case
    [Fact]
    public void CleanupAndMerge_ShouldNotChangeTheDiffs_WhenNoCleanupIsPossible()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "a"),
            new Diff(Operation.Delete, "b"),
            new Diff(Operation.Insert, "c")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(diffs.Length).And.ContainInOrder(diffs);
    }

    // diff_cleanupMerge: Prefix and suffix detection
    [Fact]
    public void CleanupAndMerge_ShouldOptimizeCommonPrefixesAndSuffixesForDeletionsAndInserts()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Delete, "a"),
            new Diff(Operation.Insert, "abc"),
            new Diff(Operation.Delete, "dc")
        };

        var expected = new[]
        {
            new Diff(Operation.Equal, "a"),
            new Diff(Operation.Delete, "d"),
            new Diff(Operation.Insert, "b"),
            new Diff(Operation.Equal, "c")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Prefix and suffix detection with equalities
    [Fact]
    public void CleanupAndMerge_ShouldOptimizeCommonPrefixesAndSuffixesForDeletionsAndInserts_WhenEqualitiesAreIncluded()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "x"),
            new Diff(Operation.Delete, "a"),
            new Diff(Operation.Insert, "abc"),
            new Diff(Operation.Delete, "dc"),
            new Diff(Operation.Equal, "y")
        };

        var expected = new[]
        {
            new Diff(Operation.Equal, "xa"),
            new Diff(Operation.Delete, "d"),
            new Diff(Operation.Insert, "b"),
            new Diff(Operation.Equal, "cy")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    [Fact]
    public void CleanupAndMerge_ShouldOptimizeCommonSuffixesForDeletionsAndInserts()
    {
        // arrange
        var diffs = new[] { new Diff(Operation.Insert, "abc"), new Diff(Operation.Delete, "dc") };

        var expected = new[]
        {
            new Diff(Operation.Delete, "d"),
            new Diff(Operation.Insert, "ab"),
            new Diff(Operation.Equal, "c")
        };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Empty equality
    [Fact]
    public void CleanupAndMerge_ShouldRemoveEmptyEqualities()
    {
        // arrange
        var diffs = new[] { new Diff(Operation.Equal, ""), new Diff(Operation.Insert, "a"), };

        var expected = new[] { new Diff(Operation.Insert, "a") };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Empty merge
    [Fact]
    public void CleanupAndMerge_ShouldRemoveOperationsThatAreRedundant()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Delete, "b"),
            new Diff(Operation.Insert, "ab"),
            new Diff(Operation.Equal, "c")
        };

        var expected = new[] { new Diff(Operation.Insert, "a"), new Diff(Operation.Equal, "bc") };

        // act
        var result = diffs.CleanupAndMerge();

        // assert
        result.Should().HaveCount(expected.Length).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Null case.
    [Fact]
    public void CleanupAndMerge_ShouldReturnEmptyList_WhenInputIsEmpty()
    {
        // arrange
        // act
        var result = Array.Empty<Diff>().CleanupAndMerge();

        // assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CleanupAndMerge_ShouldThrowArgumentNullException_WhenDiffsIsNull()
    {
        // arrange
        // act
        Action act = () => _ = DiffCollectionExtensions.CleanupAndMerge(null!);

        // assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("diffs");
    }

    [Fact]
    public void CleanupAndMerge_ShouldThrowArgumentOutOfRangeException_WhenADiffHasAnUnknownOperation()
    {
        // arrange
        var operation = (Operation)Enum.ToObject(typeof(Operation), -1);
        var diffs = new[] { new Diff(operation, "abc") };

        // act
        Action act = () => _ = diffs.CleanupAndMerge();

        // assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage($"*invalid {nameof(Operation)}*")
            .And.ParamName.Should()
            .Be("diffs");
    }
}
