using Fenestra.NativeHost.Abstractions;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

public sealed class X11EventState
{
    private readonly object _syncLock = new();
    private readonly uint _rootWindowId;
    private readonly Dictionary<uint, X11EventMask> _rootMasksByClientId = new();
    private readonly Dictionary<uint, Queue<QueuedX11Event>> _eventsByClientId = new();
    private readonly Queue<NativeInputEvent> _pendingInputEvents = new();

    public X11EventState(uint rootWindowId)
    {
        _rootWindowId = rootWindowId;
        FocusWindowId = rootWindowId;
    }

    public uint FocusWindowId { get; private set; }
    public byte RevertTo { get; private set; } = 0;

    public void SetSelection(uint clientId, uint windowId, uint eventMask)
    {
        lock (_syncLock)
        {
            if (windowId != _rootWindowId)
            {
                return;
            }

            _rootMasksByClientId[clientId] = (X11EventMask)eventMask;
        }
    }

    public void SetInputFocus(uint windowId)
    {
        lock (_syncLock)
        {
            FocusWindowId = windowId;
        }
    }

    public void EnqueueInputEvent(NativeInputEvent inputEvent)
    {
        lock (_syncLock)
        {
            _pendingInputEvents.Enqueue(inputEvent);
        }
    }

    public void BroadcastExpose(ushort sequenceNumber, uint windowId, short x, short y, ushort width, ushort height)
    {
        lock (_syncLock)
        {
            EnqueueForSubscribers(
                windowId,
                new QueuedX11Event(
                    Kind: X11EventKind.Expose,
                    RequiredMask: X11EventMask.Exposure,
                    SequenceNumber: sequenceNumber,
                    Detail: 0,
                    RootWindowId: _rootWindowId,
                    EventWindowId: windowId,
                    RootX: x,
                    RootY: y,
                    EventX: x,
                    EventY: y,
                    State: 0,
                    Width: width,
                    Height: height,
                    Time: 0));
        }
    }

    public void TranslatePendingInputEvents(ushort sequenceNumber)
    {
        lock (_syncLock)
        {
            while (_pendingInputEvents.Count > 0)
            {
                var inputEvent = _pendingInputEvents.Dequeue();
                var translated = Translate(inputEvent, sequenceNumber);
                if (translated is null)
                {
                    continue;
                }

                EnqueueForSubscribers(inputEvent.WindowId, translated.Value);
            }
        }
    }

    public IReadOnlyList<QueuedX11Event> DrainEventsForClient(uint clientId)
    {
        lock (_syncLock)
        {
            if (!_eventsByClientId.TryGetValue(clientId, out var queue) || queue.Count == 0)
            {
                return [];
            }

            var events = new List<QueuedX11Event>(queue.Count);
            while (queue.Count > 0)
            {
                events.Add(queue.Dequeue());
            }

            return events;
        }
    }

    private void EnqueueForSubscribers(uint windowId, QueuedX11Event serverEvent)
    {
        foreach (var (clientId, eventMask) in _rootMasksByClientId)
        {
            if (windowId != _rootWindowId || (eventMask & serverEvent.RequiredMask) == 0)
            {
                continue;
            }

            if (!_eventsByClientId.TryGetValue(clientId, out var queue))
            {
                queue = new Queue<QueuedX11Event>();
                _eventsByClientId[clientId] = queue;
            }

            queue.Enqueue(serverEvent);
        }
    }

    private QueuedX11Event? Translate(NativeInputEvent inputEvent, ushort sequenceNumber)
    {
        var (eventKind, requiredMask) = inputEvent.Kind switch
        {
            NativeInputEventKind.KeyDown => (X11EventKind.KeyPress, X11EventMask.KeyPress),
            NativeInputEventKind.KeyUp => (X11EventKind.KeyRelease, X11EventMask.KeyRelease),
            NativeInputEventKind.ButtonDown => (X11EventKind.ButtonPress, X11EventMask.ButtonPress),
            NativeInputEventKind.ButtonUp => (X11EventKind.ButtonRelease, X11EventMask.ButtonRelease),
            NativeInputEventKind.PointerMove => (X11EventKind.MotionNotify, X11EventMask.PointerMotion),
            NativeInputEventKind.FocusIn => (X11EventKind.FocusIn, X11EventMask.FocusChange),
            NativeInputEventKind.FocusOut => (X11EventKind.FocusOut, X11EventMask.FocusChange),
            _ => ((X11EventKind?)null, X11EventMask.None)
        };

        if (eventKind is null)
        {
            return null;
        }

        return new QueuedX11Event(
            Kind: eventKind.Value,
            RequiredMask: requiredMask,
            SequenceNumber: sequenceNumber,
            Detail: (byte)inputEvent.Detail,
            RootWindowId: _rootWindowId,
            EventWindowId: inputEvent.WindowId,
            RootX: inputEvent.X,
            RootY: inputEvent.Y,
            EventX: inputEvent.X,
            EventY: inputEvent.Y,
            State: inputEvent.State,
            Width: 0,
            Height: 0,
            Time: 0);
    }

    public FocusStateSnapshot GetFocusState()
    {
        lock (_syncLock)
        {
            return new FocusStateSnapshot(FocusWindowId, RevertTo);
        }
    }
}

public readonly record struct FocusStateSnapshot(
    uint FocusWindowId,
    byte RevertTo);
