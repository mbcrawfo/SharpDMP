using System.Text;

namespace SharpDmp.Data;

public sealed record Diff(Operation Operation, string Text)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(nameof(Operation)).Append(" = ").Append(Operation.ToString());
        builder.Append(',');
        builder.Append(nameof(Text)).Append(" = ").Append(Text.Replace('\n', '\u00b6'));

        return true;
    }
}
