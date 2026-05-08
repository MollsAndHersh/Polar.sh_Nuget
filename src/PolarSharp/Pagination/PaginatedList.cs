namespace PolarSharp.Pagination;

/// <summary>
/// Wraps a single page of results from a Polar API list endpoint, together with pagination metadata.
/// </summary>
/// <typeparam name="T">The item type returned by the list endpoint.</typeparam>
/// <remarks>
/// Use <see cref="PaginationExtensions.ToAsyncEnumerable{T}"/> to automatically page through
/// all results without materialising the full result set in memory.
/// </remarks>
public sealed class PaginatedList<T>
{
    /// <summary>
    /// Gets the items in this page of results.
    /// </summary>
    /// <value>A read-only list of items. Never <see langword="null"/>; may be empty.</value>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Gets the maximum number of items per page as requested.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Gets the total number of items matching the query across all pages.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// Gets the total number of pages available.
    /// </summary>
    public int MaxPage { get; }

    /// <summary>
    /// Gets a value indicating whether there are further pages beyond this one.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when <see cref="Page"/> is less than <see cref="MaxPage"/>;
    /// otherwise <see langword="false"/>.
    /// </value>
    public bool HasNextPage => Page < MaxPage;

    /// <summary>
    /// Initialises a new <see cref="PaginatedList{T}"/>.
    /// </summary>
    /// <param name="items">The items in this page. Must not be <see langword="null"/>.</param>
    /// <param name="limit">The page size limit.</param>
    /// <param name="totalCount">Total items across all pages.</param>
    /// <param name="page">Current page number (1-based).</param>
    /// <param name="maxPage">Total number of pages.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    public PaginatedList(IReadOnlyList<T> items, int limit, int totalCount, int page, int maxPage)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        Limit = limit;
        TotalCount = totalCount;
        Page = page;
        MaxPage = maxPage;
    }
}
