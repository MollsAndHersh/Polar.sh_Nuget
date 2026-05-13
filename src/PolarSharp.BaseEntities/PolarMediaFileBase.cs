namespace PolarSharp.BaseEntities;

/// <summary>
/// Universal base for a Polar.sh media file (product images, downloadable benefit files,
/// avatar images, invoice PDFs).
/// </summary>
/// <remarks>
/// Files are uploaded to Polar's hosted storage; PolarSharp surfaces only the metadata.
/// </remarks>
public abstract record PolarMediaFileBase : IPolarEntity, IPolarTimestamped
{
    /// <summary>Gets the Polar.sh file identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the file's display name (typically the original upload filename).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the file's MIME type (e.g. "image/png", "application/pdf").</summary>
    public string? MimeType { get; init; }

    /// <summary>Gets the file size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Gets the publicly-accessible URL of the file (when applicable; null for protected/grant-gated files).</summary>
    public string? PublicUrl { get; init; }

    /// <summary>Gets the UTC timestamp the file was uploaded.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
