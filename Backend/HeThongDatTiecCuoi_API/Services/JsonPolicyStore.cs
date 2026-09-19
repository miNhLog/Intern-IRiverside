using System.Diagnostics;
using System.Text.Json;
using HeThongDatTiecCuoi_API.DTOs.Policies;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class JsonPolicyStore : IPolicyStore
{
    private readonly PricingPolicyStoreOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonPolicyStore(IOptions<PricingPolicyStoreOptions> options)
    {
        _options = options.Value;
    }

    public async Task<PolicyResponseDto> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadDocumentAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PolicyResponseDto> UpdateAsync(
        long expectedVersion,
        PolicyContent content,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureDirectoryAsync(cancellationToken);
            await using var fileLock = await AcquireFileLockAsync(cancellationToken);
            var current = await ReadDocumentAsync(cancellationToken);

            if (current.Version != expectedVersion)
            {
                throw PolicyStoreException.Conflict();
            }

            var next = new PolicyResponseDto
            {
                SchemaVersion = PolicyValidator.CurrentSchemaVersion,
                Version = checked(current.Version + 1),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Currency = "VND",
                Deposit = content.Deposit,
                Cancellation = content.Cancellation
            };

            await WriteDocumentAsync(next, cancellationToken);
            return next;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PolicyResponseDto> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.FilePath))
        {
            throw PolicyStoreException.Unavailable();
        }

        try
        {
            await using var stream = new FileStream(
                _options.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var document = await JsonSerializer.DeserializeAsync<PolicyResponseDto>(
                stream,
                _serializerOptions,
                cancellationToken);

            if (document is null)
            {
                throw PolicyStoreException.Unavailable();
            }

            var errors = PolicyValidator.ValidateStored(document);
            if (errors.Count > 0)
            {
                throw PolicyStoreException.Unavailable();
            }

            return PolicyValidator.NormalizeStored(document);
        }
        catch (PolicyStoreException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
        catch (IOException exception)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
        catch (Exception exception) when (exception is NullReferenceException or InvalidOperationException)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
    }

    private async Task WriteDocumentAsync(
        PolicyResponseDto document,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{_options.FilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    _serializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _options.FilePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task EnsureDirectoryAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_options.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw PolicyStoreException.Unavailable();
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw PolicyStoreException.Unavailable(exception);
        }
    }

    private async Task<FileStream> AcquireFileLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = $"{_options.FilePath}.lock";
        var timeout = TimeSpan.FromSeconds(_options.LockTimeoutSeconds);
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.Asynchronous);
            }
            catch (IOException) when (stopwatch.Elapsed < timeout)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw PolicyStoreException.Unavailable(exception);
            }
            catch (IOException exception)
            {
                throw PolicyStoreException.Unavailable(exception);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
