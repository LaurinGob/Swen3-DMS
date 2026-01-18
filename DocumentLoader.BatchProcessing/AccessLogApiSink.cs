using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DocumentLoader.Models;

namespace DocumentLoader.BatchProcessing //sends access logs to an API endpoint
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
            var response = await _client.PostAsJsonAsync(
                "api/Documents/accesses",
                accesses);

            response.EnsureSuccessStatusCode();
        }
    }

}
