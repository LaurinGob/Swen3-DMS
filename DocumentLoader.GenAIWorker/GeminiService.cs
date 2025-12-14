using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentLoader.GenAIWorker
{
    public class GeminiService
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new Exception("API key missing");

        public async Task<string> SendPromptAsync(string text)
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
                    temperature = 0.7,      // creativity vs. determinism (higher = more random) (how creative the answer is)
                    topK = 40,              // sample from topK most likely tokens (how many word options are considered)
                    topP = 0.9,             // nucleus sampling, cumulative probability limit (how wide the choice range is)
                    maxOutputTokens = 512   // maximum length of the model output (how long the answer can be)
                }
            };

            var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("X-goog-api-key", _apiKey);

            var response = await _http.PostAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent", body);
            
            var json = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";

        }



    }
}
