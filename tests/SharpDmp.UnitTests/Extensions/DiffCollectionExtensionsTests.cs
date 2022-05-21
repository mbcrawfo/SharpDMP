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
                    new(Operation.Delete, "ab"),
                    new(Operation.Insert, "cd"),
                    new(Operation.Equal, "12"),
                    new(Operation.Delete, "e"),
                }
            },
            new object[]
            {
                new List<Diff>
                {
                    new(Operation.Delete, "abc"),
                    new(Operation.Insert, "ABC"),
                    new(Operation.Equal, "1234"),
                    new(Operation.Delete, "wxyz"),
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

    // diff_cleanupEfficiency: Four-edit elimination
    [Fact]
    public void CleanupEfficiency_ShouldMergeEqualityIntoSurroundingEdits_WhenEqualityIsSurroundedByFourEdits()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "ab"),
            new(Operation.Insert, "12"),
            new(Operation.Equal, "xyz"),
            new(Operation.Delete, "cd"),
            new(Operation.Insert, "34")
        };

        var expected = new List<Diff> { new(Operation.Delete, "abxyzcd"), new(Operation.Insert, "12xyz34"), };

        // act
        diffs.CleanupEfficiency();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupEfficiency: Three-edit elimination
    [Fact]
    public void CleanupEfficiency_ShouldMergeEqualityIntoSurroundingEdits_WhenEqualityIsSurroundedByThreeEdits()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Insert, "12"),
            new(Operation.Equal, "x"),
            new(Operation.Delete, "cd"),
            new(Operation.Insert, "34")
        };

        var expected = new List<Diff> { new(Operation.Delete, "xcd"), new(Operation.Insert, "12x34"), };

        // act
        diffs.CleanupEfficiency();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupEfficiency: Backpass elimination
    [Fact]
    public void CleanupEfficiency_ShouldMergeEqualityIntoSurroundingEdits_WhenFullOptimizationRequiresMultiplePasses()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "ab"),
            new(Operation.Insert, "12"),
            new(Operation.Equal, "xy"),
            new(Operation.Insert, "34"),
            new(Operation.Equal, "z"),
            new(Operation.Delete, "cd"),
            new(Operation.Insert, "56")
        };

        var expected = new List<Diff> { new(Operation.Delete, "abxyzcd"), new(Operation.Insert, "12xy34z56"), };

        // act
        diffs.CleanupEfficiency();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupEfficiency: High cost elimination
    [Fact]
    public void CleanupEfficiency_ShouldMergeEqualityIntoSurroundingEdits_WhenUsingAHigherEditCost()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "ab"),
            new(Operation.Insert, "12"),
            new(Operation.Equal, "wxyz"),
            new(Operation.Delete, "cd"),
            new(Operation.Insert, "34")
        };

        var expected = new List<Diff> { new(Operation.Delete, "abwxyzcd"), new(Operation.Insert, "12wxyz34"), };

        // act
        diffs.CleanupEfficiency(editCost: 5);

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupEfficiency: No elimination
    [Fact]
    public void CleanupEfficiency_ShouldNotModifyInput_WhenInputCanNotBeOptimized()
    {
        // arrange
        var diffs = new List<Diff>
        {
            new(Operation.Delete, "ab"),
            new(Operation.Insert, "12"),
            new(Operation.Equal, "wxyz"),
            new(Operation.Delete, "cd"),
            new(Operation.Insert, "34")
        };

        var expected = diffs.ToList();

        // act
        diffs.CleanupEfficiency();

        // assert
        diffs.Should().HaveCount(expected.Count).And.ContainInOrder(expected);
    }

    // diff_cleanupEfficiency: Null case
    [Fact]
    public void CleanupEfficiency_ShouldNotModifyInput_WhenInputIsEmpty()
    {
        // arrange
        var diffs = new List<Diff>();

        // act
        diffs.CleanupEfficiency();

        // assert
        diffs.Should().BeEmpty();
    }

    [Fact]
    public void CleanupEfficiency_ShouldThrowArgumentOutOfRangeException_WhenADiffHasAnUnknownOperation()
    {
        // arrange
        var operation = (Operation)Enum.ToObject(typeof(Operation), -1);
        var diffs = new List<Diff> { new(operation, "abc") };

        // act
        var act = () => diffs.CleanupEfficiency();

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
            new(Operation.Delete, "a"),
            new(Operation.Equal, "b"),
            new(Operation.Delete, "c")
        };

        var expected = new List<Diff> { new(Operation.Delete, "abc"), new(Operation.Insert, "b") };

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
            new(Operation.Delete, "ab"),
            new(Operation.Equal, "cd"),
            new(Operation.Delete, "e"),
            new(Operation.Equal, "f"),
            new(Operation.Insert, "g")
        };

        var expected = new List<Diff> { new(Operation.Delete, "abcdef"), new(Operation.Insert, "cdfg") };

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
            new(Operation.Insert, "1"),
            new(Operation.Equal, "A"),
            new(Operation.Delete, "B"),
            new(Operation.Insert, "2"),
            new(Operation.Equal, "_"),
            new(Operation.Insert, "1"),
            new(Operation.Equal, "A"),
            new(Operation.Delete, "B"),
            new(Operation.Insert, "2")
        };

        var expected = new List<Diff> { new(Operation.Delete, "AB_AB"), new(Operation.Insert, "1A2_1A2") };

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
        var diffs = new List<Diff> { new(Operation.Delete, "abcxx"), new(Operation.Insert, "xxdef") };

        var expected = new List<Diff> { new(Operation.Delete, "abcxx"), new(Operation.Insert, "xxdef") };

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
            new(Operation.Equal, "The c"),
            new(Operation.Delete, "ow and the c"),
            new(Operation.Equal, "at.")
        };

        var expected = new List<Diff>
        {
            new(Operation.Equal, "The "),
            new(Operation.Delete, "cow and the "),
            new(Operation.Equal, "cat.")
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
        var diffs = new List<Diff> { new(Operation.Delete, "xxxabc"), new(Operation.Insert, "defxxx") };

        var expected = new List<Diff>
        {
            new(Operation.Insert, "def"),
            new(Operation.Equal, "xxx"),
            new(Operation.Delete, "abc"),
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
        var diffs = new List<Diff> { new(Operation.Delete, "abcxxx"), new(Operation.Insert, "xxxdef") };

        var expected = new List<Diff>
        {
            new(Operation.Delete, "abc"),
            new(Operation.Equal, "xxx"),
            new(Operation.Insert, "def")
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
            new(Operation.Delete, "abcd1212"),
            new(Operation.Insert, "1212efghi"),
            new(Operation.Equal, "----"),
            new(Operation.Delete, "A3"),
            new(Operation.Insert, "3BC")
        };

        var expected = new List<Diff>
        {
            new(Operation.Delete, "abcd"),
            new(Operation.Equal, "1212"),
            new(Operation.Insert, "efghi"),
            new(Operation.Equal, "----"),
            new(Operation.Delete, "A"),
            new(Operation.Equal, "3"),
            new(Operation.Insert, "BC")
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

    [Fact]
    public void GetDestinationText_ShouldReturnTheDestinationText()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "jump"),
            new Diff(Operation.Delete, "s"),
            new Diff(Operation.Insert, "ed"),
            new Diff(Operation.Equal, " over "),
            new Diff(Operation.Delete, "the"),
            new Diff(Operation.Insert, "a"),
            new Diff(Operation.Equal, " lazy")
        };

        const string expected = "jumped over a lazy";

        // act
        var actual = diffs.GetDestinationText();

        // assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void GetDestinationText_ShouldThrowArgumentNullException_WhenDiffsIsNull()
    {
        // arrange
        // act
        var act = () => DiffCollectionExtensions.GetDestinationText(null!);

        // assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("diffs");
    }

    [Fact]
    public void GetSourceText_ShouldReturnTheSourceText()
    {
        // arrange
        var diffs = new[]
        {
            new Diff(Operation.Equal, "jump"),
            new Diff(Operation.Delete, "s"),
            new Diff(Operation.Insert, "ed"),
            new Diff(Operation.Equal, " over "),
            new Diff(Operation.Delete, "the"),
            new Diff(Operation.Insert, "a"),
            new Diff(Operation.Equal, " lazy")
        };

        const string expected = "jumps over the lazy";

        // act
        var actual = diffs.GetSourceText();

        // assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void GetSourceText_ShouldThrowArgumentNullException_WhenDiffsIsNull()
    {
        // arrange
        // act
        var act = () => DiffCollectionExtensions.GetSourceText(null!);

        // assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("diffs");
    }
}
