using System.Numerics;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;
using PromeRotation.Network.Protocol;

internal static class ReplayRecordingRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("replay recording survives a JSON-lines round trip", RoundTrip);
        run("replay router dispatches typed, catch-all and mapped simple events", RouterDispatch);
        run("replay router isolates handler exceptions and honors unsubscribe", RouterIsolation);
        run("player-owned sources are filtered from recordings", PlayerSourceFilter);
    }

    private static void PlayerSourceFilter()
    {
        bool IsPlayerOwned(ulong id) => id is 100 or 200;   // 100=玩家，200=玩家召唤物

        Check(!ReplayRecordFilter.ShouldRecord(new LogSystemCastStartEvent { SourceId = 100 }, IsPlayerOwned),
            "Player casts must be filtered.");
        Check(!ReplayRecordFilter.ShouldRecord(new LogSystemActionEffectEvent { SourceId = 200 }, IsPlayerOwned),
            "Summon action effects must be filtered.");
        Check(!ReplayRecordFilter.ShouldRecord(new LogSystemStatusAddEvent { SourceId = 100 }, IsPlayerOwned) &&
              !ReplayRecordFilter.ShouldRecord(new LogSystemStatusRemoveEvent { SourceId = 200 }, IsPlayerOwned),
            "Player-owned status changes must be filtered.");
        Check(!ReplayRecordFilter.ShouldRecord(new LogSystemCancelCastEvent { SourceId = 100 }, IsPlayerOwned) &&
              !ReplayRecordFilter.ShouldRecord(new LogSystemInterruptCastEvent { SourceId = 200 }, IsPlayerOwned),
            "Cancel/interrupt of player-owned casts must be filtered.");

        Check(ReplayRecordFilter.ShouldRecord(new LogSystemCastStartEvent { SourceId = 300 }, IsPlayerOwned),
            "Enemy casts must be kept.");
        Check(ReplayRecordFilter.ShouldRecord(new LogSystemCastStartEvent { SourceId = 0 }, IsPlayerOwned),
            "Unresolvable sources must be kept.");
        Check(ReplayRecordFilter.ShouldRecord(new LogSystemTetherEvent { SourceId = 100 }, IsPlayerOwned) &&
              ReplayRecordFilter.ShouldRecord(new LogSystemTargetIconEvent { SourceId = 100 }, IsPlayerOwned),
            "Tethers and icons are not filtered.");
        Check(ReplayRecordFilter.ShouldRecord(new LogSystemMapEffectEvent(), IsPlayerOwned) &&
              ReplayRecordFilter.ShouldRecord(new LogSystemBattleStartedEvent(), IsPlayerOwned),
            "Arena and battle events are not filtered.");
        Check(!ReplayRecordFilter.ShouldRecord(new LogSystemChatLogEvent { Sender = "甲", Message = "你好" }, IsPlayerOwned),
            "Chat logs must never be recorded.");
        Check(ReplayRecordFilter.ShouldRecord(new LogSystemNpcYellEvent { SourceId = 300 }, IsPlayerOwned) &&
              ReplayRecordFilter.ShouldRecord(new LogSystemInstanceContentTextEvent(), IsPlayerOwned),
            "NPC speech is not chat and must be kept.");
    }

    private static void RoundTrip()
    {
        var started = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        var header = new ReplayHeader(1, 1268, started, "0.1.0.0") { TerritoryName = "斗技场" };
        var cast = new LogSystemCastStartEvent
        {
            ActionId = 33126,
            DisplayDelay = 2,
            CastTime = 4.7f,
            DurationMilliseconds = 4700,
            SourceId = 0x40000001UL,
            TargetId = 0x10FFEEUL,
            CanInterrupt = 1,
            EffectPosition = new Vector3(100.5f, 0.25f, -42.75f),
            SourcePosition = new Vector3(98f, 0f, -40f),
            TargetPosition = null,
            SourceRotation = 1.5708f,
            TargetRotation = 0.33f,
        };
        var tether = new LogSystemTetherEvent
        {
            SourceId = 11,
            TargetId = 22,
            Category = (ActorControlId)19,
            TetherId = 0x1234,
            SourcePosition = new Vector3(1f, 2f, 3f),
            TargetPosition = null,
        };
        LogSystemEvent[] events =
        [
            cast,
            tether,
            new LogSystemStatusAddEvent { SourceId = 3, TargetId = 4, StatusId = 1234, Duration = 29.5f, DurationMilliseconds = 29500, StackCount = 2, Param = 7, IsRefresh = true },
            new LogSystemBattleStartedEvent(),
            new LogSystemTerritoryEvent { TerritoryId = 1268, TerritoryName = "斗技场" },
            new LogSystemChatLogEvent { Sender = "甲", Message = "你好", LogKind = "Say" },
            new LogSystemWeatherChangedEvent { WeatherId = 7 },
            new LogSystemCastStartEvent { ActionId = 1, Raw = new object() },
        ];
        double[] times = [0d, 0.5d, 1.25d, 2d, 2.5d, 3.125d, 4d, 5d];

        using var stream = new MemoryStream();
        using (var writer = new ReplayRecordingWriter(stream))
        {
            writer.WriteHeader(header);
            for (var i = 0; i < events.Length; i++)
                writer.WriteEvent(times[i], events[i]);
            writer.Flush();
            stream.Position = 0;
            var loaded = ReplayRecordingReader.Load(stream);
            CheckRoundTrip(header, events, times, cast, tether, loaded);
        }
    }

    private static void CheckRoundTrip(ReplayHeader header, LogSystemEvent[] events, double[] times,
        LogSystemCastStartEvent cast, LogSystemTetherEvent tether, ReplayRecording loaded)
    {

        Check(loaded.Header.Version == 1 && loaded.Header.Territory == 1268 && loaded.Header.PluginVersion == "0.1.0.0",
            "Header fields must round trip.");
        Check(loaded.Header.TerritoryName == "斗技场", "Header territory name must round trip.");
        Check(loaded.Header.StartedUtc == new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc), "Header timestamp must round trip.");
        Check(loaded.SkippedEvents == 0, "All recorded events must parse.");
        Check(loaded.Events.Count == events.Length, "Event count must round trip.");
        Check(loaded.Duration == 5d, "Duration must be the last event time.");
        for (var i = 0; i < times.Length; i++)
            Check(Math.Abs(loaded.Events[i].T - times[i]) < 1e-9, "Event timestamps must round trip in order.");

        var readCast = (LogSystemCastStartEvent)loaded.Events[0].Event;
        Check(readCast.ActionId == cast.ActionId && readCast.CastTime == cast.CastTime &&
              readCast.SourceId == cast.SourceId && readCast.TargetId == cast.TargetId &&
              readCast.CanInterrupt == cast.CanInterrupt && readCast.DisplayDelay == cast.DisplayDelay &&
              readCast.DurationMilliseconds == cast.DurationMilliseconds,
            "Cast scalar fields must round trip.");
        Check(readCast.EffectPosition == cast.EffectPosition && readCast.SourcePosition == cast.SourcePosition &&
              readCast.TargetPosition == null && readCast.SourceRotation == cast.SourceRotation &&
              readCast.TargetRotation == cast.TargetRotation,
            "Cast Vector3 and nullable fields must round trip exactly.");

        var readTether = (LogSystemTetherEvent)loaded.Events[1].Event;
        Check(readTether.Category == tether.Category && readTether.TetherId == tether.TetherId &&
              readTether.SourcePosition == tether.SourcePosition && readTether.TargetPosition == null,
            "Tether enum and position fields must round trip.");

        var readStatus = (LogSystemStatusAddEvent)loaded.Events[2].Event;
        Check(readStatus.StatusId == 1234 && readStatus.IsRefresh && readStatus.Duration == 29.5f && readStatus.StackCount == 2 && readStatus.Param == 7,
            "Status fields must round trip.");
        Check(((LogSystemTerritoryEvent)loaded.Events[4].Event).TerritoryName == "斗技场", "Non-ASCII strings must round trip.");
        Check(((LogSystemChatLogEvent)loaded.Events[5].Event).Message == "你好", "Chat fields must round trip.");
        Check(((LogSystemWeatherChangedEvent)loaded.Events[6].Event).WeatherId == 7, "Weather payload must round trip.");
        Check(loaded.Events[7].Event.Raw is ReplayCastData { PacketFacing: null }, "Runtime Raw must be replaced by safe replay metadata without inventing facing.");
    }

    private static void RouterDispatch()
    {
        var router = new ReplayScriptEventRouter();
        var owner = new object();
        var castHits = new List<LogSystemCastStartEvent>();
        var allHits = 0;
        var genericTetherHits = 0;
        var battleStarted = 0;
        ushort? territory = null;
        string[]? chat = null;
        uint weather = 0;

        router.Subscribe(owner, "StartCast", (Action<LogSystemCastStartEvent>)(e => castHits.Add(e)), typeof(LogSystemCastStartEvent));
        router.Subscribe(owner, "LogEvent", (Action<LogSystemEvent>)(_ => allHits++), typeof(LogSystemEvent));
        router.Subscribe(owner, "LogEvent", (Action<LogSystemTetherEvent>)(_ => genericTetherHits++), typeof(LogSystemTetherEvent));
        router.Subscribe(owner, "BattleStarted", (Action)(() => battleStarted++));
        router.Subscribe(owner, "TerritoryChanged", (Action<ushort>)(t => territory = t));
        router.Subscribe(owner, "ChatLog", (Action<string, string, string>)((s, m, k) => chat = [s, m, k]));
        router.Subscribe(owner, "WeatherChanged", (Action<uint>)(w => weather = w));

        var cast = new LogSystemCastStartEvent { ActionId = 33126 };
        router.Raise(cast);
        router.Raise(new LogSystemTetherEvent());
        router.Raise(new LogSystemBattleStartedEvent());
        router.Raise(new LogSystemTerritoryEvent { TerritoryId = 1268 });
        router.Raise(new LogSystemChatLogEvent { Sender = "甲", Message = "你好", LogKind = "Say" });
        router.Raise(new LogSystemWeatherChangedEvent { WeatherId = 7 });

        Check(castHits.Count == 1 && ReferenceEquals(castHits[0], cast), "Typed subscriptions must receive their event exactly once.");
        Check(allHits == 6, "The catch-all LogEvent subscription must receive every raised event.");
        Check(genericTetherHits == 1, "Generic OnLogEvent<TEvent> subscriptions must only receive matching types.");
        Check(battleStarted == 1, "LogSystemBattleStartedEvent must fan out to the simple BattleStarted subscription.");
        Check(territory == 1268, "TerritoryChanged must receive the ushort territory payload.");
        Check(chat is ["甲", "你好", "Say"], "ChatLog must receive sender, message and kind in order.");
        Check(weather == 7, "WeatherChanged must receive the weather id payload.");
    }

    private static void RouterIsolation()
    {
        var router = new ReplayScriptEventRouter();
        var owner = new object();
        var goodHits = 0;
        var removedHits = 0;

        router.Subscribe(owner, "StartCast", (Action<LogSystemCastStartEvent>)(_ => throw new InvalidOperationException("boom")), typeof(LogSystemCastStartEvent));
        router.Subscribe(owner, "StartCast", (Action<LogSystemCastStartEvent>)(_ => goodHits++), typeof(LogSystemCastStartEvent));
        var removed = router.Subscribe(owner, "StartCast", (Action<LogSystemCastStartEvent>)(_ => removedHits++), typeof(LogSystemCastStartEvent));
        removed.Dispose();

        var cast = new LogSystemCastStartEvent { ActionId = 1 };
        router.Raise(cast);
        router.Raise(cast);

        Check(goodHits == 2, "A throwing handler must not prevent later handlers from running.");
        Check(removedHits == 0, "Disposed subscriptions must stop receiving events.");
        Check(router.ErrorCount == 2 && router.LastError == "boom", "Handler exceptions must be counted with their message.");

        router.Dispose();
        router.Raise(cast);
        Check(goodHits == 2, "A disposed router must not dispatch further events.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
