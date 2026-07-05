using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Profiler;

public class PerformanceProfiler
{
    private readonly LogWriter _logWriter;

    public PerformanceProfiler()
    {
        _logWriter = new LogWriter();
    }

    public void Measure(string name, Action action)
    {
        long before = GC.GetTotalMemory(false); // 메서드 실행 전 메모리 사용량 측정

        Stopwatch sw = Stopwatch.StartNew(); // 시간 측정 시작

        action(); // 메서드 실행

        sw.Stop(); // 시간 측정 종료

        long after = GC.GetTotalMemory(false); // 메서드 실행 후 메모리 사용량 측정

        _logWriter.Write(new ProfileResult
            {
                Name = name,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                MemoryUsage = after - before,
                Time = DateTime.Now
            }
        );
    }

    public T Measure<T>(string name, Func<T> func)
    {
        long before = GC.GetTotalMemory(false); // 메서드 실행 전 메모리 사용량 측정

        Stopwatch sw = Stopwatch.StartNew(); // 시간 측정 시작

        T result = func(); // 메서드 실행

        sw.Stop(); // 시간 측정 종료

        long after = GC.GetTotalMemory(false); // 메서드 실행 후 메모리 사용량 측정

        _logWriter.Write(new ProfileResult
        {
            Name = name,
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            MemoryUsage = after - before,
            Time = DateTime.Now
        }
        );

        return result;
    }


    public async Task MeasureAsync(string name, Func<Task> action)
    {
        long before = GC.GetTotalMemory(false); // 메서드 실행 전 메모리 사용량 측정

        Stopwatch sw = Stopwatch.StartNew(); // 시간 측정 시작

        await action(); // 메서드 실행

        sw.Stop(); // 시간 측정 종료

        long after = GC.GetTotalMemory(false); // 메서드 실행 후 메모리 사용량 측정

        _logWriter.Write(new ProfileResult
            {
                Name = name,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                MemoryUsage = after - before,
                Time = DateTime.Now
            }
        );
    }

    public async Task<T> MeasureAsync<T>(string name, Func<Task<T>> action)
    {
        long before = GC.GetTotalMemory(false);

        Stopwatch sw = Stopwatch.StartNew();

        T result = await action();

        sw.Stop();

        long after = GC.GetTotalMemory(false);

        _logWriter.Write(new ProfileResult
            {
                Name = name,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                MemoryUsage = after - before,
                Time = DateTime.Now
            }
        );

        return result;
    }
}
