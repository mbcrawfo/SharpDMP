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
    /// <exception cref="ArgumentOutOfRangeException">
    ///     One of the items in <paramref name="diffs" /> contains an invalid <see cref="Operation" /> value.
    /// </exception>
    internal static void CleanupAndMerge(this List<Diff> diffs)
    {
        Debug.Assert(diffs is not null, "diffs is not null");

        var dummyDiff = new Diff(Operation.Equal, string.Empty);

        while (true)
        {
            // Add a dummy equality at the end to trigger merging any trailing inserts and deletes.
            diffs.Add(dummyDiff);

            var index = 0;
            var numberOfDeletions = 0;
            var textDeleted = new StringBuilder();
            var numberOfInsertions = 0;
            var textInserted = new StringBuilder();

            while (index < diffs.Count)
            {
                var currentDiff = diffs[index];
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
                                diffs,
                                index,
                                numberOfDeletions,
                                textDeleted,
                                numberOfInsertions,
                                textInserted
                            );
                        }
                        // Merge back to back equalities together.
                        else if (index is not 0 && diffs[index - 1].Operation is Operation.Equal)
                        {
                            var previousDiff = diffs[index - 1];
                            diffs[index - 1] = previousDiff with { Text = previousDiff.Text + currentDiff.Text };
                            diffs.RemoveAt(index);
                        }
                        // Remove empty equalities, but ignore the dummy diff at the end.
                        else if (index < diffs.Count - 1 && currentDiff.Text.Length == 0)
                        {
                            diffs.RemoveAt(index);
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
            if (diffs[^1] is { Operation: Operation.Equal, Text: "" })
            {
                diffs.RemoveAt(diffs.Count - 1);
            }

            // If any shifts were made we need another pass at reordering.  If not, we're done.
            if (!OptimizeSingleEdits(diffs))
            {
                break;
            }
        }
    }

    /// <summary>
    ///     Optimizes <paramref name="diffs" /> by finding single edits surrounded on both sides by equalities that can
    ///     be shifted sideways to align the edit to a word boundary.
    /// </summary>
    /// <remarks>
    ///     Example: The c<ins>at c</ins>ame. -> The <ins>cat </ins>came.
    ///     Replaces <code>diff_cleanupSemanticLossless</code>.
    /// </remarks>
    /// <param name="diffs">
    ///     The diffs to be optimized.
    /// </param>
    internal static void CleanupSemanticLossless(this List<Diff> diffs)
    {
        Debug.Assert(diffs is not null, "diffs is not null");

        // First and last element don't need to be checked.
        for (var index = 1; index < diffs.Count - 1; index++)
        {
            var previousDiff = diffs[index - 1];
            var currentDiff = diffs[index];
            var nextDiff = diffs[index + 1];

            if (previousDiff.Operation is not Operation.Equal || nextDiff.Operation is not Operation.Equal)
            {
                continue;
            }

            var (bestPrevious, bestEdit, bestNext) = CleanupSemanticOptimizeDiffText(
                previousDiff.Text,
                currentDiff.Text,
                nextDiff.Text
            );

            // No improvement was found.
            if (previousDiff.Text == bestPrevious)
            {
                continue;
            }

            if (bestPrevious.Length is not 0)
            {
                diffs[index - 1] = previousDiff with { Text = bestPrevious };
            }
            else
            {
                diffs.RemoveAt(index - 1);
                index -= 1;
            }

            diffs[index] = currentDiff with { Text = bestEdit };

            if (bestNext.Length is not 0)
            {
                diffs[index + 1] = nextDiff with { Text = bestNext };
            }
            else
            {
                diffs.RemoveAt(index + 1);
                index -= 1;
            }
        }
    }

    /// <summary>
    ///     Given the text of an equality, an edit, and a second equality, optimize the text to align with word
    ///     boundaries.
    /// </summary>
    /// <param name="originalPreviousText">
    ///     The text of the first equality.
    /// </param>
    /// <param name="originalCurrentText">
    ///     The text of the edit.
    /// </param>
    /// <param name="originalNextText">
    ///     The text of the second equality.
    /// </param>
    /// <returns>
    ///     The most optimized permutation of the texts that could be found.
    /// </returns>
    private static (string bestPrevious, string bestEdit, string bestNext) CleanupSemanticOptimizeDiffText(
        string originalPreviousText,
        string originalCurrentText,
        string originalNextText
    )
    {
        // Early out - optimizations require prev/current to share a suffix, or current/next to share a prefix.
        if (
            originalPreviousText.LastCharOrDefault() != originalCurrentText.LastCharOrDefault()
            && originalCurrentText.FirstCharOrDefault() != originalNextText.FirstCharOrDefault()
        )
        {
            return (originalPreviousText, originalCurrentText, originalNextText);
        }

        var previous = new StringBuilder(originalPreviousText);
        var edit = new StringBuilder(originalCurrentText);
        var next = new StringBuilder(originalNextText);

        // Shift the edit as far left as possible.
        var suffixLength = originalPreviousText.AsSpan().FindCommonSuffix(originalCurrentText);
        if (suffixLength is not 0)
        {
            var suffix = originalCurrentText.AsSpan()[^suffixLength..];
            previous.Remove(previous.Length - suffixLength, suffixLength);
            edit.Insert(0, suffix).Remove(edit.Length - suffixLength, suffixLength);
            next.Insert(0, suffix);
        }

        var bestPrevious = previous.ToString();
        var bestEdit = edit.ToString();
        var bestNext = next.ToString();
        var bestScore = CleanupSemanticScore(previous, edit) + CleanupSemanticScore(edit, next);

        // Step character by character to the right, looking for the text that fits best.
        while (edit.Length is not 0 && next.Length is not 0 && edit[0] == next[0])
        {
            previous.Append(edit[0]);
            edit.Remove(0, 1).Append(next[0]);
            next.Remove(0, 1);

            var score = CleanupSemanticScore(previous, edit) + CleanupSemanticScore(edit, next);
            // Using >= encourages trailing rather than leading whitespace on edits.
            if (score >= bestScore)
            {
                bestScore = score;
                bestPrevious = previous.ToString();
                bestEdit = edit.ToString();
                bestNext = next.ToString();
            }
        }

        return (bestPrevious, bestEdit, bestNext);
    }

    /// <summary>
    ///     Given two strings, comuptes a score representing whether the boundary of the strings falls on logical
    ///     boundaries.
    /// </summary>
    /// <remarks>
    ///     Each port of this method behaves slightly differently due to subtle difference in each language's
    ///     definition of things like 'whitespace'.  Since this method's purpose is largely cosmetic, the choice has
    ///     been made to use each language's native features rather than forcing total conformity.
    /// </remarks>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>
    ///     A score in the range [0, 6], with 6 being the best score.
    /// </returns>
    private static int CleanupSemanticScore(StringBuilder text1, StringBuilder text2)
    {
        // Early out - edges are the best.
        if (text1.Length is 0 || text2.Length is 0)
        {
            return 6;
        }

        var char1 = text1[^1];
        var nonAlphaNumeric1 = !char.IsLetterOrDigit(char1);
        var whitespace1 = nonAlphaNumeric1 && char.IsWhiteSpace(char1);
        var lineBreak1 = whitespace1 && char.IsControl(char1);
        var blankLine1 = lineBreak1 && text1.EndsWithBlankLine();

        var char2 = text2[0];
        var nonAlphaNumeric2 = !char.IsLetterOrDigit(char2);
        var whitespace2 = nonAlphaNumeric2 && char.IsWhiteSpace(char2);
        var lineBreak2 = whitespace2 && char.IsControl(char2);
        var blankLine2 = lineBreak2 && text2.StartsWithBlankLine();

        if (blankLine1 || blankLine2)
        {
            return 5;
        }

        if (lineBreak1 || lineBreak2)
        {
            return 4;
        }

        // End of sentence.
        if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
        {
            return 3;
        }

        if (whitespace1 || whitespace2)
        {
            return 2;
        }

        if (nonAlphaNumeric1 || nonAlphaNumeric2)
        {
            return 1;
        }

        return 0;
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
    ///     True if any changes were made to <paramref name="diffs" />.
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
