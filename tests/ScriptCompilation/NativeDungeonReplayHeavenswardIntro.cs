using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeavenswardIntro(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Select(r => Value(r, "Script"))
            .Single(s => s.GetType().BaseType?.Name == "DungeonScript");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Areas() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        object[] MechanicPreviews() => previews().Where(p => !((string)Value(p, "Id")).Contains("hw-intro/enemy/")).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (id, territory, oid, center, excluded, cone, raidwide) in new[]
        {
            ("masterplayer.palace.deathgaze", 561u, 0x1692u, new Vector3(-300, 0, -220), new Vector3(-300, 0, -244.2f), 6417u, 6420u),
            ("masterplayer.palace.alicanto", 598u, 0x1818u, new Vector3(-300, 0, -236), new Vector3(-300, 0, -260), 7129u, 7132u),
        })
        {
            load(id,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true), Actor(100, 0, center),
                    Actor(101, 0, center + Vector3.UnitX)], full: true, party: party),
                Frame(territory, .1, [Actor(1, oid, center, combat: true, cast: Cast(cone, 3, center, MathF.PI / 2))], party: party),
                CastEvent(.1, 1, cone, 3, center, MathF.PI / 2),
                Frame(territory, 2, [Actor(1, oid, center + Vector3.UnitZ * 5, combat: false, rotation: 0,
                    cast: Cast(cone, 3, center, MathF.PI / 2))], party: party),
                JsonSerializer.Serialize(new { t = 2.1, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
                    data = new { Timestamp = Start.AddSeconds(2.1) } }),
                Frame(territory, 8, [Actor(1, oid, center, combat: true)], party: party),
                Frame(territory, 9, [Actor(1, oid, center, combat: true, cast: Cast(raidwide, 3, center, startedAt: 9))], party: party),
                CastEvent(9, 1, raidwide, 3, center),
                Frame(territory, 13, [Actor(1, oid, center, combat: true)], party: party),
                Frame(territory, 14, [], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == 1 && MechanicPreviews().Length == 1, id + " dual views/navigation.");
            var hostType = Script().GetType().Assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
            var host = RuntimeHelpers.GetUninitializedObject(hostType);
            Check((bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, null)!,
                id + " still accepts the migrated Palace floor bridge.");
            Check(!(bool)manager.IsGenericDodgeSuppressed(cone)
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = (ushort)cone })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)cone }),
                id + " source-scoped generic dodge ownership.");
            dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check((bool)boundary.Contains(center, 0f) && !(bool)boundary.Contains(excluded, 0f), id + " entrance polygon.");
            var area = Areas().Single();
            var windows = ((IEnumerable)Value(area, "DangerWindows")).Cast<object>().ToArray();
            Check((DateTime)Value(windows.Single(), "DangerFrom") == Start.AddSeconds(3.4), id + " NPC delay applied twice/missing.");
            controller.Seek(6d);
            Check(dangers() == 1 && MechanicPreviews().Length == 1, id + " early time expiry.");
            dynamic geometry = Value(MechanicPreviews().Single(), "Geometry");
            Check((bool)geometry.Contains(center + Vector3.UnitX * 17.9f, 0f)
                && !(bool)geometry.Contains(center + Vector3.UnitZ * 12, 0f), id + " locked facing.");
            controller.Seek(8.1);
            Check(dangers() == 0 && MechanicPreviews().Length == 0, id + " finish cleanup.");
            controller.Seek(9.5);
            Check(Damage().Length == 2 && Damage().All(d => (DateTime)Value(d, "Activation") == Start.AddSeconds(12.3))
                && dangers() == 0 && MechanicPreviews().Length == 0, id + " raidwide prediction/preview separation.");
            controller.Seek(13.1); Check(Damage().Length == 0, id + " raidwide finish.");
            controller.Seek(6d); Check(dangers() == 1, id + " reverse Seek.");
            stop();
        }

        foreach (var (id, territory, oid, addOid, scream) in new[]
        {
            ("masterplayer.palace.ixtab", 564u, 0x16B9u, 0x1764u, 6433u),
            ("masterplayer.palace.ah-puch", 601u, 0x181Bu, 0x1906u, 7145u),
        })
        {
            var center = new Vector3(-300, 0, territory == 564 ? -226 : -236);
            var zone = center + new Vector3(.1234f, 0, .5678f);
            load(id,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true, targetId: 100),
                    Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX),
                    Actor(2, 0x1E8EA9, zone, animationState1: 1, targetable: false),
                    Actor(3, addOid, center + Vector3.UnitX * 12)], full: true, party: party),
                Frame(territory, 1, [Actor(2, 0x1E8EA9, zone, animationState1: 0, eventState: 7, targetable: false)], party: party),
                Frame(territory, 2, [Actor(2, 0x1E8EA9, zone, animationState1: territory == 564 ? (byte)1 : (byte)0,
                    eventState: territory == 564 ? (byte)7 : (byte)0, targetable: false)], party: party),
                Frame(territory, 3, [Actor(1, oid, center, combat: true, cast: Cast(scream, 3, center, startedAt: 3))], party: party),
                CastEvent(3, 1, scream, 3, center),
                Frame(territory, 8, [Actor(1, oid, center, combat: true)], party: party),
                Frame(territory, 9, [], removed: [2], party: party),
                Frame(territory, 10, [Actor(1, oid, center, dead: true)], party: party), Frame(territory, 11, [], party: party),
            ]);
            controller.Seek(.5);
            Check(dangers() == 1 && MechanicPreviews().Length == 1, id + " pox nav/dual views.");
            if (territory == 601)
            {
                dynamic arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
                Check((bool)arena.Contains(new Vector3(-309, 0, -258), 0f)
                    && !(bool)arena.Contains(new Vector3(-290.96f, 0, -214.1f), 0f), "Ah Puch world outline reflected.");
            }
            dynamic display = Value(MechanicPreviews().Single(), "Geometry");
            Check((Vector3)display.Position == zone, id + " display anchor was quantized.");
            var instance = Encounter();
            Check((ulong)instance.GetType().GetMethod("PriorityTarget")!.Invoke(instance, [1ul, center])! == 3,
                id + " adds priority missing.");
            controller.Seek(1.1); Check(dangers() == 0 && MechanicPreviews().Length == 0, id + " inactive pox leaked.");
            controller.Seek(2.1); Check(dangers() == 1, id + " independent floor pox filter.");
            controller.Seek(3.5); Check(Damage().Length == 2 && dangers() == 1, id + " Scream raidwide/pox separation.");
            instance = Encounter();
            Check(((IEnumerable<string>)instance.GetType().GetMethod("Hints")!.Invoke(instance, [center])!).Any(h => h.Contains("Fear")),
                id + " Scream fear hint missing.");
            controller.Seek(8.1); Check(Damage().Length == 0 && dangers() == 1, id + " cast ending cleared persistent pox.");
            controller.Seek(9.1); Check(dangers() == 0, id + " removed pox survived.");
            controller.Seek(10.1); Check(previews().Length == 0, id + " boss death marker leak.");
            controller.Seek(.5); Check(dangers() == 1, id + " reverse Seek reconstruction.");
            stop();
        }

        var edda = new Vector3(300, 0, 374);
        load("masterplayer.palace.edda",
        [
            Header(565), Frame(565, 0, [Actor(1, 0x16C6, edda, combat: true), Actor(100, 0, edda - Vector3.UnitZ * 120),
                Actor(101, 0, edda), Actor(2, 0x16E8, edda), Actor(3, 0x16E9, edda)], full: true, party: party),
            Frame(565, .1, [Actor(1, 0x16C6, edda, combat: true, cast: Cast(6403, 3, edda))], party: party),
            CastEvent(.1, 1, 6403, 3, edda),
            Frame(565, 2.9, [Actor(100, 0, edda - Vector3.UnitZ * 120, rotation: MathF.PI)], party: party),
            Frame(565, 2.95, [Actor(1, 0x16C6, edda, combat: true)], party: party),
            Frame(565, 3, [Actor(100, 0, edda - Vector3.UnitZ * 120, rotation: MathF.PI)], party: party),
            Frame(565, 4, [Actor(1, 0x16C6, edda, combat: true),
                Actor(2, 0x16E8, edda, cast: Cast(6404, 4, edda, MathF.PI / 2, startedAt: 4)),
                Actor(3, 0x16E9, edda, cast: Cast(6405, 4, edda + Vector3.UnitZ * 8, startedAt: 4))], party: party),
            CastEvent(4, 2, 6404, 4, edda, MathF.PI / 2), CastEvent(4, 3, 6405, 4, edda + Vector3.UnitZ * 8),
            Frame(565, 10, [Actor(2, 0x16E8, edda)], party: party),
            Frame(565, 11, [Actor(3, 0x16E9, edda)], party: party),
            Frame(565, 12, [Actor(1, 0x16C6, edda, combat: true, cast: Cast(6400, 2, edda, target: 100, startedAt: 12))], party: party),
            Frame(565, 15, [Actor(1, 0x16C6, edda, combat: true, cast: Cast(6402, 3, edda, startedAt: 15))], party: party),
            Frame(565, 20, [Actor(1, 0x16C6, edda, combat: true)], party: party), Frame(565, 21, [], party: party),
        ]);
        controller.Seek(1d);
        Check(dangers() == 0 && MechanicPreviews().Length == 3, "Edda gaze eye/pupil/arc dual-view output.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 6404 }),
            "Edda helper generic dodge not taken over.");
        var eddaEncounter = Encounter();
        var eddaHints = (IEnumerable<string>)eddaEncounter.GetType().GetMethod("Hints")!.Invoke(eddaEncounter, [edda - Vector3.UnitZ * 120])!;
        Check(eddaHints.Contains("Turn away from gaze!"), "Edda long-range gaze warning missing.");
        controller.Seek(2.8);
        eddaEncounter = Encounter();
        Check(eddaEncounter.GetType().GetMethod("GazeFacing")!.Invoke(eddaEncounter, [edda - Vector3.UnitZ * 120, 0f]) is float,
            "Edda facing plan did not enter deadline window.");
        var scriptHints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facingControl = scriptHints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scriptHints)!;
        Check(!(bool)facingControl.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facingControl)!,
            "Edda offline replay executed a PR facing request.");
        controller.Seek(2.91);
        eddaEncounter = Encounter();
        Check(!((IEnumerable<string>)eddaEncounter.GetType().GetMethod("Hints")!.Invoke(eddaEncounter, [edda - Vector3.UnitZ * 120])!)
            .Contains("Turn away from gaze!"), "Edda safe facing warning persisted.");
        controller.Seek(2.96);
        Check(MechanicPreviews().Length == 0, "Edda snapshot cancellation inside old grace interval leaked eyes.");
        controller.Seek(5d);
        Check(dangers() == 2 && MechanicPreviews().Length == 2, "Edda concurrent helper geometry output.");
        controller.Seek(9d); Check(dangers() == 2, "Edda overdue helper casts expired.");
        controller.Seek(10.1); Check(dangers() == 1, "Edda helper cancellation cleared other caster.");
        controller.Seek(11.1); Check(dangers() == 0 && MechanicPreviews().Length == 0, "Edda helper finish leaked.");
        controller.Seek(12.5);
        Check(Damage().Length == 1 && (ulong)Value(Damage().Single(), "TargetId") == 100, "Edda targeted damage prediction.");
        controller.Seek(15.5);
        Check(Damage().Length == 2 && dangers() == 0, "Edda raidwide party prediction.");
        controller.Seek(20.1); Check(Damage().Length == 0, "Edda raidwide finish leaked.");
        controller.Seek(1d); Check(MechanicPreviews().Length == 3, "Edda reverse Seek gaze reconstruction.");
        stop();

        var c = new Vector3(230, 0, 79);
        load("masterplayer.quest.a-bloody-reunion",
        [
            Header(560), Frame(560, 0, [Actor(1, 0x161E, c, combat: true), Actor(100, 0, c, targetId: 1),
                Actor(10, 0x161F, c + Vector3.UnitX * 50, rotation: 1), Actor(11, 0x1620, c - Vector3.UnitX * 5),
                Actor(12, 0x1621, c), Actor(13, 0x233C, c)], full: true, party: party),
            Frame(560, .1, [Actor(10, 0x161F, c + Vector3.UnitX * 50, rotation: 1, cast: Cast(6032, 5, c)),
                Actor(11, 0x1620, c - Vector3.UnitX * 5, cast: Cast(6032, 3, c))], party: party),
            Frame(560, 1, [Actor(10, 0x161F, c + Vector3.UnitX * 50, targetable: false)], party: party),
            Frame(560, 2, [Actor(11, 0x1620, c - Vector3.UnitX * 5)], party: party),
            Frame(560, 3, [Actor(1, 0x161E, c, combat: true, cast: Cast(6026, 2.5, c, MathF.PI / 2, startedAt: 3)),
                Actor(11, 0x1620, c - Vector3.UnitX * 5, cast: Cast(6031, 3, c + Vector3.UnitZ * 5, startedAt: 3))], party: party),
            CastEvent(3, 1, 6026, 2.5, c, MathF.PI / 2), CastEvent(3, 11, 6031, 3, c + Vector3.UnitZ * 5),
            Frame(560, 6, [Actor(1, 0x161E, c, combat: true)], party: party),
            Frame(560, 7, [Actor(11, 0x1620, c - Vector3.UnitX * 5)], party: party),
            Frame(560, 8, [Actor(1, 0x161E, c, combat: true, cast: Cast(6027, 3, c, startedAt: 8))], party: party),
            CastEvent(8, 1, 6027, 3, c),
            Frame(560, 12, [Actor(1, 0x161E, c, combat: true)], party: party), Frame(560, 13, [], party: party),
        ]);
        controller.Seek(.5);
        var e = Encounter();
        Check(dangers() == 0 && Damage().Length == 0 && MechanicPreviews().Length == 0, "Regula Self Detonate invented AOE.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 10, ActionId = 6032 })
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 11, ActionId = 6031 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 13, ActionId = 6031 }),
            "Regula source ownership omitted hint-only turret casts or swallowed helpers.");
        var hints = ((IEnumerable)e.GetType().GetMethod("Hints")!.Invoke(e, [c])!).Cast<string>().ToArray();
        Check(hints.Count(h => h.StartsWith("Kill turret")) == 1 && hints.Any(h => h.Contains("4.9s")), "Regula first-caster countdown.");
        Check((ulong)e.GetType().GetMethod("PriorityTarget")!.Invoke(e, [1ul, c])! == 11, "Regula nearest equal-priority turret.");
        var markers = previews().Where(p => ((string)Value(p, "Id")).Contains("hw-intro/enemy/")).ToArray();
        Check(markers.Length == 3 && markers.All(p => Value(p, "Geometry").GetType().Name == "LocalPolygonGeometry"), "Regula native enemy markers.");
        dynamic outside = Value(markers.Single(p => ((string)Value(p, "Id")).EndsWith("/10")), "Geometry");
        Check((float)outside.Position.X < 251, "Regula outside marker not clamped.");
        controller.Seek(1.1);
        e = Encounter();
        Check((ulong)e.GetType().GetMethod("PriorityTarget")!.Invoke(e, [10ul, c])! == 11, "Regula cancelled turret priority remained.");
        controller.Seek(2.1);
        Check(dangers() == 0 && Damage().Length == 0, "Regula cancellation created output.");
        controller.Seek(3.5); Check(dangers() == 2 && MechanicPreviews().Length == 2, "Regula Slug/Grenado concurrent output.");
        controller.Seek(6.1); Check(dangers() == 1, "Regula one cast end cleared the other.");
        controller.Seek(7.1); Check(dangers() == 0, "Regula Grenado end leak.");
        controller.Seek(8.5);
        dynamic spread = Value(MechanicPreviews().Single(), "Geometry");
        Check(dangers() == 1 && (bool)spread.Contains(c + Vector3.UnitX * 15, 0f)
            && !(bool)spread.Contains(c - Vector3.UnitZ * 15, 0f), "Regula spread 240 degrees.");
        controller.Seek(12.1); Check(dangers() == 0 && MechanicPreviews().Length == 0, "Regula final cast end.");
        controller.Seek(3.5); Check(dangers() == 2, "Regula reverse Seek.");
        stop();
        Console.WriteLine("PASS production HW intro: Deathgaze/Alicanto/Edda/Ixtab/AhPuch exact arenas, locked casts, overdue casts, gaze/facing, independent persistent pox filters and adds; Regula turret countdown, priority, enemy markers, independent casts and Seek");
    }
}
