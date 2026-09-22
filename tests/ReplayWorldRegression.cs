using System.Numerics;
using System.Text;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

internal static class ReplayWorldRegression
{
    private static readonly DateTime Start = new(2026, 9, 21, 1, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("replay world preserves initial actors, statuses, casts, tethers, roles and packet facing", () =>
        {
            var recording = Scene();
            using var stream = new MemoryStream();
            using var writer = new ReplayRecordingWriter(stream);
            writer.WriteHeader(recording.Header);
            writer.WriteWorldFrame(0, recording.WorldFrames[0].Frame);
            writer.WriteWorldFrame(1, recording.WorldFrames[1].Frame);
            writer.WriteEvent(1, recording.Events[0].Event);
            writer.WriteWorldFrame(6, recording.WorldFrames[2].Frame);
            stream.Position = 0;
            var loaded = ReplayRecordingReader.Load(stream);
            Check(loaded.WorldFrames.Count == 3 && loaded.Duration == 6, "Object frames were lost from the file duration.");
            var actor = loaded.WorldFrames[1].Frame.Actors.Single(a => a.Id == 1);
            Check(actor.SameAs(recording.WorldFrames[1].Frame.Actors[0]), "Full actor state did not round trip.");
            Check(loaded.WorldFrames[0].Frame.LocalTimelineRole == "H1" && loaded.WorldFrames[0].Frame.Party.Single().Role == "D1", "Local role and party assignment were conflated.");
            Check(CastRotation.TryGet((LogSystemCastStartEvent)loaded.Events[0].Event, out var facing) && facing == MathF.PI / 2,
                "Locked packet facing did not survive serialization.");
        });
        run("replay world deltas retain unchanged actors and remove disappeared actors", () =>
        {
            var initial = Scene().WorldFrames[0].Frame;
            var same = initial with { Actors = initial.Actors.Select(a => a with { Statuses = a.Statuses.ToArray(), Tethers = a.Tethers.ToArray() }).ToArray() };
            Check(ReplayWorldFrame.Delta(initial, same).Actors.Length == 0, "Equal status arrays force full actor rewrites.");
            var changed = initial with { Actors = [initial.Actors[0] with { Hp = 55, Statuses = [], Tethers = [], Cast = null }] };
            var delta = ReplayWorldFrame.Delta(initial, changed);
            var world = new ReplayWorldState(); world.Apply(initial); world.Apply(delta);
            Check(world.Actors.Count == 1 && world.Actors[1].Hp == 55 && world.Actors[1].Statuses.Length == 0 && world.Actors[1].Cast == null,
                "Object removal or cleared status/cast did not apply.");
            Check(delta.Removed.SequenceEqual([2UL]), "Removed object identity was not recorded.");
            var nextZone = changed with { Territory = 1037, Actors = [] };
            var full = ReplayWorldFrame.Delta(changed, nextZone); world.Apply(full);
            Check(full.Full && world.Actors.Count == 0 && world.Territory == 1037, "Territory change retained old actors.");
        });
        run("replay world keeps file order for frames and events with equal timestamps", () =>
        {
            using var stream = new MemoryStream();
            using var writer = new ReplayRecordingWriter(stream);
            writer.WriteHeader(new(2, 1038, Start, "test"));
            writer.WriteWorldFrame(0, Scene().WorldFrames[0].Frame);
            for (var i = 1; i <= 30; ++i)
            {
                writer.WriteWorldFrame(1, new() { Actors = [new() { Id = 1, Hp = (uint)i }] });
                writer.WriteEvent(1, new LogSystemActionEffectEvent { ActionId = (uint)i });
            }
            stream.Position = 0;
            var playback = new ReplayPlayback(ReplayRecordingReader.Load(stream));
            var hits = 0;
            playback.AdvanceTo(1, e => { Check(playback.World.Actors[1].Hp == ((LogSystemActionEffectEvent)e).ActionId, "Equal-time sorting reversed a frame and its event."); hits++; }, _ => { });
            Check(hits == 30, "Same-time events were dropped.");
        });
        foreach (var step in new[] { 0.025, 0.1, 0.4, 2.0 })
            run($"replay world step {step}: recorded time reproduces dungeon cast without live actors", () =>
            {
                var playback = new ReplayPlayback(Scene());
                var encounter = new DungeonEncounter(1038);
                using var safe = new ScriptSafeZoneApi(); var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
                void Update(float _)
                {
                    encounter.Synchronize(playback.World.Actors.Values.Select(a => new DungeonActor(a.Id, a.Oid, a.Position,
                        a.Dead, a.Targetable, a.InCombat, a.Cast is { } c ? new(c.ActionId, c.Location, c.Rotation, c.StartedAt, c.FinishAt, c.TargetId) : null)),
                        playback.World.LocalPlayer?.Position, playback.World.CurrentTime);
                    output.Apply(encounter, playback.World.CurrentTime, true);
                }
                var adapter = new DungeonEventAdapter(encounter, () => Update(0), e => CastRotation.TryGet(e, out var r) ? r : null,
                    message => throw new InvalidOperationException(message));
                void Dispatch(LogSystemEvent ev) { adapter.Handle(ev, playback.World.CurrentTime); output.Apply(encounter, playback.World.CurrentTime, true); }
                for (var t = 0d; t < 2; t += step) playback.AdvanceTo(t, Dispatch, Update);
                playback.AdvanceTo(2, Dispatch, Update);
                Check(encounter.Casts.Count == 1 && draw.Previews.Single().Value.Geometry.Position == new Vector3(3, 0, 4), "Replayed cast used today's time or live caster position.");
                Check(safe.Manager.BuildSnapshot(playback.World.CurrentTime).DangerAreas.Single().DangerWindows.Single().DangerFrom == Start.AddSeconds(5.3), "Replay shifted the absolute damage window.");
                var pausedTime = playback.World.CurrentTime;
                playback.AdvanceTo(2, Dispatch, Update);
                Check(playback.World.CurrentTime == pausedTime && draw.Previews.Count == 1, "Paused replay advanced or removed its cast.");
                playback.AdvanceTo(6, Dispatch, Update);
                Check(encounter.Casts.Count == 0 && draw.Previews.Count == 0 && playback.World.Actors.Count == 1, "Replay end retained destroyed objects or cast output.");
                output.Clear();
            });
        run("replay world restart reconstructs prior states and animation after a backward seek", () =>
        {
            var recording = Scene();
            var playback = new ReplayPlayback(recording);
            playback.AdvanceTo(6, _ => { }, _ => { });
            Check(!playback.World.Actors.ContainsKey(1), "Expected the recorded removal before seeking.");
            playback = new(recording);
            playback.AdvanceTo(2, _ => { }, _ => { });
            Check(playback.World.Actors[1].EventAnimation == 0x00040004 && playback.World.Actors[1].Statuses.Length == 1,
                "Backward seek did not rebuild animation/status history.");
        });
        run("replay world legacy cast never substitutes ordinary or live facing", () =>
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
                {"v":1,"territory":1038,"startedUtc":"2026-09-21T01:00:00Z","pluginVersion":"old"}
                {"t":1,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","data":{"ActionId":28546,"SourceId":1,"SourceRotation":1.5}}
                """));
            var recording = ReplayRecordingReader.Load(stream);
            Check(recording.WorldFrames.Count == 0 && !CastRotation.TryGet((LogSystemCastStartEvent)recording.Events[0].Event, out _),
                "Old replay invented locked facing or accessed live objects.");
        });
        run("replay world rejects unsupported versions and missing initial snapshot", () =>
        {
            foreach (var content in new[]
            {
                "{\"v\":99,\"territory\":1,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"future\"}",
                "{\"v\":2,\"territory\":1,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"test\"}",
                "{\"v\":2,\"territory\":1,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"test\"}\n{\"t\":0,\"kind\":\"WorldFrame\",\"data\":{\"Full\":true}}\n{broken",
                "{\"v\":2,\"territory\":1,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"test\"}\n{\"t\":0,\"kind\":\"WorldFrame\",\"data\":{\"Full\":true,\"Actors\":[{\"Id\":1,\"Statuses\":[null]}]}}",
                "{\"v\":2,\"territory\":1,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"test\"}\n{\"t\":0,\"kind\":\"WorldFrame\",\"data\":{\"Full\":true,\"Actors\":[{\"Id\":1,\"Tethers\":[null]}]}}",
                "{\"v\":2,\"territory\":1,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"test\"}\n{\"t\":0,\"kind\":\"WorldFrame\",\"data\":{\"Full\":false}}"
            })
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                var rejected = false;
                try { ReplayRecordingReader.Load(stream); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "An unusable world recording loaded silently.");
            }
        });
        run("replay world clocks freeze preview expiry and registry windows", () =>
        {
            var now = Start;
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var registry = new SafeZoneRegistry(manager, () => now);
            registry.AddDelayedHit(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0), 2, 0.6f);
            var visibility = new AoeVisibilityRegistry([], () => now);
            visibility.AddPreview("test", new CircleGeometry(Vector3.Zero, 3), false, 2000, "replay", "Replay");
            Check(manager.BuildSnapshot(now).DangerAreas.Single().DangerWindows.Single().DangerFrom == Start.AddSeconds(2), "Registry used wall time for replay danger.");
            visibility.Update(manager);
            Check(visibility.GetPreviews(now).Count() == 1, "Wall time expired a paused preview.");
            now = Start.AddSeconds(3); visibility.Update(manager);
            Check(!visibility.GetPreviews(now).Any(), "Preview did not expire with recorded time.");
        });
        run("replay world status durations preserve pending self-casts and bound native outliers", () =>
        {
            Check(ReplayStatus.Expiration(Start, -30) == Start.AddSeconds(30), "A pending self-cast status became permanent.");
            Check(ReplayStatus.Expiration(Start, float.MaxValue) == Start.AddSeconds(100000), "An outlier status overflows the replay clock.");
            Check(ReplayStatus.Expiration(Start, 0) == DateTime.MaxValue, "A durationless status became expired.");
        });
        run("replay world dynamic bindings use recorded entities and never fall through to live", () =>
        {
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            Vector3? position = new(8, 0, 9);
            manager.EntityPoseOverride = id => id == 10 && position is { } p ? (p, 1.2f) : null;
            var handle = manager.AddObstacle(new CircleTemplate(2), new FollowEntityPosition(10, Vector2.Zero, 0, MissingEntityPolicy.RemoveArea));
            Check(manager.BuildSnapshot(Start).HardObstacles.Single().Geometry.Position == position, "Binding ignored recorded position.");
            position = null;
            Check(manager.BuildSnapshot(Start).HardObstacles.Count == 0 && !manager.ContainsArea(handle), "Missing recorded entity fell back to the live table.");
        });
        run("replay world party damage follows actual target and preserves BMR damage type", () =>
        {
            var e = new DungeonEncounter(1038);
            var boss = new DungeonActor(1, 0x387C, Vector3.Zero, InCombat: true, TargetId: 3);
            var mt = new DungeonActor(2, 0, Vector3.Zero, Name: "tank", PartySlot: 0, PartyRole: "MT");
            var d1 = new DungeonActor(3, 0, Vector3.Zero, Name: "actual target", PartySlot: 4, PartyRole: "D1");
            e.Synchronize([boss, mt, d1], Vector3.Zero, Start);
            e.StartCast(1, new(28547, Vector3.Zero, null, Start, Start.AddSeconds(4.3), 3), Start);
            var prediction = e.PredictedDamage.Single();
            Check(prediction.TargetId == 3 && prediction.PartySlot == 4 && prediction.Role == "D1" && prediction.DamageType == "Tankbuster",
                "Catapult prediction guessed MT or changed its BMR default damage type.");
            e.StartCast(1, new(28545, Vector3.Zero, null, Start.AddSeconds(1), Start.AddSeconds(5.3), 1), Start.AddSeconds(1));
            Check(e.PredictedDamage.Single().TargetId == 3, "Self-targeted tankbuster did not use the caster's current target.");
            e.Synchronize([boss, mt], Vector3.Zero, Start.AddSeconds(1));
            Check(!e.PredictedDamage.Any(), "Missing party target was invented as MT.");
        });
    }

    private static ReplayRecording Scene()
    {
        var actor = new ReplayActorState
        {
            Id = 1, EntityId = 1, GameObjectId = 1, Oid = 0x3870, Name = "敌人", NameId = 554, Kind = 2,
            Position = new(27, 0, 114), Rotation = 0.7f, HitboxRadius = 2, InCombat = true, Targetable = true, Hostile = true,
            Hp = 100, MaxHp = 200, Mp = 50, MaxMp = 100, Shield = 10, Level = 20, ClassJob = 19, TargetId = 2,
            EventState = 3, ModelState = 4, AnimationState1 = 5, AnimationState2 = 6, EventAnimation = 0x00040004,
            Cast = new(28462, 2, new(3, 0, 4), MathF.PI / 2, Start.AddSeconds(1), Start.AddSeconds(5.3), true, false),
            Statuses = [new(123, 7, 2, Start.AddSeconds(30))], Tethers = [new(99, 2, 60)]
        };
        var initial = new ReplayWorldFrame
        {
            Full = true, Territory = 1038, LocalPlayerId = 2, TargetId = 1, LocalTimelineRole = "H1", Party = [new(4, "D1", 2)],
            Actors = [actor with { Cast = null }, new() { Id = 2, EntityId = 2, Position = new(27, 0, 114), Kind = 1, Targetable = true, Hp = 100 }]
        };
        var cast = new LogSystemCastStartEvent
        {
            SourceId = 1, TargetId = 2, ActionId = 28462, CastTime = 4, Timestamp = Start.AddSeconds(1),
            EffectPosition = new(3, 0, 4), SourceRotation = 0, Raw = new ReplayCastData(MathF.PI / 2)
        };
        return new(new(2, 1038, Start, "test"), [new(1, cast, 3)], 0)
        {
            WorldFrames = [new(0, initial, 1), new(1, initial with { Full = false, Actors = [actor] }, 2),
                new(6, initial with { Full = false, Actors = [], Removed = [1] }, 4)]
        };
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
