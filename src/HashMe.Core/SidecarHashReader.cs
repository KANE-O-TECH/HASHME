using System.Text.RegularExpressions;

namespace KaneO.HashMe.Core;

public sealed partial class SidecarHashReader
{
    private static readonly string[] ManifestNames =
    [
        "SHA256SUMS",
        "SHA256SUMS.txt",
        "SHA256SUMS.sha256"
    ];

    public async Task<SidecarHash?> FindExpectedHashAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileName = Path.GetFileName(fullPath);

        var candidates = new List<(string Path, bool AllowBareHash)>
        {
            (fullPath + ".sha256", true),
            (Path.ChangeExtension(fullPath, ".sha256"), true)
        };

        candidates.AddRange(ManifestNames.Select(name => (Path.Combine(directory, name), false)));

        foreach (var candidate in candidates
                     .DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate.Path))
            {
                continue;
            }

            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(candidate.Path, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var line in lines)
            {
                var parsed = ParseLine(line, fileName, candidate.AllowBareHash);
                if (parsed is not null)
                {
                    return new SidecarHash(parsed, candidate.Path);
                }
            }
        }

        return null;
    }

    public static string? ParseLine(string line, string expectedFileName, bool allowBareHash)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var trimmed = line.Trim();
        if (allowBareHash && BareHashRegex().IsMatch(trimmed))
        {
            return trimmed.ToLowerInvariant();
        }

        var conventional = ConventionalRegex().Match(trimmed);
        if (conventional.Success && FileNameMatches(conventional.Groups["file"].Value, expectedFileName))
        {
            return conventional.Groups["hash"].Value.ToLowerInvariant();
        }

        var bsd = BsdRegex().Match(trimmed);
        if (bsd.Success && FileNameMatches(bsd.Groups["file"].Value, expectedFileName))
        {
            return bsd.Groups["hash"].Value.ToLowerInvariant();
        }

        return null;
    }

    private static bool FileNameMatches(string candidate, string expectedFileName)
    {
        var normalized = candidate.Trim().TrimStart('*').Replace('\\', '/');
        var candidateName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return string.Equals(candidateName, expectedFileName, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex BareHashRegex();

    [GeneratedRegex("^(?<hash>[a-fA-F0-9]{64})\\s+[* ]?(?<file>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ConventionalRegex();

    [GeneratedRegex("^SHA256\\s*\\((?<file>.+)\\)\\s*=\\s*(?<hash>[a-fA-F0-9]{64})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BsdRegex();
}
