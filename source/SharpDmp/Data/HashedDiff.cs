using System.Diagnostics;
using System.Text;

namespace SharpDmp.Data;

internal sealed record HashedDiff(Operation Operation, IReadOnlyList<int> HashedText)
{
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
