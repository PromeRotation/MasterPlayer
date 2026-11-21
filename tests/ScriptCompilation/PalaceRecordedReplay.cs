using System.Collections;
using System.Numerics;
using System.Text.Json;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

internal static class PalaceRecordedReplay
{
    internal static void Run(dynamic controller, dynamic runtime, string config)
    {
        var gamePath = Environment.GetEnvironmentVariable("PALACE_GAME_DATA");
        if (string.IsNullOrEmpty(gamePath))
        {
            Console.WriteLine("SKIP Palace recorded production routing: set PALACE_GAME_DATA to the game sqpack directory");
            return;
        }
        var original = Svc.Data;
        using var game = new Lumina.GameData(gamePath, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
            PanicOnSheetChecksumMismatch = true,
        });
        var actions = game.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        typeof(Svc).GetProperty(nameof(Svc.Data))!.SetValue(null, new PalaceGameData(game));
        try
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "palace-recordings.json")));
            var count = 0;
            var geometryCount = 0;
            foreach (var recording in fixture.RootElement.GetProperty("recordings").EnumerateArray())
            {
                var territory = recording.GetProperty("source").GetProperty("territory").GetUInt32();
                var entities = recording.GetProperty("entities").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetUInt64());
                foreach (var ev in recording.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "cast"))
                {
                    var id = ev.GetProperty("entity").GetUInt64();
                    var oid = entities[id].GetProperty("oid").GetUInt32();
                    var action = ev.GetProperty("action").GetUInt32();
                    var pos = ev.GetProperty("sourcePos");
                    var loc = ev.GetProperty("loc");
                    static object Point(JsonElement a) => new { X = a[0].GetSingle(), Y = a[1].GetSingle(), Z = a[2].GetSingle() };
                    var lines = new List<string>
                    {
                        JsonSerializer.Serialize(new { v = 2, territory, startedUtc = "2026-11-19T00:00:00Z", pluginVersion = "palace-recorded-test" }),
                        JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new { Full = true, Territory = territory, LocalPlayerId = 99999,
                            Actors = new object[] {
                                new { Id = id, Oid = oid, Hp = 100000, MaxHp = 100000, InCombat = true, Hostile = true, Targetable = true, HitboxRadius = 0, Position = Point(pos) },
                                new { Id = 99999, Kind = 1, Hp = 1000, Position = Point(pos) } } } }),
                        JsonSerializer.Serialize(new { t = 1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                            packetFacing = ev.GetProperty("rotation").GetSingle(), data = new { SourceId = id, TargetId = 99999, ActionId = action,
                                CastTime = ev.GetProperty("seconds").GetSingle(), Timestamp = "2026-11-19T00:00:01Z", SourcePosition = Point(pos), EffectPosition = Point(loc) } }),
                        JsonSerializer.Serialize(new { t = 1.1, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                            data = new { SourceId = id, ActionId = action, Timestamp = "2026-11-19T00:00:01.1Z" } }),
                        JsonSerializer.Serialize(new { t = 2, kind = "WorldFrame", data = new { Territory = territory, LocalPlayerId = 99999, Removed = new[] { id } } }),
                    };
                    var file = Path.Combine(config, "palace-recorded-cast.mpreplay.jsonl");
                    File.WriteAllLines(file, lines);
                    Check(controller.Load(file), (string)controller.StatusMessage);
                    controller.Play();
                    controller.Seek(1.01d);
                    var dangers = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
                    var row = actions!.GetRow(action);
                    var expected = action is 7090 or 7156 || row.VFX.RowId != 25 && row.CastType != 1
                        && !(row.CastType is 2 or 5 && row.EffectRange >= 30);
                    if (controller.RouterErrorCount != 0)
                    {
                        var routers = (IEnumerable)((object)controller).GetType().GetField("_routers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue((object)controller)!;
                        foreach (var pair in routers)
                        {
                            var router = pair!.GetType().GetField("Item2")!.GetValue(pair)!;
                            throw new InvalidOperationException("Recorded router: " + router.GetType().GetProperty("LastError")!.GetValue(router));
                        }
                    }
                    Check(dangers.Length == (expected ? 1 : 0), $"Production recorded action {action} oid={oid} expected geometry={expected}, got {dangers.Length}; scripts={controller.ReplayScriptCount}; errors={controller.RouterErrorCount}; status={controller.StatusMessage}");
                    if (expected)
                    {
                        ++geometryCount;
                        if (action != 7156)
                        {
                            dynamic danger = dangers[0];
                            var center = (Vector3)danger.Geometry.Position;
                            Check(Vector3.Distance(center, new(loc[0].GetSingle(), loc[1].GetSingle(), loc[2].GetSingle())) < .001f,
                                "Production geometry did not preserve packet location: " + action);
                        }
                    }
                    controller.Seek(1.11d);
                    Check(!((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Any(), "Production recorded cancel retained danger " + action);
                    Check(controller.RouterErrorCount == 0, "Production recorded router error " + action);
                    controller.StopSession();
                    ++count;
                }
            }
            Check(count == 161, "Production did not route every recorded cast");
            Console.WriteLine($"PASS production Palace ARR routing: {count} actual recorded casts; {geometryCount} navigation outputs; real Lumina inference, locked packet poses, source-specific cancel, no live object access or movement");
        }
        finally
        {
            typeof(Svc).GetProperty(nameof(Svc.Data))!.SetValue(null, original);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
