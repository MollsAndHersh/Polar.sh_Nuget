using System.Text.Json;
using System.Text.Json.Serialization;
using PolarSharp.MultiTenant.Notifications.Channels;

namespace PolarSharp.MultiTenant.Notifications.Serialization;

/// <summary>
/// STJ source-generated serialization context for outbound notification payloads
/// (SendGrid mail-send request, webhook payload). Required for Native AOT and IL trimming
/// compatibility — every type that crosses System.Text.Json must be listed here.
/// </summary>
[JsonSerializable(typeof(SendGridMailRequest))]
[JsonSerializable(typeof(WebhookPayload))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
internal sealed partial class NotificationJsonContext : JsonSerializerContext
{
}
