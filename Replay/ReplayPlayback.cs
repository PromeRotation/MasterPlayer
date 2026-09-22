using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

internal sealed class ReplayPlayback(ReplayRecording recording)
{
    public ReplayWorldState World { get; } = new() { CurrentTime = recording.Header.StartedUtc.ToUniversalTime() };
    public double Clock { get; private set; }
    public int NextEventIndex { get; private set; }
    private int _nextFrame;

    public void AdvanceTo(double destination, Action<LogSystemEvent> dispatch, Action<float> update)
    {
        if (!double.IsFinite(destination) || destination < Clock) throw new ArgumentOutOfRangeException(nameof(destination));
        destination = Math.Min(destination, recording.Duration);
        while (true)
        {
            var ev = NextEventIndex < recording.Events.Count ? recording.Events[NextEventIndex] : null;
            var frame = _nextFrame < recording.WorldFrames.Count ? recording.WorldFrames[_nextFrame] : null;
            var takeFrame = frame != null && (ev == null || frame.T < ev.T || (frame.T == ev.T && frame.Sequence <= ev.Sequence));
            var time = takeFrame ? frame!.T : ev?.T;
            if (time == null || time > destination) break;
            var delta = MoveClock(time.Value);
            if (takeFrame) { World.Apply(frame!.Frame); _nextFrame++; update(delta); }
            else { dispatch(ev!.Event); NextEventIndex++; update(delta); }
        }
        update(MoveClock(destination));
    }

    private float MoveClock(double time)
    {
        var delta = (float)(time - Clock);
        Clock = time;
        World.CurrentTime = recording.Header.StartedUtc.ToUniversalTime().AddSeconds(time);
        return delta;
    }
}
