using System.Security.Cryptography;
using System.Text;
using KaneO.HashMe.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Known SHA-256 vector", KnownSha256Vector),
    ("Empty file SHA-256 vector", EmptyFileVector),
    ("First record then verified record", FirstThenVerified),
    ("Catalogue survives process-style reload", CatalogueReload),
    ("Known content at another path", KnownContentAnotherPath),
    ("Changed content requires explicit record replacement", ChangedContentSamePath),
    ("Recognised sidecar verification", SidecarVerification),
    ("Recognised sidecar mismatch", SidecarMismatch),
    ("Sidecar parser formats", SidecarParserFormats),
    ("Pair match and pair copy format", PairMatchAndFormat),
    ("Pair difference detection", PairDifference),
    ("Multi-file standard copy format", MultiFileFormat),
    ("Multi-file display format", MultiFileDisplayFormat),
    ("Streaming progress reaches completion", StreamingProgress),
    ("Hashing never modifies source file", SourceFileNotModified)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static async Task KnownSha256Vector()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "abc.txt");
        await File.WriteAllTextAsync(path, "abc", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var actual = await new FileHashCalculator().ComputeSha256Async(path);
        Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", actual);
    });
}

static async Task EmptyFileVector()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "empty.bin");
        await File.WriteAllBytesAsync(path, []);
        var actual = await new FileHashCalculator().ComputeSha256Async(path);
        Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", actual);
    });
}

static async Task FirstThenVerified()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "evidence.zip");
        await File.WriteAllTextAsync(path, "evidence");
        var workflow = CreateWorkflow(workspace);

        var first = await workflow.ProcessFileAsync(path);
        var second = await workflow.ProcessFileAsync(path);

        Equal(HashResultStatus.New, first.Status);
        Equal(HashResultStatus.Verified, second.Status);
        Equal(first.Sha256, second.Sha256);
    });
}

static async Task KnownContentAnotherPath()
{
    await WithWorkspace(async workspace =>
    {
        var firstPath = Path.Combine(workspace, "first.dat");
        var secondPath = Path.Combine(workspace, "copy.dat");
        await File.WriteAllTextAsync(firstPath, "identical-content");
        File.Copy(firstPath, secondPath);
        var workflow = CreateWorkflow(workspace);

        var first = await workflow.ProcessFileAsync(firstPath);
        var second = await workflow.ProcessFileAsync(secondPath);

        Equal(HashResultStatus.New, first.Status);
        Equal(HashResultStatus.Verified, second.Status);
        True(second.Detail.Contains("already known", StringComparison.OrdinalIgnoreCase));
    });
}

static async Task CatalogueReload()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "retained-evidence.bin");
        await File.WriteAllTextAsync(path, "persistent-record");

        var first = await CreateWorkflow(workspace).ProcessFileAsync(path);
        var reloaded = await CreateWorkflow(workspace).ProcessFileAsync(path);

        Equal(HashResultStatus.New, first.Status);
        Equal(HashResultStatus.Verified, reloaded.Status);
        Equal(first.Sha256, reloaded.Sha256);
    });
}

static async Task ChangedContentSamePath()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "mutable.txt");
        await File.WriteAllTextAsync(path, "before");
        var workflow = CreateWorkflow(workspace);
        var before = await workflow.ProcessFileAsync(path);
        await File.WriteAllTextAsync(path, "after");
        var after = await workflow.ProcessFileAsync(path);
        var repeatedWithoutApproval = await workflow.ProcessFileAsync(path);

        Equal(HashResultStatus.New, before.Status);
        Equal(HashResultStatus.Changed, after.Status);
        Equal(HashResultStatus.Changed, repeatedWithoutApproval.Status);
        NotEqual(before.Sha256, after.Sha256);
        Equal(before.Sha256, after.ExpectedSha256);

        var replaced = await workflow.ReplaceStoredRecordAsync(after);
        var verifiedAfterApproval = await workflow.ProcessFileAsync(path);
        Equal(HashResultStatus.Verified, replaced.Status);
        Equal(HashResultStatus.Verified, verifiedAfterApproval.Status);
        Equal(after.Sha256, verifiedAfterApproval.Sha256);
    });
}

static async Task SidecarVerification()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "package.zip");
        await File.WriteAllTextAsync(path, "package-content");
        var expected = await new FileHashCalculator().ComputeSha256Async(path);
        await File.WriteAllTextAsync(path + ".sha256", $"{expected}  *package.zip\n");

        var result = await CreateWorkflow(workspace).ProcessFileAsync(path);
        Equal(HashResultStatus.Verified, result.Status);
        Equal(expected, result.ExpectedSha256);
        True(result.VerificationSource?.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) == true);
    });
}

static async Task SidecarMismatch()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "package.zip");
        await File.WriteAllTextAsync(path, "package-content");
        var wrong = new string('0', 64);
        await File.WriteAllTextAsync(path + ".sha256", $"{wrong}  *package.zip\n");

        var result = await CreateWorkflow(workspace).ProcessFileAsync(path);
        Equal(HashResultStatus.Mismatch, result.Status);
        Equal(wrong, result.ExpectedSha256);
        NotEqual(wrong, result.Sha256);
    });
}

