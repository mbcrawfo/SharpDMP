using System.Diagnostics;
using System.Text;
using SharpDmp.Data;

namespace SharpDmp.Extensions;

public static class DiffCollectionExtensions
{
    /// <summary>
    ///     Computes and returns the source text of a set of diffs (equalities and deletions).
    /// </summary>
    /// <remarks>
    ///     Replaces <code>diff_text1</code>.
    /// </remarks>
    /// <param name="diffs"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="diffs"/> is null.
    /// </exception>
    public static string GetSourceText(this IEnumerable<Diff> diffs)
    {
        if (diffs is null)
        {
            throw new ArgumentNullException(nameof(diffs));
        }

        var sb = new StringBuilder();
        foreach (var (_, text) in diffs.Where(d => d.Operation is not Operation.Insert))
        {
            sb.Append(text);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Computes and returns the destination text of a set of diffs (equalities and insertions).
    /// </summary>
    /// <remarks>
    ///     Replaces <code>diff_text2</code>.
    /// </remarks>
    /// <param name="diffs"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="diffs"/> is null.
    /// </exception>
    public static string GetDestinationText(this IEnumerable<Diff> diffs)
    {
        if (diffs is null)
        {
            throw new ArgumentNullException(nameof(diffs));
        }

        var sb = new StringBuilder();
        foreach (var (_, text) in diffs.Where(d => d.Operation is not Operation.Delete))
        {
            sb.Append(text);
        }

        return sb.ToString();
    }

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
                            index = CleanupAndMerge_MergeInsertAndDeleteCommonalities(
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
            if (!CleanupAndMerge_OptimizeSingleEdits(diffs))
            {
                break;
            }
        }
    }

    /// <summary>
    ///     Reduces the number of edits by eliminating operationally trivial equalities.
    /// </summary>
    /// <remarks>
    ///     Replaces <code>diff_cleanupEfficiency</code>.
    /// </remarks>
    /// <param name="diffs">
    ///     The diffs to be optimized.
    /// </param>
    /// <param name="editCost">
    ///     The cost of an empty edit operation in terms of edit characters.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     One of the items in <paramref name="diffs" /> contains an invalid <see cref="Operation" /> value.
    /// </exception>
    internal static void CleanupEfficiency(this List<Diff> diffs, int editCost = 4)
    {
        Debug.Assert(diffs is not null, "diffs is not null");
        Debug.Assert(editCost > 0, "editCost > 0");

        var changes = false;

        // Track indices of equalities that are candidates to be eliminated.
        var equalities = new Stack<int>();

        // Have edits occurred before or after the last equality?
        var beforeEquality = (d: false, i: false);
        var afterEquality = beforeEquality;

        static bool Both((bool d, bool i) tuple) => tuple.d && tuple.i;
        static bool Either((bool d, bool i) tuple) => tuple.d || tuple.i;

        static int Sum((bool d, bool i) tuple) =>
            Both(tuple)
              ? 2
              : Either(tuple)
                  ? 1
                  : 0;

        for (var index = 0; index < diffs.Count; index++)
        {
            var currentDiff = diffs[index];

            switch (currentDiff.Operation)
            {
                case Operation.Equal:
                    if (currentDiff.Text.Length < editCost && Either(afterEquality))
                    {
                        // Equality is a candidate to eliminate.
                        equalities.Push(index);
                        beforeEquality = afterEquality;
                    }
                    else
                    {
                        // Equality is not a candidate and can never become one.
                        equalities.Clear();
                    }

                    afterEquality = (false, false);
                    break;

                case Operation.Delete:
                case Operation.Insert:
#pragma warning disable CS8509
                    afterEquality = currentDiff.Operation switch
                    {
                        Operation.Delete => afterEquality with { d = true },
                        Operation.Insert => afterEquality with { i = true },
                    };
#pragma warning restore CS8509

                    if (!equalities.TryPeek(out var lastEqualityIndex))
                    {
                        continue;
                    }

                    Debug.Assert(lastEqualityIndex < diffs.Count, "lastEqualityIndex < diffs.Count");
                    var (op, lastEquality) = diffs[lastEqualityIndex];
                    Debug.Assert(op is Operation.Equal, "op is Operation.Equal");

                    // Five types to be split:
                    // <ins>A</ins><del>B</del>XY<ins>C</ins><del>D</del>
                    // <ins>A</ins>X<ins>C</ins><del>D</del>
                    // <ins>A</ins><del>B</del>X<ins>C</ins>
                    // <ins>A</ins>X<ins>C</ins><del>D</del>
                    // <ins>A</ins><del>B</del>X<del>C</del>
                    var sum = Sum(beforeEquality) + Sum(afterEquality);
                    if (
                        (lastEquality.Length is 0 || sum is not 4)
                        && (lastEquality.Length >= editCost / 2 || sum is not 3)
                    )
                    {
                        continue;
                    }

                    diffs.Insert(lastEqualityIndex, new Diff(Operation.Delete, lastEquality));
                    diffs[lastEqualityIndex + 1] = diffs[lastEqualityIndex + 1] with { Operation = Operation.Insert };

                    // Throw away the equality we just replaced.
                    equalities.Pop();

                    if (Both(beforeEquality))
                    {
                        // Changes were made that could affect the previous entry, so keep going.
                        equalities.Clear();
                        afterEquality = (true, true);
                    }
                    else
                    {
                        // Previous equality needs to be re-evaluated.
                        equalities.TryPop(out _);

                        // Jump back to the candidate equality before if one exists, otherwise restart.
                        index = equalities.TryPeek(out var i) ? i : -1;

                        afterEquality = (false, false);
                    }

                    changes = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(diffs),
                        currentDiff.Operation,
                        $"{nameof(Diff)} contains an invalid {nameof(Operation)} value."
                    );
            }
        }

        if (changes)
        {
            diffs.CleanupAndMerge();
        }
    }

