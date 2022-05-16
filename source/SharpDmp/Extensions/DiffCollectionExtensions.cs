using System.Diagnostics;
using System.Text;
using SharpDmp.Data;

namespace SharpDmp.Extensions;

public static class DiffCollectionExtensions
{
    /// <summary>
    ///     Reorder and merge together redundant edit sections.  Multiple equalities are merged together.  Any other
    ///     edit section can move as long as it doesn't cross an equality.
    /// </summary>
    /// <remarks>
    ///     Replaces <code>diff_cleanupMerge</code>.
    /// </remarks>
    /// <param name="diffs">
    ///     The diffs to be optimized.
    /// </param>
    /// <returns>
    ///     A new collection of diffs that have been optimized.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="diffs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     One of the items in <paramref name="diffs"/> contains an invalid <see cref="Operation"/> value.
    /// </exception>
    public static IReadOnlyList<Diff> CleanupAndMerge(this IEnumerable<Diff> diffs)
    {
        if (diffs is null)
        {
            throw new ArgumentNullException(nameof(diffs));
        }

        // Copy into a list that we can mutate.
        var resultDiffs = diffs.ToList();
        var dummyDiff = new Diff(Operation.Equal, string.Empty);

        while (true)
        {
            // Add a dummy equality at the end to trigger merging any trailing inserts and deletes.
            resultDiffs.Add(dummyDiff);

            var index = 0;
            var numberOfDeletions = 0;
            var textDeleted = new StringBuilder();
            var numberOfInsertions = 0;
            var textInserted = new StringBuilder();

            while (index < resultDiffs.Count)
            {
                var currentDiff = resultDiffs[index];
                switch (currentDiff.Operation)
                {
                    case Operation.Delete:
                        numberOfDeletions += 1;
                        textDeleted.Append(currentDiff.Text);
                        index += 1;
                        break;

                    case Operation.Insert:
                        numberOfInsertions += 1;
                        textInserted.Append(currentDiff.Text);
                        index += 1;
                        break;

                    case Operation.Equal:
                        if (numberOfDeletions + numberOfInsertions > 1)
                        {
                            index = MergeInsertAndDeleteCommonalities(
                                resultDiffs,
                                index,
                                numberOfDeletions,
                                textDeleted,
                                numberOfInsertions,
                                textInserted
                            );
                        }
                        // Merge back to back equalities together.
                        else if (index is not 0 && resultDiffs[index - 1].Operation is Operation.Equal)
                        {
                            var previousDiff = resultDiffs[index - 1];
                            resultDiffs[index - 1] = previousDiff with { Text = previousDiff.Text + currentDiff.Text };
                            resultDiffs.RemoveAt(index);
                        }
                        // Remove empty equalities, but ignore the dummy diff at the end.
                        else if (index < resultDiffs.Count - 1 && currentDiff.Text.Length == 0)
                        {
                            resultDiffs.RemoveAt(index);
                        }
                        else
                        {
                            index += 1;
                        }

                        numberOfDeletions = 0;
                        textDeleted.Clear();
                        numberOfInsertions = 0;
                        textInserted.Clear();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(diffs),
                            currentDiff.Operation,
                            $"{nameof(Diff)} contains an invalid {nameof(Operation)} value."
                        );
                }
            }

            // Remove the dummy entry, if it's still there.
            if (resultDiffs[^1] is { Operation: Operation.Equal, Text: "" })
            {
                resultDiffs.RemoveAt(resultDiffs.Count - 1);
            }

            // If any shifts were made we need another pass at reordering.  If not, we're done.
            if (!OptimizeSingleEdits(resultDiffs))
            {
                return resultDiffs.AsReadOnly();
            }
        }
    }

    /// <summary>
    ///     When the current diff is an equality preceeded by insertions or deletions, see if we can merge together
    ///     any common prefixes or suffixes from the previous diffs.
    /// </summary>
    /// <param name="diffs"></param>
    /// <param name="index"></param>
    /// <param name="numberOfDeletions"></param>
    /// <param name="textDeleted"></param>
    /// <param name="numberOfInsertions"></param>
    /// <param name="textInserted"></param>
    /// <returns></returns>
    private static int MergeInsertAndDeleteCommonalities(
        List<Diff> diffs,
        int index,
        int numberOfDeletions,
        StringBuilder textDeleted,
        int numberOfInsertions,
        StringBuilder textInserted
    )
    {
        Debug.Assert(diffs[index].Operation == Operation.Equal, "diffs[index].Operation == Operation.Equal");

        // Cache each string as they're allocated off of the StringBuilders so that we can avoid duplicate
        // allocations unless the StringBuilder contents were actually changed.
        var deletedBeforePrefix = textDeleted.ToString();
        var insertedBeforePrefix = textInserted.ToString();
        var prefixLength = 0;
        var deletedBeforeSuffix = string.Empty;
        var insertedBeforeSuffix = string.Empty;
        var suffixLength = 0;

        if (numberOfDeletions is not 0 && numberOfInsertions is not 0)
        {
            // Factor out any common prefixes.
            prefixLength = insertedBeforePrefix.AsSpan().FindCommonPrefix(deletedBeforePrefix);
            if (prefixLength is not 0)
            {
                var commonPrefix = insertedBeforePrefix[..prefixLength];
                var candidateEqualityIndex = Math.Max(0, index - numberOfDeletions - numberOfInsertions - 1);
                if (diffs[candidateEqualityIndex].Operation is Operation.Equal)
                {
                    var diffToUpdate = diffs[candidateEqualityIndex];
                    diffs[candidateEqualityIndex] = diffToUpdate with { Text = diffToUpdate.Text + commonPrefix };
                }
                else
                {
                    diffs.Insert(0, new Diff(Operation.Equal, commonPrefix));
                    index += 1;
                }

                textDeleted.Remove(0, prefixLength);
                textInserted.Remove(0, prefixLength);
            }

            // Factor out any common suffixes.
            deletedBeforeSuffix = prefixLength is 0 ? deletedBeforePrefix : textDeleted.ToString();
            insertedBeforeSuffix = prefixLength is 0 ? insertedBeforePrefix : textInserted.ToString();
            suffixLength = insertedBeforeSuffix.AsSpan().FindCommonSuffix(deletedBeforeSuffix);
            if (suffixLength is not 0)
            {
                var commonSuffix = insertedBeforeSuffix[(textInserted.Length - suffixLength)..];
                diffs[index] = diffs[index] with { Text = commonSuffix + diffs[index].Text };

                textDeleted.Remove(textDeleted.Length - suffixLength, suffixLength);
                textInserted.Remove(textInserted.Length - suffixLength, suffixLength);
            }
        }

        // Remove the diffs we've been merging, and reinsert any remaining deletes or inserts
        index -= numberOfDeletions + numberOfInsertions;
        diffs.RemoveRange(index, numberOfDeletions + numberOfInsertions);

        if (textDeleted.Length is not 0)
        {
            var newDeleted = (prefixLength, suffixLength) switch
            {
                (0, 0) => deletedBeforePrefix,
                (> 0, 0) => deletedBeforeSuffix,
                _ => textDeleted.ToString()
            };
            diffs.Insert(index, new Diff(Operation.Delete, newDeleted));
            index += 1;
        }

        if (textInserted.Length is not 0)
        {
            var newInserted = (prefixLength, suffixLength) switch
            {
                (0, 0) => insertedBeforePrefix,
                (> 0, 0) => insertedBeforeSuffix,
                _ => textInserted.ToString()
            };
            diffs.Insert(index, new Diff(Operation.Insert, newInserted));
            index += 1;
        }

        return index + 1;
    }

    /// <summary>
    ///     Look for single edits surrounded on both sides by equalities which can be shifted left or right to
    ///     eliminate one of the equalities, e.g. A<ins>BA</ins>C -> <ins>AB</ins>AC
    /// </summary>
    /// <param name="diffs"></param>
    /// <returns>
    ///     True if any changes were made to <paramref name="diffs"/>.
    /// </returns>
    private static bool OptimizeSingleEdits(IList<Diff> diffs)
    {
        var changes = false;

        // Intentionally ignore the first and last element (they don't qualifity).
        var index = 1;
        while (index < diffs.Count - 1)
        {
            var previousDiff = diffs[index - 1];
            var currentDiff = diffs[index];
            var nextDiff = diffs[index + 1];

            if (previousDiff.Operation is Operation.Equal && nextDiff.Operation is Operation.Equal)
            {
                if (currentDiff.Text.EndsWith(previousDiff.Text, StringComparison.Ordinal))
                {
                    diffs[index] = currentDiff with
                    {
                        Text = previousDiff.Text + currentDiff.Text[..^previousDiff.Text.Length]
                    };
                    diffs[index + 1] = nextDiff with { Text = previousDiff.Text + nextDiff.Text };
                    diffs.RemoveAt(index - 1);
                    changes = true;
                }
                else if (currentDiff.Text.StartsWith(nextDiff.Text, StringComparison.Ordinal))
                {
                    diffs[index - 1] = previousDiff with { Text = previousDiff.Text + nextDiff.Text };
                    diffs[index] = currentDiff with { Text = currentDiff.Text[nextDiff.Text.Length..] + nextDiff.Text };
                    diffs.RemoveAt(index + 1);
                    changes = true;
                }
            }

            index += 1;
        }

        return changes;
    }
}
