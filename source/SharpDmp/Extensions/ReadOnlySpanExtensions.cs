namespace SharpDmp.Extensions;

public static class ReadOnlySpanExtensions
{
    /// <summary>
    ///     Determines the common prefix of two strings.
    /// </summary>
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

        var truncatedText1 = text1.Length > text2.Length ? text1[^text2.Length..] : text1;
        var truncatedText2 = text1.Length < text2.Length ? text2[..text1.Length] : text2;

        var lengthToCheck = Math.Min(text1.Length, text2.Length);
        if (truncatedText1.Equals(truncatedText2, StringComparison.Ordinal))
        {
            return lengthToCheck;
        }

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
}
