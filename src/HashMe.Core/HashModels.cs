namespace KaneO.HashMe.Core;

public enum HashResultStatus
{
    New,
    Verified,
    Changed,
    Mismatch,
    Error
}

public sealed record HashProgress(long BytesRead, long TotalBytes)
{
    public int Percent => TotalBytes <= 0
        ? 100
        : (int)Math.Clamp(BytesRead * 100L / TotalBytes, 0L, 100L);
}

public sealed record HashResult(
    string SourcePath,
    string DisplayName,
    string Sha256,
    HashResultStatus Status,
    string Detail,
    string? ExpectedSha256 = null,
    string? VerificationSource = null)
{
    public bool IsSuccessful => Status is not HashResultStatus.Error;
}

public sealed record PairHashResult(HashResult First, HashResult Second)
{
    public bool IsMatch =>
        First.IsSuccessful &&
        Second.IsSuccessful &&
        string.Equals(First.Sha256, Second.Sha256, StringComparison.OrdinalIgnoreCase);
}

public sealed record SidecarHash(string Sha256, string SourcePath);
