using System.Diagnostics;
using System.Text;

namespace SharpDmp.Extensions;

public static class StringBuilderExtensions
{
    internal static bool EndsWithBlankLine(this StringBuilder sb)
    {
        Debug.Assert(sb is not null, "sb is not null");

        if (sb.Length < 2 || sb[^1] is not '\n')
        {
            return false;
        }

        return sb[^2] switch
        {
            '\n' => true,
            '\r' when sb.Length >= 3 => sb[^3] is '\n',
            _ => false,
        };
    }

    internal static bool StartsWithBlankLine(this StringBuilder sb)
    {
        Debug.Assert(sb is not null, "sb is not null");

        if (sb.Length < 2)
        {
            return false;
        }

        return (sb[0], sb[1], sb.Length) switch
        {
            ('\n', '\n', _) => true,
            ('\n', '\r', >= 3) => sb[2] is '\n',
            ('\r', '\n', var len and >= 3)
              => (sb[2], len) switch
              {
                  ('\n', _) => true,
                  ('\r', >= 4) => sb[3] is '\n',
                  _ => false
              },
            _ => false,
        };
    }
}
