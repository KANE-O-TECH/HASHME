namespace KaneO.HashMe.Core;

public static class HashResultFormatter
{
    public static string FormatStandard(IEnumerable<HashResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return string.Join(
            "    ",
            results
                .Where(item => item.IsSuccessful)
                .Select(result => $"{result.DisplayName}  SHA256: {result.Sha256}"));
    }

    public static string FormatDisplay(IEnumerable<HashResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return string.Join(
            "    ",
            results
                .Where(item => item.IsSuccessful)
                .Select(result => $"SHA256: {result.Sha256}"));
    }

    public static string FormatPair(PairHashResult pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return $"FILE A: {pair.First.DisplayName}  SHA256: {pair.First.Sha256}" +
               $"    FILE B: {pair.Second.DisplayName}  SHA256: {pair.Second.Sha256}" +
               $"    MATCH: {(pair.IsMatch ? "YES" : "NO")}";
    }
}
