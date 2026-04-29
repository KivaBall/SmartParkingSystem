using SmartParkingSystem.Domain.Models.Camera;

namespace SmartParkingSystem.Maui.Services.Camera;

public sealed class CameraSnapshotService : ICameraSnapshotService
{
    private const string DataUrlPrefix = "data:image/jpeg;base64,";
    private const string SnapshotDirectoryName = "camera-snapshots";
    private readonly Lock _sync = new Lock();

    public IReadOnlyList<CameraSnapshot> GetSnapshots()
    {
        var directory = ResolveSnapshotDirectory();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        lock (_sync)
        {
            return Directory
                .EnumerateFiles(directory, "*.jpg")
                .OrderByDescending(File.GetCreationTimeUtc)
                .Take(24)
                .Select(BuildSnapshot)
                .ToArray();
        }
    }

    public async Task<CameraSnapshot> SaveSnapshotAsync(
        string imageDataUrl,
        CancellationToken cancellationToken = default)
    {
        if (!imageDataUrl.StartsWith(DataUrlPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Camera snapshot payload is not a JPEG data URL.");
        }

        var directory = ResolveSnapshotDirectory();
        Directory.CreateDirectory(directory);

        var id = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var filePath = Path.Combine(directory, $"entry-camera-{id}.jpg");
        var bytes = Convert.FromBase64String(imageDataUrl[DataUrlPrefix.Length..]);

        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
        return BuildSnapshot(filePath);
    }

    private static CameraSnapshot BuildSnapshot(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var imageSource = $"{DataUrlPrefix}{Convert.ToBase64String(bytes)}";
        var createdAtUtc = File.GetCreationTimeUtc(filePath);

        return new CameraSnapshot(
            Path.GetFileNameWithoutExtension(filePath),
            filePath,
            imageSource,
            new DateTimeOffset(DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)));
    }

    private static string ResolveSnapshotDirectory()
    {
        return Path.Combine(FileSystem.AppDataDirectory, SnapshotDirectoryName);
    }
}