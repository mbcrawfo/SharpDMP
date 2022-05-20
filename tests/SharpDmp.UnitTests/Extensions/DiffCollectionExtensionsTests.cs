using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public static IEnumerable DiffsThatCanNotBeOptimizedTestCases =>
        new object[]
        {
            new object[]
            {
                new List<Diff>
                {
                    new Diff(Operation.Delete, "ab"),
                    new Diff(Operation.Insert, "cd"),
                    new Diff(Operation.Equal, "12"),
                    new Diff(Operation.Delete, "e"),
                }
            },
            new object[]
            {
                new List<Diff>
                {
                    new Diff(Operation.Delete, "abc"),
                    new Diff(Operation.Insert, "ABC"),
                    new Diff(Operation.Equal, "1234"),
                    new Diff(Operation.Delete, "wxyz"),
                }
            }
        };

    // diff_cleanupSemantic: No elimination #1
    // diff_cleanupSemantic: No elimination #2
    [Theory]
    [MemberData(nameof(DiffsThatCanNotBeOptimizedTestCases))]
    public void CleanupSemantic_ShouldNotModifyInput_WhenDiffsCanNotBeOptimized(List<Diff> diffs)
    {
        // arrange
        var expected = diffs.ToList();

        // act
        diffs.CleanupSemantic();

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

    // diff_cleanupSemantic: Simple elimination
    [Fact]
    public void CleanupSemantic_ShouldMergeEqualityIntoSurroundingEdits_WhenEqualityIsShorterThanSurroundingEdits()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new Diff(Operation.Delete, "a"),
            new Diff(Operation.Equal, "b"),
            new Diff(Operation.Delete, "c")
        };

        var expected = new List<Diff> { new Diff(Operation.Delete, "abc"), new Diff(Operation.Insert, "b") };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: Backpass elimination
    [Fact]
    public void CleanupSemantic_ShouldMergeEqualityIntoSurroundingEdits_WhenFullOptimizationRequiresBacktrackingAfterAPreviousOptimization()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new Diff(Operation.Delete, "ab"),
            new Diff(Operation.Equal, "cd"),
            new Diff(Operation.Delete, "e"),
            new Diff(Operation.Equal, "f"),
            new Diff(Operation.Insert, "g")
        };

        var expected = new List<Diff> { new Diff(Operation.Delete, "abcdef"), new Diff(Operation.Insert, "cdfg") };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: Multiple elimination
    [Fact]
    public void CleanupSemantic_ShouldMergeEqualityIntoSurroundingEdits_WhenFullOptimizationRequiresBacktrackingMultipleTimes()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new Diff(Operation.Insert, "1"),
            new Diff(Operation.Equal, "A"),
            new Diff(Operation.Delete, "B"),
            new Diff(Operation.Insert, "2"),
            new Diff(Operation.Equal, "_"),
            new Diff(Operation.Insert, "1"),
            new Diff(Operation.Equal, "A"),
            new Diff(Operation.Delete, "B"),
            new Diff(Operation.Insert, "2")
        };

        var expected = new List<Diff> { new Diff(Operation.Delete, "AB_AB"), new Diff(Operation.Insert, "1A2_1A2") };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: No overlap elimination
    [Fact]
    public void CleanupSemantic_ShouldNotModifyInput_WhenDeletionsAndInsertionsHaveShortOverlap()
    {
        // arrange
        var diffs = new List<Diff> { new Diff(Operation.Delete, "abcxx"), new Diff(Operation.Insert, "xxdef") };

        var expected = new List<Diff> { new Diff(Operation.Delete, "abcxx"), new Diff(Operation.Insert, "xxdef") };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: Null case
    [Fact]
    public void CleanupSemantic_ShouldNotModifyInput_WhenInputIsEmpty()
    {
        // arrange
        var diffs = new List<Diff>();

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().BeEmpty();
    }

    // diff_cleanupSemantic: Word boundaries
    [Fact]
    public void CleanupSemantic_ShouldOptimizeDiffsAlongWordBoundaries()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new Diff(Operation.Equal, "The c"),
            new Diff(Operation.Delete, "ow and the c"),
            new Diff(Operation.Equal, "at.")
        };

        var expected = new List<Diff>
        {
            new Diff(Operation.Equal, "The "),
            new Diff(Operation.Delete, "cow and the "),
            new Diff(Operation.Equal, "cat.")
        };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: Reverse overlap elimination
    [Fact]
    public void CleanupSemantic_ShouldShiftOverlappingEditsIntoAnEquality_WhenDeletionPrefixOverlapsInsertSuffix()
    {
        // arrange
        var diffs = new List<Diff> { new Diff(Operation.Delete, "xxxabc"), new Diff(Operation.Insert, "defxxx") };

        var expected = new List<Diff>
        {
            new Diff(Operation.Insert, "def"),
            new Diff(Operation.Equal, "xxx"),
            new Diff(Operation.Delete, "abc"),
        };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: Overlap elimination
    [Fact]
    public void CleanupSemantic_ShouldShiftOverlappingEditsIntoAnEquality_WhenDeletionSuffixOverlapsInsertPrefix()
    {
        // arrange
        var diffs = new List<Diff> { new Diff(Operation.Delete, "abcxxx"), new Diff(Operation.Insert, "xxxdef") };

        var expected = new List<Diff>
        {
            new Diff(Operation.Delete, "abc"),
            new Diff(Operation.Equal, "xxx"),
            new Diff(Operation.Insert, "def")
        };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupSemantic: Two overlap eliminations
    [Fact]
    public void CleanupSemantic_ShouldShiftOverlappingEditsIntoAnEquality_WhenMultipleEditsCanBeOptimized()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new Diff(Operation.Delete, "abcd1212"),
            new Diff(Operation.Insert, "1212efghi"),
            new Diff(Operation.Equal, "----"),
            new Diff(Operation.Delete, "A3"),
            new Diff(Operation.Insert, "3BC")
        };

        var expected = new List<Diff>
        {
            new Diff(Operation.Delete, "abcd"),
            new Diff(Operation.Equal, "1212"),
            new Diff(Operation.Insert, "efghi"),
            new Diff(Operation.Equal, "----"),
            new Diff(Operation.Delete, "A"),
            new Diff(Operation.Equal, "3"),
            new Diff(Operation.Insert, "BC")
        };

        // act
        diffs.CleanupSemantic();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
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
