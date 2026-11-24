using System.IO.Pipes;
using System.Text.Json;
using Sona.Core;
namespace Sona.App.Windows;

public sealed class AgentServer(Func<AgentRequest, CancellationToken, Task<AgentReply>> handle) : IAsyncDisposable
{
    private const int ListenerPoolSize = 4;
    private readonly CancellationTokenSource stop = new();
    private Task? loop;
    public void Start() => loop = Task.WhenAll(Enumerable.Range(0, ListenerPoolSize).Select(_ => RunAsync()));
    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(AgentProtocol.PipeName, PipeDirection.InOut, ListenerPoolSize, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
                var request = await AgentProtocol.ReadAsync<AgentRequest>(pipe, deadline.Token);
                AgentReply reply;
                try { reply = await handle(request, deadline.Token); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or JsonException) { reply = new(false, Error: "The operation could not be completed. Check the request and Sona's current state."); }
                await AgentProtocol.WriteAsync(pipe, reply, deadline.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException) { /* Malformed/disconnected clients cannot stop the local service or expose their input. */ }
        }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        if (loop != null)
        {
            try { await loop; }
            catch (OperationCanceledException) { }
        }
        stop.Dispose();
    }
}
