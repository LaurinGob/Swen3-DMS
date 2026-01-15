using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentLoader.BatchProcessing
{
    public interface IAccessLogSink
    {
        Task StoreDailyAccessAsync(
            int documentId,
            DateOnly batchDate,
            int accessCount);
    }
}
