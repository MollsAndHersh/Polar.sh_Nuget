namespace PolarSharp.Webhooks.Reconciliation;

/// <summary>Internal checkpoint data record for JSON file persistence.</summary>
internal sealed record WebhookInternalCheckpointData(DateTimeOffset LastProcessed);
