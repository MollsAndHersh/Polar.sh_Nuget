using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// Default <see cref="IRefundService"/> implementation. Validates inputs, delegates the
/// Polar HTTP call to <see cref="IPolarRefundsApi"/>, captures every successful refund in
/// the admin audit log so the tenant has a record of who issued what.
/// </summary>
/// <remarks>
/// <para>
/// Refund records themselves are NOT persisted locally — Polar is the source of truth for
/// financial data and the listing path goes straight to their API. The only thing we keep
/// locally is the audit-log entry capturing actor + amount + reason + timestamp.
/// </para>
/// <para>
/// Polar HTTP wiring (<see cref="IPolarRefundsApi"/>) ships as a best-effort wrapper in
/// v1.3.0; sandbox validation is tracked under TASK-V20-002.
/// </para>
/// </remarks>
internal sealed class RefundService(
    IPolarRefundsApi polarApi,
    PolarCatalogDbContext db,
    IAuditLogActorProvider actorProvider,
    TimeProvider time,
    ILogger<RefundService> logger) : IRefundService
{
    private readonly IPolarRefundsApi _polarApi = polarApi ?? throw new ArgumentNullException(nameof(polarApi));
    private readonly PolarCatalogDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IAuditLogActorProvider _actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly ILogger<RefundService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<RefundResult, RefundError>> IssueFullRefundAsync(
        string polarOrderId,
        RefundReason reason,
        string? comment,
        CancellationToken ct = default)
    {
        if (ValidateCommentRequirement(reason, comment) is { } validationError)
            return Result<RefundResult, RefundError>.Failure(validationError);

        return await IssueAndAuditAsync(
            new RefundApiRequest(polarOrderId, Amount: null, Currency: null, reason, comment),
            ct).ConfigureAwait(false);
    }

    public async Task<Result<RefundResult, RefundError>> IssuePartialRefundAsync(
        string polarOrderId,
        int amount,
        string currency,
        RefundReason reason,
        string? comment,
        CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return Result<RefundResult, RefundError>.Failure(new RefundError(
                RefundErrorKind.AmountExceedsRefundable,
                $"Refund amount must be positive (got {amount})."));
        }
        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result<RefundResult, RefundError>.Failure(new RefundError(
                RefundErrorKind.CurrencyMismatch,
                "Currency code is required for partial refunds."));
        }
        if (ValidateCommentRequirement(reason, comment) is { } validationError)
            return Result<RefundResult, RefundError>.Failure(validationError);

        return await IssueAndAuditAsync(
            new RefundApiRequest(polarOrderId, amount, currency, reason, comment),
            ct).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<RefundRecord>, RefundError>> ListForOrderAsync(
        string polarOrderId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(polarOrderId);

        var apiResult = await _polarApi.ListRefundsForOrderAsync(polarOrderId, ct).ConfigureAwait(false);
        return apiResult.Match(
            onSuccess: rows => Result<IReadOnlyList<RefundRecord>, RefundError>.Success(
                [.. rows.Select(r => new RefundRecord(r.RefundId, r.Amount, r.Currency, r.Reason, r.Comment, r.CreatedAt))]),
            onFailure: err => Result<IReadOnlyList<RefundRecord>, RefundError>.Failure(MapApiError(err)));
    }

    private async Task<Result<RefundResult, RefundError>> IssueAndAuditAsync(
        RefundApiRequest request,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.PolarOrderId);

        var apiResult = await _polarApi.CreateRefundAsync(request, ct).ConfigureAwait(false);
        if (apiResult.IsFailure)
        {
            return apiResult.Match(
                onSuccess: _ => Result<RefundResult, RefundError>.Failure(new RefundError(RefundErrorKind.PolarApiFailure, "Unreachable")),
                onFailure: err => Result<RefundResult, RefundError>.Failure(MapApiError(err)));
        }

        var response = apiResult.Match(onSuccess: r => r, onFailure: _ => throw new InvalidOperationException("Unreachable"));

        try
        {
            await RecordAuditEntryAsync(request, response, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Refund succeeded at Polar. Failing the audit insert would not roll the refund back —
            // log and surface success to the caller. The operator can reconcile via Polar's history.
            _logger.LogError(ex,
                "Refund {RefundId} succeeded at Polar but audit-log persistence failed. Refund stands; reconcile via Polar history.",
                response.RefundId);
        }

        return Result<RefundResult, RefundError>.Success(new RefundResult(
            response.RefundId, response.Amount, response.Currency, response.CreatedAt));
    }

    private async Task RecordAuditEntryAsync(RefundApiRequest request, RefundApiResponse response, CancellationToken ct)
    {
        var actor = _actorProvider.GetCurrentActor();
        var entry = new AdminAuditLogEntry
        {
            Id = Guid.NewGuid(),
            // TenantId is stamped by TenantAwareDbContextBase.StampNewEntities on SaveChanges.
            ActorUserId = actor.UserId,
            ActorEmail = actor.Email,
            EntityType = "Refund",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Create,
            OccurredAt = _time.GetUtcNow(),
            BeforeValues = null,
            AfterValues = null,
            ChangedFields = [
                $"PolarOrderId={request.PolarOrderId}",
                $"PolarRefundId={response.RefundId}",
                $"Amount={response.Amount}",
                $"Currency={response.Currency}",
                $"Reason={response.Reason}",
            ],
            CrossTenantAccess = actor.IsAppMasterAdmin && actor.CurrentTenantId is not null,
            CrossTenantJustification = null,
        };
        _db.AuditLog.Add(entry);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static RefundError? ValidateCommentRequirement(RefundReason reason, string? comment)
    {
        if (reason == RefundReason.Other && string.IsNullOrWhiteSpace(comment))
        {
            return new RefundError(
                RefundErrorKind.CommentRequired,
                "A comment is required when refunding with reason 'Other' so the audit log can record why.");
        }
        return null;
    }

    private static RefundError MapApiError(RefundApiError err) => err.Kind switch
    {
        RefundApiErrorKind.OrderNotFound => new RefundError(RefundErrorKind.OrderNotFound, err.Message),
        RefundApiErrorKind.AlreadyFullyRefunded => new RefundError(RefundErrorKind.AlreadyFullyRefunded, err.Message),
        RefundApiErrorKind.AmountExceedsRefundable => new RefundError(RefundErrorKind.AmountExceedsRefundable, err.Message),
        RefundApiErrorKind.CurrencyMismatch => new RefundError(RefundErrorKind.CurrencyMismatch, err.Message),
        _ => new RefundError(RefundErrorKind.PolarApiFailure, err.Message),
    };
}
