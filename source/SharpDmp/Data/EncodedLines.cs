namespace SharpDmp.Data;

internal sealed record EncodedLines(
    IReadOnlyList<int> EncodedText1,
    IReadOnlyList<int> EncodedText2,
    IReadOnlyList<string> UniqueStrings
);