    /// <summary>
    ///     Reduces the number of edits by eliminating semantically trivial equalities.
    /// </summary>
    /// <remarks>
    ///     Replaces <code>diff_cleanupSemantic</code>.
    /// </remarks>
    /// <param name="diffs"></param>
    internal static void CleanupSemantic(this List<Diff> diffs)
    {
        Debug.Assert(diffs is not null, "diffs is not null");

        // Eliminating small equalities will produce new deletions and insertions that need to be merged together with
        // existing edits.
        if (diffs.CleanupSemantic_EliminateTrivialEqualities())
        {
            diffs.CleanupAndMerge();
        }

        diffs.CleanupSemanticLossless();
        diffs.CleanupSemantic_ExtractOverlappingEdits();
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

            var (bestPrevious, bestEdit, bestNext) = CleanupSemanticLossless_OptimizeDiffText(
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
    private static int CleanupAndMerge_MergeInsertAndDeleteCommonalities(
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
    private static bool CleanupAndMerge_OptimizeSingleEdits(IList<Diff> diffs)
    {
        var changes = false;

        // Intentionally ignore the first and last element (they don't qualifity).
        for (var index = 1; index < diffs.Count - 1; index++)
        {
            var previousDiff = diffs[index - 1];
            var currentDiff = diffs[index];
            var nextDiff = diffs[index + 1];

            if (previousDiff.Operation is not Operation.Equal || nextDiff.Operation is not Operation.Equal)
            {
                continue;
            }

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

        return changes;
    }

    /// <summary>
    ///     Eliminates equalities that are smaller than or equal to the edits on both sides of the equality.
    /// </summary>
    /// <param name="diffs"></param>
    /// <returns>
    ///     True if any changes were made to <paramref name="diffs" />.
    /// </returns>
    private static bool CleanupSemantic_EliminateTrivialEqualities(this List<Diff> diffs)
    {
        var changes = false;

        // Tracks the indices where equalities are found.
        var equalities = new Stack<int>();

        // Track the number of characters changed before and after the last equality found.
        var beforeEquality = (d: 0, i: 0);
        var afterEquality = beforeEquality;

        static int Max((int d, int i) tuple) => Math.Max(tuple.d, tuple.i);

        for (var index = 0; index < diffs.Count; index++)
        {
            var currentDiff = diffs[index];
            if (currentDiff.Operation is Operation.Equal)
            {
                equalities.Push(index);
                beforeEquality = afterEquality;
                afterEquality = (0, 0);
            }
            else
            {
                Debug.Assert(
                    currentDiff.Operation is Operation.Delete or Operation.Insert,
                    "currentDiff.Operation is Operation.Delete or Operation.Insert"
                );
#pragma warning disable CS8509
                afterEquality = currentDiff.Operation switch
                {
                    Operation.Delete => afterEquality with { d = afterEquality.d + currentDiff.Text.Length },
                    Operation.Insert => afterEquality with { i = afterEquality.i + currentDiff.Text.Length },
                };
#pragma warning restore CS8509

                if (!equalities.TryPeek(out var lastEqualityIndex))
                {
                    continue;
                }

                Debug.Assert(lastEqualityIndex < diffs.Count, "lastEqualityIndex < diffs.Count");

                var (op, lastEquality) = diffs[lastEqualityIndex];
                Debug.Assert(op is Operation.Equal, "op is Operation.Equal");

                // Eliminate the equality if it's smaller or equal to the edits on both sides.
                if (lastEquality.Length > Math.Min(Max(beforeEquality), Max(afterEquality)))
                {
                    continue;
                }

                // Transform the equality into a deletion and insertion that can be merged with existing edits by
                // later optimizations.
                diffs.Insert(lastEqualityIndex, new Diff(Operation.Delete, lastEquality));
                diffs[lastEqualityIndex + 1] = diffs[lastEqualityIndex + 1] with { Operation = Operation.Insert };

                // Clear the edit we just replaced.
                equalities.Pop();

                // If there was an equality that couldn't be replaced before the one we just replaced, the new deletion
                // and insertion may have changed the math for it, so we need to check it again.
                equalities.TryPop(out _);

                // Pick up from the 2nd equality back if there is one, otherwise start over from the beginning.
                index = equalities.TryPeek(out var i) ? i : -1;

                beforeEquality = (0, 0);
                afterEquality = (0, 0);
                changes = true;
            }
        }

        return changes;
    }

    /// <summary>
    ///     Find and eliminate overlaps between deletions and insertions where the overlap is as large as the edit
    ///     ahead or behind it.
    /// </summary>
    /// <param name="diffs"></param>
    private static void CleanupSemantic_ExtractOverlappingEdits(this List<Diff> diffs)
    {
        for (var index = 1; index < diffs.Count; index++)
        {
            var previousDiff = diffs[index - 1];
            var currentDiff = diffs[index];

            if (previousDiff.Operation is not Operation.Delete || currentDiff.Operation is not Operation.Insert)
            {
                continue;
            }

            var deletion = previousDiff.Text;
            var insertion = currentDiff.Text;
            var halfMaxEditLength = Math.Min(deletion.Length, insertion.Length) / 2.0;
            var deletionToInsertionOverlap = deletion.AsSpan().FindCommonOverlap(insertion);
            var insertionToDeletionOverlap = insertion.AsSpan().FindCommonOverlap(deletion);

            if (deletionToInsertionOverlap >= insertionToDeletionOverlap)
            {
                if (deletionToInsertionOverlap >= halfMaxEditLength)
                {
                    diffs[index - 1] = previousDiff with { Text = deletion[..^deletionToInsertionOverlap] };
                    diffs.Insert(index, new Diff(Operation.Equal, insertion[..deletionToInsertionOverlap]));
                    diffs[index + 1] = currentDiff with { Text = insertion[deletionToInsertionOverlap..] };

                    index += 1;
                }
            }
            else if (insertionToDeletionOverlap >= halfMaxEditLength)
            {
                diffs[index - 1] = currentDiff with { Text = insertion[..^insertionToDeletionOverlap] };
                diffs.Insert(index, new Diff(Operation.Equal, deletion[..insertionToDeletionOverlap]));
                diffs[index + 1] = previousDiff with { Text = deletion[insertionToDeletionOverlap..] };

                index += 1;
            }

            index += 1;
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
    private static (string bestPrevious, string bestEdit, string bestNext) CleanupSemanticLossless_OptimizeDiffText(
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
        var bestScore = SemanticScore(previous, edit) + SemanticScore(edit, next);

        // Step character by character to the right, looking for the text that fits best.
        while (edit.Length is not 0 && next.Length is not 0 && edit[0] == next[0])
        {
            previous.Append(edit[0]);
            edit.Remove(0, 1).Append(next[0]);
            next.Remove(0, 1);

            var score = SemanticScore(previous, edit) + SemanticScore(edit, next);
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
    ///     Replaces <code>diff_cleanupSemanticScore</code>.
    /// </remarks>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>
    ///     A score in the range [0, 6], with 6 being the best score.
    /// </returns>
    private static int SemanticScore(StringBuilder text1, StringBuilder text2)
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
}
