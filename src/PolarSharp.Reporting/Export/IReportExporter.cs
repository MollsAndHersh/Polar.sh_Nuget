namespace PolarSharp.Reporting.Export;

/// <summary>
/// Streams report rows to an output stream in CSV or JSON. Designed for the host's
/// "download report" endpoint — never buffers the whole result in memory.
/// </summary>
public interface IReportExporter
{
    /// <summary>Writes rows as CSV (UTF-8, RFC 4180 quoting).</summary>
    Task ExportCsvAsync<T>(IAsyncEnumerable<T> rows, Stream output, CancellationToken ct = default);

    /// <summary>Writes rows as JSON array (one element per row, streamed).</summary>
    Task ExportJsonAsync<T>(IAsyncEnumerable<T> rows, Stream output, CancellationToken ct = default);
}
