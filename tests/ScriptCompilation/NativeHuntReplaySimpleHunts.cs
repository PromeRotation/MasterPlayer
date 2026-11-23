using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSimpleHunts(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const uint territory = 956;
        var rules = new (uint Oid, uint Action)[]
        {
            (0x3672, 27725), (0x3672, 27724), (0x4571, 39623), (0x4571, 39624),
            (0x1ABF, 8188), (0x1ABF, 8187), (0x1ABF, 8189),
            (0x1AB3, 7997), (0x1AB3, 8000), (0x1AB3, 8002),
            (0x2820, 16830), (0x2820, 16831), (0x2820, 16832), (0x2820, 16833),
            (0x35C0, 27269), (0x35C0, 27270), (0x35C0, 27271), (0x35C0, 27272), (0x35C0, 27273),
        };
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Healer", EntityId = 101UL } };
        var actors = rules.Select((r, i) => Actor((ulong)i + 1, r.Oid, new Vector3(i * 10, 0, 0), combat: true,
            cast: Cast(r.Action, 3, new(i * 10, 0, 10), MathF.PI / 2, target: 101))).ToArray();
        var lines = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, Vector3.Zero), Actor(101, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, actors, party: party),
        };
        for (var i = 0; i < rules.Length; ++i)
            lines.Add(JsonSerializer.Serialize(new { t = .1, kind = "CastStart",
                type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = MathF.PI / 2,
                data = new { SourceId = i + 1, TargetId = 101, ActionId = rules[i].Action, CastTime = 3,
                    EffectPosition = Pose(new(i * 10, 0, 10)), Timestamp = Start.AddSeconds(.1) } }));
        lines.Add(Frame(territory, 2, [Actor(100, 0, Vector3.Zero, dead: true),
            Actor(1, 0x3672, new(500, 0, 500), combat: true, cast: Cast(27725, 3, new(0, 0, 10), MathF.PI / 2), rotation: 3)], party: party));
        lines.Add(JsonSerializer.Serialize(new { t = 2.1, kind = "BattleEnded",
            type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent", data = new { Timestamp = Start.AddSeconds(2.1) } }));
        lines.Add(Frame(territory, 3, [Actor(1, 0x3672, new(500, 0, 500), combat: true)], party: party));
        lines.Add(JsonSerializer.Serialize(new { t = 3.2, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = 2, ActionId = 27724, Timestamp = Start.AddSeconds(3.2) } }));
        lines.Add(Frame(territory, 4, [Actor(3, 0x4571, Vector3.Zero)], party: party));
        lines.Add(Frame(territory, 6, [], removed: Enumerable.Range(1, rules.Length).Select(i => (ulong)i).ToArray(), party: party));
        lines.Add(Frame(territory, 7, [], party: party));
        load("masterplayer.hunts.simple", lines);
        controller.Seek(1d);
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        string? Text(string name) => (string?)Script().GetType().GetProperty(name)!.GetValue(Script());
        Check(dangers() == 13 && previews().Length == 32, "Simple hunt rules lost dual-view/navigation output, enemy markers or turned unavoidable damage into a danger.");
        Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Open-world hunts created a hard arena boundary.");
        Check(Text("PredictedDamageText") == "Raidwide：2人/4次；Tankbuster：1人/2次"
            || Text("PredictedDamageText") == "Tankbuster：1人/2次；Raidwide：2人/4次",
            "Hunt damage diagnostics lost target binding, independent sources, or invented x4 activation times.");
        Check(Text("MechanicHintsText")!.Contains("Raidwide x4"), "Hunt source hint was dropped.");
        var first = previews().Single(p => p.GetType().GetProperty("Id")!.GetValue(p)!.ToString()!.EndsWith("hunt/1", StringComparison.Ordinal));
        dynamic geometry = first.GetType().GetProperty("Geometry")!.GetValue(first)!;
        Check((bool)geometry.Contains(new Vector3(0, 0, 10), 0f), "Hunt cast location was replaced by actor position.");
        var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var host = RuntimeHelpers.GetUninitializedObject(hostType);
        Check(!(bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null)!,
            "A hunt script suppressed the entire open-world territory.");
        controller.Seek(2.5d);
        Check(dangers() == 13, "Player death/BattleEnded or caster movement cleared a fighting hunt.");
        controller.Seek(3.1d);
        Check(dangers() == 12 && previews().Length == 31, "One instance's observed end cleared another same-OID instance.");
        controller.Seek(3.5d);
        Check(dangers() == 11, "Cancel failed or stale cast resurrected on the next tick.");
        controller.Seek(5.5d);
        Check(dangers() == 10, "Boss disengagement retained output or estimated finish expired an observed active cast.");
        controller.Seek(6.5d);
        Check(dangers() == 0 && previews().Length == 0 && Text("PredictedDamageText") == null, "Removed hunts leaked ranges or predictions.");
        controller.Seek(1d);
        Check(dangers() == 13, "Reverse Seek failed to reconstruct isolated hunt instances.");
        stop();
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x3672, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
            CastEvent(.01, 1, 27724, 4, new(10, 0, 20), MathF.PI / 2),
            Frame(territory, .12, [Actor(1, 0x3672, Vector3.Zero, combat: true)]),
            Frame(territory, .2, [Actor(1, 0x3672, Vector3.Zero, combat: true,
                cast: Cast(27724, 4, new(10, 0, 20), MathF.PI / 2, startedAt: .01))]),
            Frame(territory, 2, [Actor(1, 0x3672, Vector3.Zero, combat: true,
                cast: Cast(27724, 4, new(80, 0, 20), 0, startedAt: 2))]),
            Frame(territory, 2.2, [Actor(1, 0x3672, Vector3.Zero, combat: true,
                cast: Cast(27724, 4, new(10, 0, 20), MathF.PI / 2, startedAt: .01))]),
            Frame(territory, 4, [Actor(1, 0x3672, Vector3.Zero, combat: true)]),
            JsonSerializer.Serialize(new { t = 4.2, kind = "CastStart",
                type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = MathF.PI / 2,
                data = new { SourceId = 1, ActionId = 27724, CastTime = 4, EffectPosition = Pose(new(10, 0, 20)),
                    Timestamp = Start.AddSeconds(.01) } }),
            Frame(territory, 5, []),
        ]);
        controller.Seek(.15d);
        Check(dangers() == 1, "Packet cast was ended by a pre-cast object snapshot.");
        dynamic boundary = Activator.CreateInstance(Script().GetType().Assembly.GetType("MasterPlayer.Spatial.Geometry.CircleGeometry")!, [Vector3.Zero, 100f])!;
        runtime.Manager.SetArenaBoundary(boundary);
        controller.Play(); controller.Update(.1f); controller.Pause();
        Check(dangers() == 1 && ReferenceEquals((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, (object)boundary),
            "Matching delayed snapshot lost the packet cast or replaced another arena.");
        controller.Play(); controller.Update(2.05f); controller.Pause();
        var laterPreview = previews().Single(p => p.GetType().GetProperty("Id")!.GetValue(p)!.ToString()!.EndsWith("hunt/1", StringComparison.Ordinal));
        dynamic later = laterPreview.GetType().GetProperty("Geometry")!.GetValue(laterPreview)!;
        Check((bool)later.Contains(new Vector3(80, 0, 25), 0f) && !(bool)later.Contains(new Vector3(12, 0, 20), 0f),
            "An old object cast replaced the next generation.");
        controller.Play(); controller.Update(2f); controller.Pause();
        Check(dangers() == 0, "A delayed old CastStart resurrected an ended earlier generation.");
        manager.StopScript("masterplayer.hunts.simple");
        Check(ReferenceEquals((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, (object)boundary),
            "Hunt stop cleared another owner's arena.");
        runtime.Manager.ClearArenaBoundary();
        stop();
        Console.WriteLine("PASS SimpleHunts actual DLL 19 rules, same-OID instances, locked poses, observed ends, pure damage targets, personal death, boss disengagement, no arena, partial bridge ownership, reverse Seek and owner cleanup");
    }
}
