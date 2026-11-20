using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

internal static class FinalVerseRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("Final Verse normal owns the shared boss only in the two supported territories", () =>
        {
            Check(PtBossCatalog.Owns(0x48EA, 1333), "Normal trial must suppress duplicate BMR output");
            Check(PtBossCatalog.Owns(0x48EA, 1290), "PT99 ownership must remain intact");
            foreach (var territory in new uint[] { 0, 1065, 1311, 1332, 1334 })
                Check(!PtBossCatalog.Owns(0x48EA, territory), "Unrelated areas, CFC IDs and Quantum must not be claimed");
            Check(!PtBossCatalog.Owns(0x48EE, 1311) && !PtBossCatalog.Owns(0x48EE, 1333), "Quantum OID remains with its own module");
        });
        run("Final Verse normal ARR opening drives the unchanged PT99 model", () =>
        {
            using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "PilgrimsTraverse", "final-verse-normal-013910.json")));
            var fixture = file.RootElement;
            Check(fixture.GetProperty("content").GetInt32() == 1065 && fixture.GetProperty("territory").GetInt32() == 1333,
                "Fixture must be the normal trial, not Quantum or a deep-dungeon recording");
            var start = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
            var e = new Pt99Encounter();
            var bosses = fixture.GetProperty("bosses").EnumerateArray().Select(b => new ReplayActorState
            {
                Id = b.GetProperty("id").GetUInt64(), Oid = b.GetProperty("oid").GetUInt32(),
                Position = Point(b.GetProperty("position")), Hp = 1000, MaxHp = 1000, Targetable = true, InCombat = true
            }).ToArray();
            Check(bosses.Select(b => b.Oid).Order().SequenceEqual(new uint[] { 0x48EA, 0x48EB }), "Both real boss OIDs must match PT99");
            var pc = new ReplayActorState { Id = 100, Kind = 1, Position = e.Center,
                Statuses = [new(4560, 0, 0, DateTime.MaxValue)] };
            e.Synchronize([.. bosses, pc], start);
            Check(e.Active && e.SelectTarget(start, 100, 0)?.Oid == 0x48EA,
                "Normal trial activates and selects the light boss even when that boss stands outside the floor");
            var casts = 0;
            var effects = 0;
            foreach (var ev in fixture.GetProperty("events").EnumerateArray())
            {
                var now = start.AddSeconds(ev.GetProperty("t").GetDouble());
                var action = ev.GetProperty("action").GetUInt32();
                var source = ev.GetProperty("source").GetUInt64();
                Check(e.Actions.Contains(action), $"Normal trial action {action} must already be supported");
                if (ev.GetProperty("kind").GetString() == "cast")
                {
                    ++casts;
                    var position = Point(ev.GetProperty("position"));
                    var rotation = ev.GetProperty("rotation").GetSingle();
                    var hit = now.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3);
                    e.StartCast(new(source, action, position, rotation, now, hit, 100));
                    if (action is 44067 or 44062)
                    {
                        var area = e.Collect(now, 100).Single(a => a.Owner == source && a.Action == action);
                        Check(area.Position == position && Math.Abs((area.From - hit.AddSeconds(-.2)).TotalSeconds) < .001,
                            "Normal trial preserves the packet pose and PT99 explicit timing");
                        if (action == 44062)
                            Check(area.Geometry.Contains(position + new Vector3(5.9f, 0, 0))
                                && !area.Geometry.Contains(position + new Vector3(6.1f, 0, 0)), "Normal Ball of Fire keeps R6");
                    }
                }
                else
                {
                    ++effects;
                    e.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action,
                        GlobalSequence = ev.GetProperty("seq").GetUInt32() }, now);
                    Check(!e.Collect(now, 100).Any(a => a.Owner == source && a.Action == action), "Real effect clears the matching telegraph");
                }
            }
            Check(casts == 9 && effects == 7, "The complete available opening sample must be replayed");
        });
    }

    private static Vector3 Point(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
