using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using SharpDmp.Data;
using Xunit;

namespace SharpDmp.UnitTests.Data;

public class HashedDiffTests
{
    // diff_charsToLines: Shared lines
    [Fact]
    public void CollectionOfHashedDiffs_ShouldBeRehydrated()
    {
        // arrange
        var uniqueLines = new[] { "", "alpha\n", "beta\n" };

        var hashedDiffs = new[]
        {
            new HashedDiff(Operation.Equal, new[] { 1, 2, 1 }),
            new HashedDiff(Operation.Insert, new[] { 2, 1, 2 }),
        };

        var expected = new[]
        {
            new Diff(Operation.Equal, "alpha\nbeta\nalpha\n"),
            new Diff(Operation.Insert, "beta\nalpha\nbeta\n"),
        };

        // act
        var actual = hashedDiffs.Select(d => d.Rehydrate(uniqueLines)).ToList();

        // assert
        actual.Should().ContainInOrder(expected);
    }

    // diff_charsToLines: More than 256
    [Fact]
    public void CollectionOfHashedDiffs_ShouldBeRehydrated_WhenMoreThan256UniqueStrings()
    {
        // arrange
        const int numberOfLines = 300;
        var hashedText = new List<int>(numberOfLines);
        var sb = new StringBuilder(numberOfLines * 2);
        var uniqueLines = new List<string>(numberOfLines + 2) { "" };
        foreach (var i in Enumerable.Range(1, numberOfLines + 1))
        {
            hashedText.Add(i);

            var s = i + "\n";
            sb.Append(s);
            uniqueLines.Add(s);
        }

        var hashedDiffs = new[] { new HashedDiff(Operation.Equal, hashedText), };
        var expected = new[] { new Diff(Operation.Equal, sb.ToString()), };

        // act
        var actual = hashedDiffs.Select(d => d.Rehydrate(uniqueLines)).ToList();

        // assert
        actual.Should().ContainInOrder(expected);
    }

    // diff_charsToLines: More than 65536
    [Fact]
    public void CollectionOfHashedDiffs_ShouldBeRehydrated_WhenMoreThan65536UniqueStrings()
    {
        // arrange
        const int numberOfLines = 66000;
        var hashedText = new List<int>(numberOfLines);
        var sb = new StringBuilder(numberOfLines * 2);
        var uniqueLines = new List<string>(numberOfLines + 2) { "" };
        foreach (var i in Enumerable.Range(1, numberOfLines + 1))
        {
            hashedText.Add(i);

            var s = i + "\n";
            sb.Append(s);
            uniqueLines.Add(s);
        }

        var hashedDiffs = new[] { new HashedDiff(Operation.Equal, hashedText), };
        var expected = new[] { new Diff(Operation.Equal, sb.ToString()), };

        // act
        var actual = hashedDiffs.Select(d => d.Rehydrate(uniqueLines)).ToList();

        // assert
        actual.Should().ContainInOrder(expected);
    }

    [Fact]
    public void Rehydrate_ShouldReturnDiffWithOriginalTextRestored()
    {
        // arrange
        var uniqueLines = new[] { "", "a\n", "b\n", "c" };
        var hashedText = new[] { 1, 2, 1, 2, 3 };
        var expected = new Diff(Operation.Delete, "a\nb\na\nb\nc");
        var sut = new HashedDiff(Operation.Delete, hashedText);

        // act
        var actual = sut.Rehydrate(uniqueLines);

        // assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void Rehydrate_ShouldThrowArgumentNullException_WhenUniqueLinesIsNull()
    {
        // arrange
        var sut = new HashedDiff(Operation.Delete, Array.Empty<int>());

        // act
        Action act = () => _ = sut.Rehydrate(null!);

        // assert
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("uniqueLines");
    }
}
