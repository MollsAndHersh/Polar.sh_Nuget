using System.Net;
using System.Text;
using PolarSharp.EcommerceStoreManagement.Translation;
using PolarSharp.EcommerceStoreManagement.Translation.Anthropic;
using PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI;
using PolarSharp.EcommerceStoreManagement.Translation.Gemini;
using PolarSharp.EcommerceStoreManagement.Translation.Grok;
using PolarSharp.EcommerceStoreManagement.Translation.OpenAI;

namespace PolarSharp.EcommerceStoreManagement.Translation.Tests;

/// <summary>
/// For each provider, captures the outgoing request via a stub HttpClient and verifies the
/// translator (a) sends the right URL + auth header, (b) packs the prompt into the right
/// JSON shape, (c) parses the provider-specific response shape back into our standard
/// dictionary. No real HTTP traffic to a provider — those live in <c>Category=Integration</c>.
/// </summary>
public sealed class ProviderRoundtripTests
{
    private static readonly IReadOnlyDictionary<string, string> Source = new Dictionary<string, string>
    {
        ["name"] = "Premium Headphones",
        ["description"] = "Crystal-clear sound.",
    };

    [Fact]
    public async Task Anthropic_translator_sends_to_messages_endpoint_with_x_api_key_header_and_parses_content_array()
    {
        var handler = new StubHandler(_ => Respond("""
            {"content":[{"type":"text","text":"{\"name\":\"Auriculares\",\"description\":\"Sonido claro\"}"}]}
            """));
        var translator = new AnthropicCatalogTranslator(new HttpClient(handler), "sk-ant-test", "claude-sonnet-4-6");

        var result = await translator.TranslateAsync(Source, "en-US", "es-MX");

        Assert.Equal("Auriculares", result["name"]);
        Assert.Equal("Sonido claro", result["description"]);
        Assert.Equal("https://api.anthropic.com/v1/messages", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("sk-ant-test", handler.LastRequest.Headers.GetValues("x-api-key").First());
        Assert.Equal("2023-06-01", handler.LastRequest.Headers.GetValues("anthropic-version").First());
    }

    [Fact]
    public async Task OpenAI_translator_sends_to_chat_completions_with_Bearer_auth_and_parses_choices()
    {
        var handler = new StubHandler(_ => Respond("""
            {"choices":[{"message":{"content":"{\"name\":\"Auriculares\"}"}}]}
            """));
        var translator = new OpenAiCatalogTranslator(new HttpClient(handler), "sk-test", "gpt-4o-mini");

        var result = await translator.TranslateAsync(Source, "en-US", "es-MX");

        Assert.Equal("Auriculares", result["name"]);
        Assert.EndsWith("/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task AzureOpenAI_translator_uses_api_key_header_and_deployment_path()
    {
        var handler = new StubHandler(_ => Respond("""
            {"choices":[{"message":{"content":"{\"name\":\"Auriculares\"}"}}]}
            """));
        var translator = new AzureOpenAiCatalogTranslator(
            new HttpClient(handler),
            apiKey: "azure-key",
            deployment: "gpt4o-prod",
            endpoint: "https://my-resource.openai.azure.com");

        var result = await translator.TranslateAsync(Source, "en-US", "es-MX");

        Assert.Equal("Auriculares", result["name"]);
        Assert.Contains("/openai/deployments/gpt4o-prod/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("api-version=", handler.LastRequest.RequestUri.Query);
        Assert.Equal("azure-key", handler.LastRequest.Headers.GetValues("api-key").First());
        Assert.Null(handler.LastRequest.Headers.Authorization);   // Azure uses key auth, NOT Bearer
    }

    [Fact]
    public async Task Gemini_translator_puts_api_key_in_query_string_and_parses_candidates()
    {
        var handler = new StubHandler(_ => Respond("""
            {"candidates":[{"content":{"parts":[{"text":"{\"name\":\"Auriculares\"}"}]}}]}
            """));
        var translator = new GeminiCatalogTranslator(new HttpClient(handler), "gemini-key", "gemini-2.5-flash");

        var result = await translator.TranslateAsync(Source, "en-US", "es-MX");

        Assert.Equal("Auriculares", result["name"]);
        Assert.Contains(":generateContent?key=gemini-key", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Grok_translator_targets_api_x_ai_with_Bearer_auth()
    {
        var handler = new StubHandler(_ => Respond("""
            {"choices":[{"message":{"content":"{\"name\":\"Auriculares\"}"}}]}
            """));
        var translator = new GrokCatalogTranslator(new HttpClient(handler), "xai-key", "grok-4-fast");

        var result = await translator.TranslateAsync(Source, "en-US", "es-MX");

        Assert.Equal("Auriculares", result["name"]);
        Assert.Equal("https://api.x.ai/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("xai-key", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Empty_source_fields_short_circuit_without_calling_HTTP()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Should not send HTTP for empty fields"));
        var translator = new AnthropicCatalogTranslator(new HttpClient(handler), "k", "m");

        var result = await translator.TranslateAsync(new Dictionary<string, string>(), "en", "es");
        Assert.Empty(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Provider_request_body_contains_the_translation_prompt_with_source_fields()
    {
        var handler = new StubHandler(_ => Respond("""{"choices":[{"message":{"content":"{}"}}]}"""));
        var translator = new OpenAiCatalogTranslator(new HttpClient(handler), "k", "m");

        await translator.TranslateAsync(Source, "en-US", "es-MX");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("Premium Headphones", body);
        Assert.Contains("Crystal-clear sound", body);
        Assert.Contains("en-US", body);
        Assert.Contains("es-MX", body);
    }

    private static HttpResponseMessage Respond(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json.Trim(), Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) { _respond = respond; }
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }
}
