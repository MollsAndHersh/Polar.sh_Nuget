using PolarSharp.EcommerceStoreManagement.Translation;

namespace PolarSharp.EcommerceStoreManagement.Translation.Tests;

/// <summary>
/// Verifies the prompt builder and JSON response parser — the shared core every provider
/// package uses. Defects here would break translation across every provider.
/// </summary>
public sealed class TranslationPromptTests
{
    [Fact]
    public void Build_includes_source_target_languages_and_strict_json_instruction()
    {
        var fields = new Dictionary<string, string> { ["name"] = "Premium Headphones" };
        var prompt = TranslationPrompt.Build(fields, "en-US", "es-MX");

        Assert.Contains("en-US", prompt);
        Assert.Contains("es-MX", prompt);
        Assert.Contains("Premium Headphones", prompt);
        Assert.Contains("JSON", prompt);
        Assert.DoesNotContain("```", prompt);   // no markdown fences in our prompt
    }

    [Fact]
    public void Build_throws_on_blank_master_or_target_language()
    {
        var fields = new Dictionary<string, string> { ["k"] = "v" };
        Assert.Throws<ArgumentException>(() => TranslationPrompt.Build(fields, "", "es-MX"));
        Assert.Throws<ArgumentException>(() => TranslationPrompt.Build(fields, "en", "  "));
    }

    [Fact]
    public void Parse_extracts_expected_keys_from_clean_json()
    {
        var response = """{"name":"Auriculares premium","description":"Sonido cristalino"}""";
        var result = TranslationPrompt.ParseJsonResponse(response, ["name", "description"]);

        Assert.Equal(2, result.Count);
        Assert.Equal("Auriculares premium", result["name"]);
        Assert.Equal("Sonido cristalino", result["description"]);
    }

    [Fact]
    public void Parse_strips_markdown_json_fences()
    {
        var response = """
            ```json
            {"name":"Auriculares premium"}
            ```
            """;
        var result = TranslationPrompt.ParseJsonResponse(response, ["name"]);
        Assert.Equal("Auriculares premium", result["name"]);
    }

    [Fact]
    public void Parse_strips_plain_triple_backtick_fences()
    {
        var response = """
            ```
            {"name":"Auriculares"}
            ```
            """;
        var result = TranslationPrompt.ParseJsonResponse(response, ["name"]);
        Assert.Equal("Auriculares", result["name"]);
    }

    [Fact]
    public void Parse_filters_out_hallucinated_fields_not_in_expected_set()
    {
        // The AI returned a key the caller didn't ask for. Drop it silently to avoid storing
        // junk that the host can't display.
        var response = """{"name":"Auriculares","extra":"hallucinated content","description":"Desc"}""";
        var result = TranslationPrompt.ParseJsonResponse(response, ["name", "description"]);

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey("extra"));
    }

    [Fact]
    public void Parse_returns_empty_on_malformed_json()
    {
        var result = TranslationPrompt.ParseJsonResponse("this is not json at all", ["name"]);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_returns_empty_on_blank_input()
    {
        Assert.Empty(TranslationPrompt.ParseJsonResponse("", ["name"]));
        Assert.Empty(TranslationPrompt.ParseJsonResponse("   ", ["name"]));
        Assert.Empty(TranslationPrompt.ParseJsonResponse(null!, ["name"]));
    }

    [Fact]
    public void Parse_skips_non_string_values_in_response()
    {
        var response = """{"name":"Real","count":42,"flag":true}""";
        var result = TranslationPrompt.ParseJsonResponse(response, ["name", "count", "flag"]);
        Assert.Single(result);
        Assert.Equal("Real", result["name"]);
    }
}
