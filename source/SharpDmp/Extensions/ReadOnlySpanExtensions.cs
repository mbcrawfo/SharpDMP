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
}
