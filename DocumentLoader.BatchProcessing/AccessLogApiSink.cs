using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DocumentLoader.Models;

namespace DocumentLoader.BatchProcessing
{
    public class AccessLogApiSink : IAccessLogSink
    {
        private readonly HttpClient _client;

        public AccessLogApiSink(HttpClient client)
        {
            _client = client;
        }

        public async Task StoreDailyAccessAsync(
            int documentId,
            DateOnly date,
            int accessCount)
        {
            var dto = new DailyAccessDto
            {
                DocumentId = documentId,
                Date = date,
                AccessCount = accessCount
            };

            var response = await _client.PostAsJsonAsync(
                "api/accesses",
                dto);

            response.EnsureSuccessStatusCode();
        }
    }

}
