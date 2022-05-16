using System.Diagnostics;
using System.Text;
using SharpDmp.Extensions;

namespace SharpDmp.Data;

internal sealed record HashedDiff(Operation Operation, IReadOnlyList<int> HashedText)
{
    /// <summary>
    ///     Converts this <see cref="HashedDiff"/> back to a normal <see cref="Diff"/>.
    /// </summary>
    /// <remarks>
    ///     Replaces <code>diff_charsToLines</code>.
    /// </remarks>
    /// <param name="uniqueLines">
    ///     The collection of unique lines produced by <see cref="ReadOnlySpanExtensions.LinesToChars"/>.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="uniqueLines"/> is null.
    /// </exception>
    public Diff Rehydrate(IReadOnlyList<string> uniqueLines)
    {
        if (uniqueLines is null)
        {
            throw new ArgumentNullException(nameof(uniqueLines));
        }

        var sb = new StringBuilder();
        foreach (var hash in HashedText)
        {
            Debug.Assert(hash < uniqueLines.Count, "hash < uniqueLines.Count");
            sb.Append(uniqueLines[hash]);
        }

        return new(Operation, sb.ToString());
    }
}
