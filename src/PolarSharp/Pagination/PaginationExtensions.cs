namespace PolarSharp.Pagination;

/// <summary>
/// Extension methods for working with paginated Polar API responses.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Converts a page-fetching delegate into an <see cref="IAsyncEnumerable{T}"/> that
    /// automatically fetches subsequent pages until all results are exhausted.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="fetchPage">
    /// A delegate that accepts a page number (1-based) and returns a <see cref="Task{T}"/>
    /// resolving to the <see cref="PaginatedList{T}"/> for that page.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can cancel the iteration. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that yields all items across all pages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fetchPage"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// await foreach (var order in PaginationExtensions.ToAsyncEnumerable(
    ///     page => polar.Orders.GetAsync(q => q.QueryParameters.Page = page),
    ///     ct))
    /// {
    ///     Console.WriteLine(order.Id);
    /// }
    /// </code>
    /// </example>
#pragma warning disable VSTHRD200
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
#pragma warning restore VSTHRD200
        Func<int, Task<PaginatedList<T>>> fetchPage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetchPage);

        var page = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await fetchPage(page).ConfigureAwait(false);
            foreach (var item in result.Items)
                yield return item;

            if (!result.HasNextPage)
                break;

            page++;
        }
    }
}
