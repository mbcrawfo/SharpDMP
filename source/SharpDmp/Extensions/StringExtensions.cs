using System.Diagnostics;
using System.Text;
using System.Web;

namespace SharpDmp.Extensions;

internal static class StringExtensions
{
    // Replaces encodeURI
    public static string EncodeUri(this string input)
    {
        Debug.Assert(input is not null, "input is not null");

        var sb = new StringBuilder(HttpUtility.UrlEncode(input));

        // C# encodes some characters that we want to un-encode.
        // Do that in a single pass instead of looping over the string repeatedly with Replace().
        for (var i = 0; i < sb.Length; i++)
        {
            switch (sb[i])
            {
                case '+':
                    sb[i] = ' ';
                    break;

                case '%' when i + 2 < sb.Length:
                {
                    var replacementChar = (sb[i + 1], sb[i + 2]) switch
                    {
                        ('2', '3') => '#',
                        ('2', '4') => '$',
                        ('2', '6') => '&',
                        ('2', '7') => '\'',
                        ('2', 'b') => '+',
                        ('2', 'c') => ',',
                        ('2', 'f') => '/',
                        ('3', 'a') => ':',
                        ('3', 'b') => ';',
                        ('3', 'd') => '=',
                        ('3', 'f') => '?',
                        ('4', '0') => '@',
                        ('7', 'e') => '~',
                        _ => '\0'
                    };

                    if (replacementChar is not '\0')
                    {
                        sb[i] = replacementChar;
                        sb.Remove(i + 1, 2);
                    }

                    break;
                }
            }
        }

        return sb.ToString();
    }

    public static char? FirstCharOrDefault(this string input)
    {
        Debug.Assert(input is not null, "input is not null");

        return input.Length is 0 ? null : input[0];
    }

    public static char? LastCharOrDefault(this string input)
    {
        Debug.Assert(input is not null, "input is not null");

        return input.Length is 0 ? null : input[^1];
    }
}
