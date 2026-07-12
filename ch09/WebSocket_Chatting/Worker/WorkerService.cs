
namespace Study_WebServer.Worker;

public class WorkerService
{
    private readonly PacketQueue _queue;
    private readonly PacketHandler _handler;

    public WorkerService(PacketQueue queue,
                         PacketHandler handler)
    {
        _queue = queue;
        _handler = handler;
    }

    public void Start()
    {
        Task.Run(ProcessLoop);
    }

    private void ProcessLoop()
    {
        while (true)
        {
            var (session, packet) = _queue.Pop();

            _handler.Handle(session, packet);
        }
    }
}
