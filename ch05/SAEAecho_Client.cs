using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static async Task Main()
    {
        int clientCount = 1000;

        for (int i = 0; i < clientCount; i++)
        {
            int id = i;
            _ = Task.Run(() => RunClient(id));
        }

        Console.ReadLine(); // 엔터 치기 전까지 메인 프로세스 살려둠 -> 백그라운드 Task 계속 실행됨
    }

    static async Task RunClient(int id)
    {
        try
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 5000);

            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];

            string msg = $"{id}";
            byte[] data = Encoding.UTF8.GetBytes(msg);

            await stream.WriteAsync(data, 0, data.Length);

            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer, 0, read);

            Console.WriteLine($"Client {id}: {response}");

            await Task.Delay(1000);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client {id} error: {ex.Message}");
        }
    }
}