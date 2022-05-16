namespace SharpDmp.Data;

internal readonly ref struct HalfMatch
{
    public HalfMatch()
    {
        Found = false;
        Text1Prefix = ReadOnlySpan<char>.Empty;
        Text1Suffix = ReadOnlySpan<char>.Empty;
        Text2Prefix = ReadOnlySpan<char>.Empty;
        Text2Suffix = ReadOnlySpan<char>.Empty;
        CommonMiddle = ReadOnlySpan<char>.Empty;
    }

    public HalfMatch(
        ReadOnlySpan<char> text1Prefix,
        ReadOnlySpan<char> text1Suffix,
        ReadOnlySpan<char> text2Prefix,
        ReadOnlySpan<char> text2Suffix,
        ReadOnlySpan<char> commonMiddle
    )
    {
        Found = true;
        Text1Prefix = text1Prefix;
        Text1Suffix = text1Suffix;
        Text2Prefix = text2Prefix;
        Text2Suffix = text2Suffix;
        CommonMiddle = commonMiddle;
    }

    public bool Found { get; }

    public ReadOnlySpan<char> Text1Prefix { get; }

    public ReadOnlySpan<char> Text1Suffix { get; }

    public ReadOnlySpan<char> Text2Prefix { get; }

    public ReadOnlySpan<char> Text2Suffix { get; }

    public ReadOnlySpan<char> CommonMiddle { get; }
}
