using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.BatchProcessing
{
    [DisallowConcurrentExecution]
    public class AccessBatchJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var processor = context.JobDetail.JobDataMap.Get("processor") as AccessLogBatchProcessor;
            Console.WriteLine($"AccessBatchJob triggered at{DateTime.Now}");

            if (processor != null)
            {
                await processor.RunOnceAsync();
            }
        }
    }
}
