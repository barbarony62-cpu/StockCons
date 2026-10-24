using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace AskCons;

public sealed class LocalInterpreter
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:11434/"),
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<string> TestConnection(CancellationToken token)
    {
        using var response = await Http.GetAsync("api/tags", token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama HTTP {(int)response.StatusCode}: {Truncate(body)}");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ollama did not return a valid models list.");
        if (!models.EnumerateArray().Any(m => m.TryGetProperty("name", out var name) &&
                name.GetString()?.StartsWith("qwen3:4b", StringComparison.OrdinalIgnoreCase) == true))
            throw new InvalidOperationException("Ollama is reachable, but qwen3:4b is not installed. Run: ollama pull qwen3:4b");
        return "Ollama is reachable and qwen3:4b is installed.";
    }

    public async Task<QueryIntent> Interpret(string question, CancellationToken token)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var prompt = $"""
            You are a strict parser for a local portfolio application. Today is {today}.
            Return one JSON object only, with these keys:
            operation: account_change, account_value, holding_change, holding_value, top_holding_changes, bottom_holding_changes, or unsupported
            security: string or null
            start_date: YYYY-MM-DD or null
            end_date: YYYY-MM-DD or null
            language: en or he
            'My Tesla investment' refers to the holding, not market share-price performance.
            'Best five stocks' / 'top stocks by money' means top_holding_changes (absolute NIS difference between holding values).
            'Worst five stocks' means bottom_holding_changes. Do NOT calculate transaction-adjusted profit.
            For rankings without a period, default to January 1 this year through today. If a year is specified, use January 1 of that year. Respect restrictions such as only holdings that existed on the first recorded day of the year; the application applies this filter.
            For changes provide start_date and end_date; for value provide end_date.
            'Since January 1' means January 1 of this year; 'six months' means six calendar months ago.
            Do not invent values or SQL. If unclear, choose unsupported.
            Question: {question}
            """;
        var request = new
        {
            model = "qwen3:4b",
            stream = false,
            think = false,
            format = "json",
            options = new { temperature = 0, num_predict = 350 },
            messages = new[] { new { role = "user", content = prompt } }
        };
        using var response = await Http.PostAsJsonAsync("api/chat", request, token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama HTTP {(int)response.StatusCode}: {Truncate(body)}");
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Ollama returned an empty HTTP response.");
        using var envelope = ParseJson(body, "Ollama response");
        if (!envelope.RootElement.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Ollama response does not contain message.content: " + Truncate(body));
        var answer = content.GetString();
        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidOperationException("Qwen returned no answer text. Check model logs and retry.");
        answer = answer.Trim();
        // Some models wrap structured responses in Markdown fences despite format=json.
        if (answer.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = answer.IndexOf('\n');
            var lastFence = answer.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
                answer = answer[(firstNewline + 1)..lastFence].Trim();
        }
        using var parsed = ParseJson(answer, "Qwen answer");
        var intent = JsonSerializer.Deserialize<QueryIntent>(parsed.RootElement.GetRawText());
        return intent ?? throw new InvalidOperationException("Qwen returned a null interpretation.");
    }

    private static JsonDocument ParseJson(string value, string label)
    {
        try { return JsonDocument.Parse(value); }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{label} is not valid JSON: {Truncate(value)}", ex);
        }
    }

    private static string Truncate(string? value) => value is null ? "(null)" : value.Length <= 400 ? value : value[..400] + "…";
}
