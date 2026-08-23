using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace AskMyBooks.Infrastructure.Llm;

/// <summary>
/// الكلاس الوحيد في المشروع اللي بيعرف شكل الـ API بتاع Ollama.
/// لو حبيت تستبدله بـ OpenAI مثلاً، اكتب كلاس تاني بينفذ ILlmClient وغيّر التسجيل بس.
/// </summary>
public sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public OllamaLlmClient(HttpClient http, IOptions<LlmOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var request = new OllamaEmbedRequest(_options.EmbeddingModel, text);
        var response = await SendAsync("/api/embeddings", request, ct);

        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: ct);
        if (payload?.Embedding is null || payload.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama returned an empty embedding.");

        return payload.Embedding;
    }

    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        => ChatAsync(system, user, jsonMode: false, ct);

    public async Task<string> CompleteJsonAsync(string system, string user, CancellationToken ct)
    {
        var raw = await ChatAsync(system, user, jsonMode: true, ct);

        // بنتأكد من الـ JSON هنا على الحدود، فأي حد بينادي الميثود دي
        // مضمونله إنها ترجّع JSON سليم أو ترمي LlmContractException.
        try
        {
            using var _ = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new LlmContractException("The model did not return valid JSON.", raw);
        }

        return raw;
    }

    private async Task<string> ChatAsync(string system, string user, bool jsonMode, CancellationToken ct)
    {
        var request = new OllamaChatRequest(
            Model: _options.ChatModel,
            Messages:
            [
                new OllamaMessage("system", system),
                new OllamaMessage("user", user)
            ],
            Stream: false,
            Format: jsonMode ? "json" : null);

        var response = await SendAsync("/api/chat", request, ct);

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);
        var content = payload?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            throw new LlmContractException("The model returned an empty response.", content);

        return content.Trim();
    }

    private async Task<HttpResponseMessage> SendAsync<TRequest>(string path, TRequest body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(path, body, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama at {_http.BaseAddress}{path.TrimStart('/')}. Is Ollama running?", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Ollama returned {(int)response.StatusCode} for {path}: {errorBody}");
        }

        return response;
    }

    // --- DTOs خاصة بشكل الـ API بتاع Ollama، private ومش بتخرج بره الكلاس ---

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] OllamaMessage[] Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format")] string? Format);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message);
}