using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Live.Models;
using Live.Utilities;

namespace Live.Services;

public class StateManager : IDisposable
{
    private readonly ILogger<StateManager> _logger;
    private readonly F1Client _f1Client;
    private readonly object _stateLock = new();
    private JsonObject _state = new();
    private readonly Channel<Message> _broadcastChannel;
    private readonly List<Channel<Message>> _subscribers = new();
    private readonly object _subscribersLock = new();
    private CancellationTokenSource? _cts;
    private Task? _managerTask;

    public StateManager(ILogger<StateManager> logger, F1Client f1Client)
    {
        _logger = logger;
        _f1Client = f1Client;
        _broadcastChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _managerTask = Task.Run(() => ManageAsync(_cts.Token));
    }

    private async Task ManageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _f1Client.ConnectAndStreamAsync(cancellationToken))
            {
                ProcessMessage(message);
                await BroadcastAsync(message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("State manager stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in state manager");
        }
    }

    private void ProcessMessage(Message message)
    {
        lock (_stateLock)
        {
            switch (message)
            {
                case Message.Initial initial:
                    if (initial.Data is JsonObject obj)
                    {
                        _state = obj.DeepClone().AsObject();
                    }
                    break;

                case Message.Updates updates:
                    foreach (var (topic, data) in updates.Items)
                    {
                        JsonMerge.MergeInto(_state, topic, data);
                    }
                    break;
            }
        }
    }

    private async Task BroadcastAsync(Message message)
    {
        List<Channel<Message>> subscribersCopy;
        lock (_subscribersLock)
        {
            subscribersCopy = _subscribers.ToList();
        }

        foreach (var subscriber in subscribersCopy)
        {
            try
            {
                await subscriber.Writer.WriteAsync(message);
            }
            catch (ChannelClosedException)
            {
                lock (_subscribersLock)
                {
                    _subscribers.Remove(subscriber);
                }
            }
        }
    }

    public (JsonObject State, Channel<Message> Updates) Subscribe()
    {
        var channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        JsonObject stateCopy;
        lock (_stateLock)
        {
            stateCopy = _state.DeepClone().AsObject();
        }

        lock (_subscribersLock)
        {
            _subscribers.Add(channel);
        }

        var connectionCount = 0;
        lock (_subscribersLock)
        {
            connectionCount = _subscribers.Count;
        }
        _logger.LogInformation("New SSE connection, total connections: {Count}", connectionCount);

        return (stateCopy, channel);
    }

    public void Unsubscribe(Channel<Message> channel)
    {
        lock (_subscribersLock)
        {
            _subscribers.Remove(channel);
        }
        channel.Writer.TryComplete();
    }

    public JsonNode? GetDriverList()
    {
        lock (_stateLock)
        {
            return _state["driverList"]?.DeepClone();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _managerTask?.Wait(TimeSpan.FromSeconds(5));
    }
}
