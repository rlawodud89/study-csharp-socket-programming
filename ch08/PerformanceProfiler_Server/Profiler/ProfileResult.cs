using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Profiler;

public class ProfileResult
{
    public string Name { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public long MemoryUsage { get; set; }

    public DateTime Time { get; set; }
}
