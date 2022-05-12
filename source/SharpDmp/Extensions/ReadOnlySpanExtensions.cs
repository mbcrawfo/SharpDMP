using System.Diagnostics;
using SharpDmp.Data;

namespace SharpDmp.Extensions;

public static class ReadOnlySpanExtensions
{
    /// <summary>
    ///     Determines the common prefix of two strings.
    /// </summary>
    /// <remarks>
    ///     Performance analysis:  https://neil.fraser.name/news/2010/11/04/
    /// </remarks>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>
    ///     The number of characters in common at the start of each string.
    /// </returns>
    public static int FindCommonPrefix(this ReadOnlySpan<char> text1, ReadOnlySpan<char> text2)
    {
        var lengthToCheck = Math.Min(text1.Length, text2.Length);

        for (var i = 0; i < lengthToCheck; i++)
        {
            if (text1[i] != text2[i])
            {
                return i;
            }
        }

        return lengthToCheck;
    }

    /// <summary>
    ///     Determines the common suffix of two strings.
    /// </summary>
    /// <remarks>
    ///     Performance analysis:  https://neil.fraser.name/news/2010/11/04/
    /// </remarks>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>
    ///     The number of characters in common at the end of each string.
    /// </returns>
    public static int FindCommonSuffix(this ReadOnlySpan<char> text1, ReadOnlySpan<char> text2)
    {
        var lengthToCheck = Math.Min(text1.Length, text2.Length);

        for (var i = 1; i <= lengthToCheck; i++)
        {
            if (text1[^i] != text2[^i])
            {
                return i - 1;
            }
        }

        return lengthToCheck;
    }

    /// <summary>
    ///     Determines if the suffix of the first string is the prefix of the second string.
    /// </summary>
    /// <remarks>
    ///     Performance analysis:  https://neil.fraser.name/news/2010/11/04/
    /// </remarks>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>
    ///     The number of characters in common at the end of the first string and the start of the second string.
    /// </returns>
    public static int FindCommonOverlap(this ReadOnlySpan<char> text1, ReadOnlySpan<char> text2)
    {
        if (text1.Length is 0 || text2.Length is 0)
        {
            return 0;
        }

        // Truncate the longer string.
        var truncatedText1 = text1.Length > text2.Length ? text1[^text2.Length..] : text1;
        var truncatedText2 = text1.Length < text2.Length ? text2[..text1.Length] : text2;

        var lengthToCheck = Math.Min(text1.Length, text2.Length);

        // Quick check for the worst case.
        if (truncatedText1.Equals(truncatedText2, StringComparison.Ordinal))
        {
            return lengthToCheck;
        }

        // Start by looking for a single character match, then increase length until no match is found.
        var best = 0;
        var length = 1;
        while (true)
        {
            var pattern = truncatedText1[(lengthToCheck - length)..];
            var found = truncatedText2.IndexOf(pattern, StringComparison.Ordinal);
            if (found == -1)
            {
                return best;
            }

            length += found;
            if (
                found is 0
                || truncatedText1[(lengthToCheck - length)..].Equals(
                    truncatedText2[..lengthToCheck],
                    StringComparison.Ordinal
                )
            )
            {
                best = length;
                length += 1;
            }
        }
    }

    /// <summary>
    ///     Do the two strings share a substring which is at least half the length of the the longer text?
    /// </summary>
    /// <remarks>
    ///     This speedup can produce non-minimal diffs.
    /// </remarks>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <param name="result"></param>
    internal static void FindHalfMatch(this ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, ref HalfMatch result)
    {
        var longText = text1.Length > text2.Length ? text1 : text2;
        var shortText = text1.Length > text2.Length ? text2 : text1;

        // Pointless [to check short strings..?].
        if (longText.Length < 4 || shortText.Length * 2 < longText.Length)
        {
            return;
        }

        var hm1 = new HalfMatch();
        var hm2 = new HalfMatch();

        // Check if the second quarter is the seed for a half match.
        FindHalfMatch(longText, shortText, (longText.Length + 3) / 4, ref hm1);
        // Check again based on the third quarter.
        FindHalfMatch(longText, shortText, (longText.Length + 1) / 2, ref hm2);

        if (!hm1.HasValue && !hm2.HasValue)
        {
            return;
        }

        ref var halfMatch = ref hm1;
        if (hm2.HasValue && (!hm1.HasValue || hm1.CommonMiddle.Length < hm2.CommonMiddle.Length))
        {
            halfMatch = ref hm2;
        }

        result =
            text1.Length > text2.Length
                ? halfMatch
                : new(
                      halfMatch.Text2Prefix,
                      halfMatch.Text2Suffix,
                      halfMatch.Text1Prefix,
                      halfMatch.Text1Suffix,
                      halfMatch.CommonMiddle
                  );
    }

    /// <summary>
    ///     Does a substring of <paramref name="shortText"/> exist within <paramref name="longText"/> such that the
    ///     substring is at least half the length of <paramref name="longText"/>?
    /// </summary>
    /// <param name="longText"></param>
    /// <param name="shortText"></param>
    /// <param name="longTextQuarterLengthStart"></param>
    /// <param name="result"></param>
    private static void FindHalfMatch(
        ReadOnlySpan<char> longText,
        ReadOnlySpan<char> shortText,
        int longTextQuarterLengthStart,
        ref HalfMatch result
    )
    {
        Debug.Assert(
            longTextQuarterLengthStart + longText.Length / 4 < longText.Length,
            "longTextQuarterLengthStart + longText.Length / 4 < longText.Length"
        );

        // Start with a 1/4 length Substring as a seed.
        var seed = longText.Slice(longTextQuarterLengthStart, longText.Length / 4);

        var index = -1;
        var bestCommon = ReadOnlySpan<char>.Empty;
        var bestLongTextA = ReadOnlySpan<char>.Empty;
        var bestLongTextB = ReadOnlySpan<char>.Empty;
        var bestShortTextA = ReadOnlySpan<char>.Empty;
        var bestShortTextB = ReadOnlySpan<char>.Empty;

        while (index < shortText.Length)
        {
            // There is no span IndexOf overload that takes a start index, so instead slice off the end of the span that
            // we need to check.  Moved this check out of the loop conditional for readability.
            var substringIndex = shortText[(index + 1)..].IndexOf(seed, StringComparison.Ordinal);
            if (substringIndex == -1)
            {
                break;
            }

            index += substringIndex + 1;

            var prefixLength = longText[longTextQuarterLengthStart..].FindCommonPrefix(shortText[index..]);
            var suffixLength = longText[..longTextQuarterLengthStart].FindCommonSuffix(shortText[..index]);

            if (bestCommon.Length < suffixLength + prefixLength)
            {
                // Can't concatenate spans together, must read the common middle as one slice.
                bestCommon = shortText.Slice(index - suffixLength, prefixLength + suffixLength);
                bestLongTextA = longText[..(longTextQuarterLengthStart - suffixLength)];
                bestLongTextB = longText[(longTextQuarterLengthStart + prefixLength)..];
                bestShortTextA = shortText[..(index - suffixLength)];
                bestShortTextB = shortText[(index + prefixLength)..];
            }
        }

        if (bestCommon.Length * 2 >= longText.Length)
        {
            result = new(bestLongTextA, bestLongTextB, bestShortTextA, bestShortTextB, bestCommon);
        }
    }
}
