using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Profiler;

public class LogWriter
{
    private readonly string _path;

    public LogWriter()
    {
        _path = Path.Combine(AppContext.BaseDirectory, "Log", "log.txt");

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Write(ProfileResult result)
    {
        string log =
            $"[{result.Time}] " +
            $"{result.Name} | " +
            $"Time : {result.ElapsedMilliseconds} ms | " +
            $"Memory : {result.MemoryUsage} bytes";

        File.AppendAllText(_path, log + Environment.NewLine);
    }
}
