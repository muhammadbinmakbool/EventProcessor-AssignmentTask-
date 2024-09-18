using System;
using System.Collections.Concurrent;

class Event
{
    private int id;
    private String type;
    private Object data;

    public int getId() => id;
    public String getEventType() => type;
    public Object getData() => data;

    public Event(int id, string type, Object data)
    {
        this.id = id;
        this.type = type;
        this.data = data;
    }
}

interface EventListener
{
    void OnEventReceived(Event evnt);
}

class FixedDelayStrategy
{
    private readonly int _delayMilliseconds;

    public FixedDelayStrategy(int delayMilliseconds)
    {
        _delayMilliseconds = delayMilliseconds;
    }

    public async Task<bool> Execute(Func<bool> action, int maxAttempts)
    {
        int attempts = 0;
        while (attempts < maxAttempts)
        {
            attempts++;
            if (action())
            {
                return true;
            }

            Console.WriteLine($"Failed attempt {attempts}. Retrying in {_delayMilliseconds}ms...");
            await Task.Delay(_delayMilliseconds);
        }
        return false;
    }
}

class EventProcessor
{
    private ConcurrentQueue<Event> _eventQueue = new ConcurrentQueue<Event>();
    private Dictionary<string, Func<Event, bool>> _event_handlers = new Dictionary<string, Func<Event, bool>>();
    private bool _running = false;
    private FixedDelayStrategy _retryStrategy;

    public EventProcessor(FixedDelayStrategy retryStrategy)
    {
        _retryStrategy = retryStrategy;
    }

    public void StartProcessing()
    {
        _running = true;
        Task.Run(() => ProcessEvents());
    }

    public void StopProcessing()
    {
        _running = false;
    }

    private async Task ProcessEvents()
    {
        while (_running)
        {
            if (_eventQueue.TryDequeue(out Event? evnt))
            {
                string eventType = evnt.getEventType();
                if (_event_handlers.ContainsKey(eventType))
                {
                    Console.WriteLine($"Processing event {evnt.getId()} of type {eventType}");

                    bool success = await _retryStrategy.Execute(() => _event_handlers[eventType](evnt), 5);
                    if (success)
                    {
                        Console.WriteLine($"Successfully processed event {evnt.getId()}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to process event {evnt.getId()} after retries");
                    }
                }
                else
                {
                    Console.WriteLine($"No handler found for event type {eventType}");
                }
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    public void EnqueueEvent(Event evnt)
    {
        _eventQueue.Enqueue(evnt);
        Console.WriteLine($"Event {evnt.getId()} enqueued.");
    }

    public void RegisterEventHandler(string eventType, Func<Event, bool> handler)
    {
        _event_handlers[eventType] = handler;
        Console.WriteLine($"Handler registered for event type {eventType}");
    }
}

class EventProducer
{
    private EventListener _eventListener;

    public EventProducer(EventListener eventListener)
    {
        _eventListener = eventListener;
    }

    public void Start()
    {
        _eventListener.OnEventReceived(new Event(1, "log", "Log line 1"));
        _eventListener.OnEventReceived(new Event(2, "error", "Error code 404"));
        _eventListener.OnEventReceived(new Event(3, "log", "Log line 3"));
        _eventListener.OnEventReceived(new Event(4, "alert", "Alert: Disk usage high"));
    }
}

class AssignmentEventListener : EventListener
{
    private EventProcessor _processor;

    public AssignmentEventListener(EventProcessor processor)
    {
        _processor = processor;
    }

    public void OnEventReceived(Event evnt)
    {
        _processor.EnqueueEvent(evnt);
    }
}

class Program
{
    static void Main(string[] args)
    {
        FixedDelayStrategy retryStrategy = new FixedDelayStrategy(1000);

        EventProcessor processor = new EventProcessor(retryStrategy);
        AssignmentEventListener listener = new AssignmentEventListener(processor);
        EventProducer producer = new EventProducer(listener);

        processor.RegisterEventHandler("log", evnt =>
        {
            Console.WriteLine($"Processing log event: {evnt.getData()}");
            return true;
        });

        processor.RegisterEventHandler("error", evnt =>
        {
            Console.WriteLine($"Processing error event: {evnt.getData()}");
            return evnt.getId() % 2 != 0;
        });

        processor.RegisterEventHandler("alert", evnt =>
        {
            Console.WriteLine($"Processing alert event: {evnt.getData()}");
            return true;
        });

        processor.StartProcessing();
        producer.Start();

        Thread.Sleep(10000);
        processor.StopProcessing();
    }
}