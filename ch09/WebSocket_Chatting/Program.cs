using Study_WebServer.Network;
using Study_WebServer.Worker;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<PacketQueue>();
builder.Services.AddSingleton<PacketHandler>();
builder.Services.AddSingleton<WorkerService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<ChatHub>("/chat");

// Worker Thread Ω√¿€
var worker = app.Services.GetRequiredService<WorkerService>();
worker.Start();

app.Run();