using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
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
                Frame(territory, 2, [Actor(1, oid, center + Vector3.UnitZ * 5, combat: true, rotation: 0,
                    cast: Cast(cone, 3, center, MathF.PI / 2))], party: party),
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
            Check(!(bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, null)!,
                id + " suppressed the floor session.");
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
        Console.WriteLine("PASS production HW intro: Deathgaze/Alicanto exact arenas, locked casts, live overdue casts, raidwide predictions; Regula turret countdown, priority, enemy markers, independent casts and Seek");
    }
}
