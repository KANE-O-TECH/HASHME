namespace KaneO.HashMe.Core;

public sealed class HashWorkflow
{
    private readonly FileHashCalculator _calculator;
    private readonly SidecarHashReader _sidecarReader;
    private readonly HashCatalogStore _catalog;

    public HashWorkflow(
        FileHashCalculator calculator,
        SidecarHashReader sidecarReader,
        HashCatalogStore catalog)
    {
        _calculator = calculator;
        _sidecarReader = sidecarReader;
        _catalog = catalog;
    }

    public async Task<HashResult> ProcessFileAsync(
        string sourcePath,
        IProgress<HashProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(sourcePath);
            if (!File.Exists(fullPath))
            {
                return Error(fullPath, "File is unavailable or no longer exists");
            }

            var sidecar = await _sidecarReader.FindExpectedHashAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            var hash = await _calculator.ComputeSha256Async(fullPath, progress, cancellationToken)
                .ConfigureAwait(false);
            var classification = await _catalog.ClassifyAndRecordAsync(fullPath, hash, sidecar, cancellationToken)
                .ConfigureAwait(false);

            return new HashResult(
                fullPath,
                Path.GetFileName(fullPath),
                hash,
                classification.Status,
                classification.Detail,
                classification.ExpectedSha256,
                classification.VerificationSource);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return Error(sourcePath, "Access denied: " + exception.Message);
        }
        catch (IOException exception)
        {
            return Error(sourcePath, "File read failed: " + exception.Message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Error(sourcePath, "Hashing failed: " + exception.Message);
        }
    }

    public async Task<HashResult> ReplaceStoredRecordAsync(
        HashResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccessful || string.IsNullOrWhiteSpace(result.Sha256))
        {
            throw new ArgumentException("Only a successful SHA-256 result can replace a stored record.", nameof(result));
        }

        if (!File.Exists(result.SourcePath))
        {
            throw new InvalidOperationException("The file is no longer available. Its stored record was not replaced.");
        }

        var currentHash = await _calculator.ComputeSha256Async(
                result.SourcePath,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(currentHash, result.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The file changed after it was checked. Drop it again before replacing its stored record.");
        }

        await _catalog.ReplaceStoredRecordAsync(result.SourcePath, result.Sha256, cancellationToken)
            .ConfigureAwait(false);

        return result with
        {
            Status = HashResultStatus.Verified,
            Detail = "Matches explicitly replaced HASHME record",
            ExpectedSha256 = result.Sha256,
            VerificationSource = "HASHME catalogue — explicit replacement"
        };
    }

    private static HashResult Error(string path, string detail) => new(
        path,
        Path.GetFileName(path),
        string.Empty,
        HashResultStatus.Error,
        detail);
}