static Task SidecarParserFormats()
{
    var hash = new string('a', 64);
    Equal(hash, SidecarHashReader.ParseLine($"{hash}  *Evidence.ZIP", "evidence.zip", false));
    Equal(hash, SidecarHashReader.ParseLine($"SHA256 (evidence.zip) = {hash}", "evidence.zip", false));
    Equal(hash, SidecarHashReader.ParseLine(hash, "evidence.zip", true));
    Equal<string?>(null, SidecarHashReader.ParseLine($"{hash}  *other.zip", "evidence.zip", false));
    return Task.CompletedTask;
}

static Task PairMatchAndFormat()
{
    var hash = new string('b', 64);
    var first = new HashResult("C:\\A.zip", "A.zip", hash, HashResultStatus.New, "new");
    var second = new HashResult("C:\\B.zip", "B.zip", hash, HashResultStatus.New, "new");
    var pair = new PairHashResult(first, second);

    True(pair.IsMatch);
    var formatted = HashResultFormatter.FormatPair(pair);
    True(formatted.Contains("MATCH: YES", StringComparison.Ordinal));
    True(formatted.Contains("FILE A", StringComparison.Ordinal));
    True(formatted.Contains("FILE B", StringComparison.Ordinal));
    True(!formatted.Contains(Environment.NewLine, StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task PairDifference()
{
    var first = new HashResult("C:\\A.zip", "A.zip", new string('a', 64), HashResultStatus.New, "new");
    var second = new HashResult("C:\\B.zip", "B.zip", new string('b', 64), HashResultStatus.New, "new");
    var pair = new PairHashResult(first, second);

    True(!pair.IsMatch);
    True(HashResultFormatter.FormatPair(pair).Contains("MATCH: NO", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task MultiFileFormat()
{
    var firstHash = new string('1', 64);
    var secondHash = new string('2', 64);
    var results = new[]
    {
        new HashResult("C:\\one.txt", "one.txt", firstHash, HashResultStatus.New, "new"),
        new HashResult("C:\\two.txt", "two.txt", secondHash, HashResultStatus.Verified, "verified")
    };

    Equal(
        $"one.txt  SHA256: {firstHash}    two.txt  SHA256: {secondHash}",
        HashResultFormatter.FormatStandard(results));
    return Task.CompletedTask;
}

static Task MultiFileDisplayFormat()
{
    var firstHash = new string('3', 64);
    var secondHash = new string('4', 64);
    var results = new[]
    {
        new HashResult("C:\\alpha.txt", "alpha.txt", firstHash, HashResultStatus.New, "new"),
        new HashResult("C:\\beta.txt", "beta.txt", secondHash, HashResultStatus.Verified, "verified")
    };

    Equal(
        $"SHA256: {firstHash}    SHA256: {secondHash}",
        HashResultFormatter.FormatDisplay(results));
    return Task.CompletedTask;
}

static async Task SourceFileNotModified()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "evidence.bin");
        var bytes = RandomNumberGenerator.GetBytes(4096);
        await File.WriteAllBytesAsync(path, bytes);
        var timestamp = File.GetLastWriteTimeUtc(path);

        await CreateWorkflow(workspace).ProcessFileAsync(path);

        var after = await File.ReadAllBytesAsync(path);
        True(bytes.SequenceEqual(after));
        Equal(timestamp, File.GetLastWriteTimeUtc(path));
    });
}

static async Task StreamingProgress()
{
    await WithWorkspace(async workspace =>
    {
        var path = Path.Combine(workspace, "large-evidence.bin");
        var bytes = RandomNumberGenerator.GetBytes(3 * 1024 * 1024 + 17);
        await File.WriteAllBytesAsync(path, bytes);
        var updates = new List<HashProgress>();

        var actual = await new FileHashCalculator().ComputeSha256Async(
            path,
            new InlineProgress<HashProgress>(updates.Add));
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        Equal(expected, actual);
        True(updates.Count >= 4);
        Equal(100, updates[^1].Percent);
        True(updates.Zip(updates.Skip(1), (left, right) => right.BytesRead >= left.BytesRead).All(value => value));
    });
}

static HashWorkflow CreateWorkflow(string workspace) => new(
    new FileHashCalculator(),
    new SidecarHashReader(),
    new HashCatalogStore(Path.Combine(workspace, "catalog", "hash-records.json")));

static async Task WithWorkspace(Func<string, Task> action)
{
    var path = Path.Combine(Path.GetTempPath(), "HASHME-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    try
    {
        await action(path);
    }
    finally
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void NotEqual<T>(T first, T second)
{
    if (EqualityComparer<T>.Default.Equals(first, second))
    {
        throw new InvalidOperationException($"Values unexpectedly matched: '{first}'.");
    }
}

static void True(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Condition was false.");
    }
}

file sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
