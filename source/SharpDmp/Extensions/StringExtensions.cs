using System.Diagnostics;

namespace SharpDmp.Extensions;

internal static class StringExtensions
{
    public static char? LastCharOrDefault(this string input)
    {
        Debug.Assert(input is not null, "input is not null");

        return input.Length is 0 ? null : input[^1];
    }

    public static char? FirstCharOrDefault(this string input)
    {
        Debug.Assert(input is not null, "input is not null");

        return input.Length is 0 ? null : input[0];
    }
}
