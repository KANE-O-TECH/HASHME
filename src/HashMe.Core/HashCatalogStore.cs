using System.Text.Json;

namespace KaneO.HashMe.Core;

public sealed class HashCatalogStore
{
    private readonly string _catalogPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    private HashCatalogDocument? _document;

    public HashCatalogStore(string catalogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        _catalogPath = Path.GetFullPath(catalogPath);
    }

    public async Task<HashClassification> ClassifyAndRecordAsync(
        string filePath,
        string sha256,
        SidecarHash? sidecar,
        CancellationToken cancellationToken = default)
    {
        var normalizedHash = NormalizeHash(sha256);
        var fullPath = Path.GetFullPath(filePath);
        var pathKey = NormalizePathKey(fullPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await GetDocumentAsync(cancellationToken).ConfigureAwait(false);
            document.Paths.TryGetValue(pathKey, out var previous);
            var knownContent = document.Hashes.ContainsKey(normalizedHash);

            HashClassification classification;
            if (sidecar is not null)
            {
                classification = string.Equals(sidecar.Sha256, normalizedHash, StringComparison.OrdinalIgnoreCase)
                    ? new HashClassification(HashResultStatus.Verified, "Matches recognised SHA-256 record", sidecar.Sha256, sidecar.SourcePath)
                    : new HashClassification(HashResultStatus.Mismatch, "Does not match recognised SHA-256 record", sidecar.Sha256, sidecar.SourcePath);
            }
            else if (previous is not null)
            {
                classification = string.Equals(previous.Sha256, normalizedHash, StringComparison.OrdinalIgnoreCase)
                    ? new HashClassification(HashResultStatus.Verified, "Matches HASHME's prior record", previous.Sha256, "HASHME catalogue")
                    : new HashClassification(HashResultStatus.Changed, "File content changed since HASHME's prior record", previous.Sha256, "HASHME catalogue");
            }
            else if (knownContent)
            {
                classification = new HashClassification(HashResultStatus.Verified, "Exact content already known to HASHME", normalizedHash, "HASHME catalogue");
            }
            else
            {
                classification = new HashClassification(HashResultStatus.New, "First HASHME record for this content", null, null);
            }

            if (classification.Status is HashResultStatus.New or HashResultStatus.Verified)
            {
                RecordHash(document, fullPath, pathKey, normalizedHash);
                await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            }

            return classification;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReplaceStoredRecordAsync(
        string filePath,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var normalizedHash = NormalizeHash(sha256);
        var fullPath = Path.GetFullPath(filePath);
        var pathKey = NormalizePathKey(fullPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await GetDocumentAsync(cancellationToken).ConfigureAwait(false);
            RecordHash(document, fullPath, pathKey, normalizedHash);
            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void RecordHash(
        HashCatalogDocument document,
        string fullPath,
        string pathKey,
        string normalizedHash)
    {
        var now = DateTimeOffset.UtcNow;
        document.Paths[pathKey] = new HashPathEntry(fullPath, normalizedHash, now);
        if (!document.Hashes.TryGetValue(normalizedHash, out var hashEntry))
        {
            hashEntry = new HashIdentityEntry(now, now, []);
            document.Hashes[normalizedHash] = hashEntry;
        }

        hashEntry.LastSeenUtc = now;
        if (!hashEntry.Paths.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            hashEntry.Paths.Add(fullPath);
            if (hashEntry.Paths.Count > 32)
            {
                hashEntry.Paths.RemoveAt(0);
            }
        }
    }

    private async Task<HashCatalogDocument> GetDocumentAsync(CancellationToken cancellationToken)
    {
        if (_document is not null)
        {
            return _document;
        }

        if (!File.Exists(_catalogPath))
        {
            _document = new HashCatalogDocument();
            return _document;
        }

        try
        {
            await using var stream = File.OpenRead(_catalogPath);
            _document = await JsonSerializer.DeserializeAsync<HashCatalogDocument>(
                    stream,
                    _jsonOptions,
                    cancellationToken)
                .ConfigureAwait(false) ?? new HashCatalogDocument();
        }
        catch (JsonException)
        {
            _document = new HashCatalogDocument();
        }
        catch (IOException)
        {
            _document = new HashCatalogDocument();
        }

        _document.Paths = new Dictionary<string, HashPathEntry>(_document.Paths, StringComparer.OrdinalIgnoreCase);
        _document.Hashes = new Dictionary<string, HashIdentityEntry>(_document.Hashes, StringComparer.OrdinalIgnoreCase);
        return _document;
    }

    private async Task SaveDocumentAsync(HashCatalogDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_catalogPath)
            ?? throw new InvalidOperationException("HASHME catalogue path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _catalogPath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         64 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, document, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _catalogPath, overwrite: true);
    }

    private static string NormalizePathKey(string path) => Path.GetFullPath(path).ToUpperInvariant();

    private static string NormalizeHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 value must contain exactly 64 hexadecimal characters.", nameof(hash));
        }

        return hash.ToLowerInvariant();
    }
}

public sealed record HashClassification(
    HashResultStatus Status,
    string Detail,
    string? ExpectedSha256,
    string? VerificationSource);

public sealed class HashCatalogDocument
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, HashPathEntry> Paths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HashIdentityEntry> Hashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record HashPathEntry(string Path, string Sha256, DateTimeOffset LastSeenUtc);

public sealed class HashIdentityEntry
{
    public HashIdentityEntry(DateTimeOffset firstSeenUtc, DateTimeOffset lastSeenUtc, List<string> paths)
    {
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = lastSeenUtc;
        Paths = paths;
    }

    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public List<string> Paths { get; set; }
}
