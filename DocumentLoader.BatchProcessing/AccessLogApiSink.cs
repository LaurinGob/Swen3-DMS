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
        public async Task StoreBatchAsync(List<DailyAccessDto> accesses)
        {
            // This sends the entire list as a JSON array in one request
            var response = await _client.PostAsJsonAsync(
                "api/Documents/accesses",
                accesses);

            // This ensures that if the API returns 400 (Bad Request) or 500,
            // an exception is thrown so the file moves to the Error folder.
            response.EnsureSuccessStatusCode();
        }
    }

}
