using System;
using System.Collections.Generic;
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
        var diffs = new List<Diff> { new(operationToRemove, "a"), new(operationToRemain, "abc"), };

        var expected = new List<Diff> { new(Operation.Equal, "a"), new(operationToRemain, "bc"), };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
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
        var diffs = new List<Diff> { new(Operation.Equal, "a"), new(operation, "ba"), new(Operation.Equal, "c") };

        var expected = new List<Diff> { new(operation, "ab"), new(Operation.Equal, "ac") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
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
        var diffs = new List<Diff> { new(Operation.Equal, "c"), new(operation, "ab"), new(Operation.Equal, "a") };

        var expected = new List<Diff> { new(Operation.Equal, "ca"), new(operation, "ba") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
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
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "a"),
            new(operation, "b"),
            new(Operation.Equal, "c"),
            new(operation, "ac"),
            new(Operation.Equal, "x")
        };

        var expected = new List<Diff> { new(operation, "abc"), new(Operation.Equal, "acx") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
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
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "x"),
            new(operation, "ca"),
            new(Operation.Equal, "c"),
            new(operation, "b"),
            new(Operation.Equal, "a")
        };

        var expected = new List<Diff> { new(Operation.Equal, "xca"), new(operation, "cba") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Merge interweave
    [Fact]
    public void CleanupAndMerge_ShouldMergeOperationsTogether_WhenOperationsAreInterwoven()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "a"),
            new(Operation.Insert, "b"),
            new(Operation.Delete, "c"),
            new(Operation.Insert, "d"),
            new(Operation.Equal, "e"),
            new(Operation.Equal, "f")
        };

        var expected = new List<Diff>
        {
            new(Operation.Delete, "ac"),
            new(Operation.Insert, "bd"),
            new(Operation.Equal, "ef")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Merge deletions
    [Fact]
    public void CleanupAndMerge_ShouldMergeSequentialOperationsTogether_WhenOperationsAreDelete()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "a"),
            new(Operation.Delete, "b"),
            new(Operation.Delete, "c")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(1).And.Contain(new Diff(Operation.Delete, "abc"));
    }

    // diff_cleanupMerge: Merge equalities
    [Fact]
    public void CleanupAndMerge_ShouldMergeSequentialOperationsTogether_WhenOperationsAreEqual()
    {
        // arrange
        var diffs = new List<Diff> { new(Operation.Equal, "a"), new(Operation.Equal, "b"), new(Operation.Equal, "c") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(1).And.Contain(new Diff(Operation.Equal, "abc"));
    }

    // diff_cleanupMerge: Merge insertions
    [Fact]
    public void CleanupAndMerge_ShouldMergeSequentialOperationsTogether_WhenOperationsAreInsert()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Insert, "a"),
            new(Operation.Insert, "b"),
            new(Operation.Insert, "c")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(1).And.Contain(new Diff(Operation.Insert, "abc"));
    }

    // diff_cleanupMerge: No change case
    [Fact]
    public void CleanupAndMerge_ShouldNotChangeTheDiffs_WhenNoCleanupIsPossible()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "a"),
            new(Operation.Delete, "b"),
            new(Operation.Insert, "c")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(diffs.Count).And.ContainInOrder(diffs);
    }

    // diff_cleanupMerge: Prefix and suffix detection
    [Fact]
    public void CleanupAndMerge_ShouldOptimizeCommonPrefixesAndSuffixesForDeletionsAndInserts()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "a"),
            new(Operation.Insert, "abc"),
            new(Operation.Delete, "dc")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "a"),
            new(Operation.Delete, "d"),
            new(Operation.Insert, "b"),
            new(Operation.Equal, "c")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Prefix and suffix detection with equalities
    [Fact]
    public void CleanupAndMerge_ShouldOptimizeCommonPrefixesAndSuffixesForDeletionsAndInserts_WhenEqualitiesAreIncluded()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "x"),
            new(Operation.Delete, "a"),
            new(Operation.Insert, "abc"),
            new(Operation.Delete, "dc"),
            new(Operation.Equal, "y")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "xa"),
            new(Operation.Delete, "d"),
            new(Operation.Insert, "b"),
            new(Operation.Equal, "cy")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    [Fact]
    public void CleanupAndMerge_ShouldOptimizeCommonSuffixesForDeletionsAndInserts()
    {
        // arrange
        var diffs = new List<Diff> { new(Operation.Insert, "abc"), new(Operation.Delete, "dc") };

        var expected = new List<Diff>
        {
            new(Operation.Delete, "d"),
            new(Operation.Insert, "ab"),
            new(Operation.Equal, "c")
        };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Empty equality
    [Fact]
    public void CleanupAndMerge_ShouldRemoveEmptyEqualities()
    {
        // arrange
        var diffs = new List<Diff> { new(Operation.Equal, ""), new(Operation.Insert, "a"), };

        var expected = new List<Diff> { new(Operation.Insert, "a") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Empty merge
    [Fact]
    public void CleanupAndMerge_ShouldRemoveOperationsThatAreRedundant()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "b"),
            new(Operation.Insert, "ab"),
            new(Operation.Equal, "c")
        };

        var expected = new List<Diff> { new(Operation.Insert, "a"), new(Operation.Equal, "bc") };

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupMerge: Null case.
    [Fact]
    public void CleanupAndMerge_ShouldReturnEmptyList_WhenInputIsEmpty()
    {
        // arrange
        var diffs = new List<Diff>();

        // act
        diffs.CleanupAndMerge();

        // assert
        diffs.Should().BeEmpty();
    }

    [Fact]
    public void CleanupAndMerge_ShouldThrowArgumentOutOfRangeException_WhenADiffHasAnUnknownOperation()
    {
        // arrange
        var operation = (Operation)Enum.ToObject(typeof(Operation), -1);
        var diffs = new List<Diff> { new(operation, "abc") };

        // act
        var act = () => diffs.CleanupAndMerge();

        // assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage($"*invalid {nameof(Operation)}*")
            .And.ParamName.Should()
            .Be("diffs");
    }

    // diff_cleanupSemanticLossless: Sentence boundaries
    [Fact]
    public void CleanupSemanticLossless_ShouldAlignEqualitiesToSentenceBoundaries()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "The xxx. The "),
            new(Operation.Insert, "zzz. The "),
            new(Operation.Equal, "yyy.")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "The xxx."),
            new(Operation.Insert, " The zzz."),
            new(Operation.Equal, " The yyy.")
        };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    [Fact]
    public void CleanupSemanticLossless_ShouldNotModifyDiffs_WhenDiffPatternCannotBeOptimized()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Insert, "a"),
            new(Operation.Equal, "b"),
            new(Operation.Delete, "c")
        };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(diffs.Count).And.ContainInOrder(diffs);
    }

    [Fact]
    public void CleanupSemanticLossless_ShouldNotModifyDiffs_WhenDiffTextCannotBeOptimized()
    {
        // arrange
        var diffs = new List<Diff> { new(Operation.Equal, "a"), new(Operation.Insert, "b"), new(Operation.Equal, "c") };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(diffs.Count).And.ContainInOrder(diffs);
    }

    // diff_cleanupSemanticLossless: Hitting the start
    [Fact]
    public void CleanupSemanticLossless_ShouldRemoveRedundantEqualities_WhenLeadingEqualityCanBeMergedToTheRight()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "a"),
            new(Operation.Delete, "a"),
            new(Operation.Equal, "ax")
        };

        var expected = new List<Diff> { new(Operation.Delete, "a"), new(Operation.Equal, "aax") };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemanticLossless: Hitting the end
    [Fact]
    public void CleanupSemanticLossless_ShouldRemoveRedundantEqualities_WhenTrailingEqualityCanBeMergedToTheLeft()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "xa"),
            new(Operation.Delete, "a"),
            new(Operation.Equal, "a")
        };

        var expected = new List<Diff> { new(Operation.Equal, "xaa"), new(Operation.Delete, "a") };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemanticLossless: Null case
    [Fact]
    public void CleanupSemanticLossless_ShouldReturnEmptyList_WhenDiffsIsEmpty()
    {
        // arrange
        var diffs = new List<Diff>();

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().BeEmpty();
    }

    // diff_cleanupSemanticLossless: Alphanumeric boundaries
    [Fact]
    public void CleanupSemanticLossless_ShouldShiftSuffixesToTheRight_WhenDiffsCrossAlphanumericBoundaries()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "The-c"),
            new(Operation.Insert, "ow-and-the-c"),
            new(Operation.Equal, "at.")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "The-"),
            new(Operation.Insert, "cow-and-the-"),
            new(Operation.Equal, "cat.")
        };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemanticLossless: Blank lines
    [Fact]
    public void CleanupSemanticLossless_ShouldShiftSuffixesToTheRight_WhenDiffsCrossBlankLines()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "AAA\r\n\r\nBBB"),
            new(Operation.Insert, "\r\nDDD\r\n\r\nBBB"),
            new(Operation.Equal, "\r\nEEE")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "AAA\r\n\r\n"),
            new(Operation.Insert, "BBB\r\nDDD\r\n\r\n"),
            new(Operation.Equal, "BBB\r\nEEE")
        };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemanticLossless: Line boundaries
    [Fact]
    public void CleanupSemanticLossless_ShouldShiftSuffixesToTheRight_WhenDiffsCrossLineBoundaries()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "AAA\r\nBBB"),
            new(Operation.Insert, " DDD\r\nBBB"),
            new(Operation.Equal, " EEE")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "AAA\r\n"),
            new(Operation.Insert, "BBB DDD\r\n"),
            new(Operation.Equal, "BBB EEE")
        };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemanticLossless: Word boundaries
    [Fact]
    public void CleanupSemanticLossless_ShouldShiftSuffixesToTheRight_WhenDiffsCrossWordBoundaries()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Equal, "The c"),
            new(Operation.Insert, "ow and the c"),
            new(Operation.Equal, "at.")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "The "),
            new(Operation.Insert, "cow and the "),
            new(Operation.Equal, "cat.")
        };

        // act
        diffs.CleanupSemanticLossless();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }
}
