using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.BatchProcessing
{
    public class AccessBatchJob : IJob
    {
        private readonly AccessLogBatchProcessor _processor;
        public AccessBatchJob(AccessLogBatchProcessor processor)
        {
            _processor = processor;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            await _processor.RunOnceAsync();
        }
    }
}
