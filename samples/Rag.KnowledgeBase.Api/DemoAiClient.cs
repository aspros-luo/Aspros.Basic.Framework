using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Rag.KnowledgeBase.Api;

public sealed class DemoAiOptions
{
    public DemoAiEndpointOptions Embedding { get; set; } = new();
    public DemoAiEndpointOptions Chat { get; set; } = new();
}

public sealed class DemoAiEndpointOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string ApiKey { get; set; } = "ollama";
    public string Model { get; set; } = string.Empty;
}

public sealed class DemoAiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<DemoAiOptions> options)
{
    public string EmbeddingModelKey =>
        $"{GetEndpointKey(options.Value.Embedding)}";

    public string ChatModelKey =>
        $"{GetEndpointKey(options.Value.Chat)}";

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var endpoint = options.Value.Embedding;
        Validate(endpoint, "Embedding");
        using var request = CreateRequest(endpoint, "embeddings");
        request.Content = JsonContent.Create(new
        {
            model = endpoint.Model,
            input = inputs
        });

        using var response = await httpClientFactory.CreateClient(nameof(DemoAiClient))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var data = json.RootElement.GetProperty("data").EnumerateArray()
            .OrderBy(item => item.GetProperty("index").GetInt32())
            .Select(item => item.GetProperty("embedding").EnumerateArray()
                .Select(value => value.GetSingle()).ToArray())
            .ToArray();

        if (data.Length != inputs.Count || data.Any(vector => vector.Length == 0))
        {
            throw new InvalidOperationException("The embedding provider returned an invalid vector batch.");
        }

        var dimension = data[0].Length;
        if (data.Any(vector => vector.Length != dimension))
        {
            throw new InvalidOperationException("The embedding provider returned inconsistent vector dimensions.");
        }

        return data;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var endpoint = options.Value.Chat;
        Validate(endpoint, "Chat");
        using var request = CreateRequest(endpoint, "chat/completions");
        request.Content = JsonContent.Create(new
        {
            model = endpoint.Model,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        using var response = await httpClientFactory.CreateClient(nameof(DemoAiClient))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return json.RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString()
            ?? throw new InvalidOperationException("The chat provider returned an empty response.");
    }

    private static HttpRequestMessage CreateRequest(DemoAiEndpointOptions endpoint, string path)
    {
        var baseUrl = endpoint.BaseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{path}");
        if (!string.IsNullOrWhiteSpace(endpoint.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
        }

        return request;
    }

    private static void Validate(DemoAiEndpointOptions endpoint, string kind)
    {
        if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out _) ||
            string.IsNullOrWhiteSpace(endpoint.Model))
        {
            throw new InvalidOperationException(
                $"AI {kind} endpoint is not configured. Set BaseUrl and Model in appsettings or environment variables.");
        }
    }

    private static string GetEndpointKey(DemoAiEndpointOptions endpoint) =>
        $"{endpoint.BaseUrl.TrimEnd('/')}:{endpoint.Model}";
}
