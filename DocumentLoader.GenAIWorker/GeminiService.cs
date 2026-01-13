using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentLoader.GenAIWorker
{
    public class GeminiService
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new Exception("API key missing");
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(ILogger<GeminiService> logger)
        {
            _logger = logger;
        }

        public async Task<string> SendPromptAsync(string text, int maxRetries = 3)
        {
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    topK = 40,
                    topP = 0.9,
                    maxOutputTokens = 512
                }
            };

            var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("X-goog-api-key", _apiKey);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _http.PostAsync(
                        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
                        body
                    );

                    var json = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("[GeminiService] API Response: {json}", json);

                    using var doc = JsonDocument.Parse(json);

                    // 1️⃣ Check for quota/retry instructions
                    if (doc.RootElement.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var detail in details.EnumerateArray())
                        {
                            if (detail.TryGetProperty("@type", out var type) &&
                                type.GetString()?.Contains("RetryInfo") == true &&
                                detail.TryGetProperty("retryDelay", out var retryDelayProp))
                            {
                                var retryDelayStr = retryDelayProp.GetString() ?? "5s";
                                if (!TimeSpan.TryParse(retryDelayStr, out var retryDelay))
                                    retryDelay = TimeSpan.FromSeconds(5);

                                _logger.LogWarning(
                                    "[GeminiService] Quota/retry limit hit. Waiting {delay} before retry #{attempt}",
                                    retryDelay, attempt
                                );
                                await Task.Delay(retryDelay);
                                continue; // retry this attempt
                            }
                        }
                    }

                    // 2️⃣ Parse the actual summary text
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                        candidates.ValueKind == JsonValueKind.Array &&
                        candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];

                        if (firstCandidate.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) &&
                            parts.ValueKind == JsonValueKind.Array &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var textElement))
                        {
                            return textElement.GetString() ?? "";
                        }
                    }

                    // 3️⃣ If parsing fails, log and return empty string (no retry)
                    _logger.LogError("[GeminiService] API response did not contain expected keys. Returning empty summary.");
                    return "";
                }
                catch (HttpRequestException ex)
                {
                    // Retry only network/API errors
                    _logger.LogError(ex, "[GeminiService] Network/API error (attempt {attempt})", attempt);
                    if (attempt < maxRetries)
                    {
                        _logger.LogInformation("[GeminiService] Retrying in 2s...");
                        await Task.Delay(2000);
                        continue;
                    }
                    throw; // fail after last attempt
                }
                catch (JsonException ex)
                {
                    // Parsing errors should not retry
                    _logger.LogError(ex, "[GeminiService] Failed to parse API response. Returning empty summary.");
                    return "";
                }
            }

            // Fallback
            return "";
        }
    }
}
