using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void CarnivaleBeautyBody(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints(Vector3? position = null) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!
            .Invoke(Encounter(), [position ?? center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action, double? eventTime = null)
            => JsonSerializer.Serialize(new
            {
                t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(eventTime ?? t) },
            });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Beauty replay leaked native controls.");
        }

        foreach (var (oid, name, add) in new[]
        {
            (0x26F5u, "猩红死妖姬——卡蜜拉·第一幕", 0x26F6u),
            (0x26F8u, "猩红死妖姬——卡蜜拉·第二幕", 0x26F7u),
        })
        {
            load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, oid, center)]),
                At(.2, [Actor(2, add, center + Vector3.UnitX * 10, combat: true)]),
                At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, dead: true)]),
                At(3, [], removed: [2]), At(4, [])]);
            controller.Seek(.15);
            Check(Value(Encounter(), "Active") == null && Value(Encounter(), "CarnivalePrecombatHint") != null
                && Shown("carnivale/precombat/arena").Length == 1 && dangers() == 0, "Beauty precombat.");
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x26F5), "Beauty group/default pull.");
            controller.Seek(1.5);
            Check(Value(Encounter(), "CarnivalePrecombatHint") == null && Shown("carnivale/enemy/").Length == 2,
                "Beauty combat transition/markers.");
            Check((Vector4)Value(Shown("carnivale/enemy/2").Single(), "Color")
                == (oid == 0x26F8 ? new Vector4(1, 128f / 255, 0, 1) : new Vector4(1, 0, 0, 1))
                && (Vector4)Value(Shown("carnivale/enemy/").Single(a => ((string)Value(a, "Id")).EndsWith("/1")), "Color")
                    == new Vector4(1, 0, 0, 1), "Beauty object colour.");
            var assembly = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = assembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!
                .GetField(oid == 0x26F5 ? "CircleBig" : "CircleSmall", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var direction = assembly.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 73.13f; x < 127; x += .7f)
                for (var z = 73.17f; z < 127; z += .7f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(direction, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Beauty source arena oracle.");
                    ++count;
                }
            controller.Seek(2.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x26F5), "Beauty completion waited wrong group.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0, "Beauty completion leaked output.");
            controller.Seek(1.5);
            Isolated();
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(1.6);
            Check(previews().Length == 0 && dangers() == 0, "Beauty disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale {oid:X} production preparation group or primary pull completion enemy colours exact arena source oracle {count} points reverse Seek owner");
        }

        foreach (var (oid, actions) in new[]
        {
            (0x26F5u, new uint[] { 14879 }),
            (0x26F8u, new uint[] { 14880, 14888, 14884, 14889, 14881, 14887 }),
        })
        {
            foreach (var action in actions)
            {
                var locked = center + Vector3.UnitX * 3;
                var changed = locked + Vector3.UnitZ * 4;
                load(guid, [Header(796), At(0, [Actor(1, oid, center, combat: true), Actor(100, 0, center)], true),
                    CastEvent(.1, 1, action, 3, center, rotation: -.3f),
                    At(.2, [Actor(1, oid, center, combat: true, cast: Cast(action, 3, locked, rotation: .7f))]),
                    At(1, [Actor(1, oid, center + Vector3.UnitZ * 12, rotation: 2, combat: true,
                        cast: Cast(action, 3, locked, rotation: .7f))]),
                    At(2, [Actor(1, oid, center, combat: true, cast: Cast(action, 3, changed, rotation: -.4f))]),
                    At(6, [Actor(1, oid, center, combat: true)]), At(7, [])]);
                controller.Seek(1.5);
                Check(dangers() == 1 && Shown("cast/").Length == 1
                    && (Vector3)((dynamic)Value(Shown("cast/").Single(), "Geometry")).Position == locked,
                    "Beauty packet/raw cast lock or dual output.");
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = (ushort)action }),
                    "Beauty native takeover.");
                controller.Seek(5);
                Check(dangers() == 1 && (Vector3)((dynamic)Value(Shown("cast/").Single(), "Geometry")).Position == changed,
                    "Beauty replacement or observed lifetime.");
                controller.Seek(6.5);
                Check(dangers() == 0 && Shown("cast/").Length == 0, "Beauty observed Finish.");
                controller.Seek(1.5);
                Check(dangers() == 1, "Beauty reverse Seek.");
                Isolated();
                stop();
            }
            Console.WriteLine($"PASS native Carnivale {oid:X} production all simple AOEs first raw correction locked source location rotation raw generation observed end navigation double view reverse Seek");
        }

        var gazePosition = center + Vector3.UnitX * 4;
        var playerPosition = gazePosition - Vector3.UnitZ * 120;
        load(guid, [Header(796), At(0,
                [Actor(1, 0x26F8, center, combat: true), Actor(100, 0, playerPosition)], true),
            CastEvent(.1, 1, 14951, 3, center),
            At(.2, [Actor(1, 0x26F8, center, combat: true, cast: Cast(14951, 3, gazePosition))]),
            At(1, [Actor(1, 0x26F8, center + Vector3.UnitX * 15, combat: true,
                cast: Cast(14951, 3, gazePosition), rotation: 2)]),
            At(4, [Actor(100, 0, playerPosition, rotation: MathF.PI)]),
            At(5, [Actor(100, 0, gazePosition - Vector3.UnitZ * 10001)]),
            EffectEvent(6, 1, 14951, 1, center),
            At(8, [Actor(1, 0x26F8, center, combat: true)]), At(9, [])]);
        controller.Seek(1.5);
        Check(Values("Gazes").Length == 1 && Shown("carnivale/gaze/").Length == 3
            && !previews().Any(a => ((string)Value(a, "Id")).EndsWith("gaze/1"))
            && dangers() == 0 && Hints(playerPosition).Contains("Turn away from gaze!")
            && (Vector3)Value(Values("Gazes").Single(), "Location") == gazePosition,
            "Beauty gaze lock full eye/pupil/arc or invented danger.");
        Check((Vector3)Value(Value(Shown("carnivale/gaze/1/eye").Single(), "Geometry"), "Position") == gazePosition
            && (Vector3)Value(Value(Shown("carnivale/gaze/1/pupil").Single(), "Geometry"), "Position") == gazePosition
            && (Vector3)Value(Value(Shown("carnivale/gaze/view").Single(), "Geometry"), "Position") == playerPosition,
            "Beauty final eye/pupil/viewing arc anchors.");
        Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [playerPosition, 0f]) == null, "Early gaze facing.");
        controller.Seek(2.8);
        var heading = (float)Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [playerPosition, 0f])!;
        var eyeDirection = Vector3.Normalize(gazePosition - playerPosition);
        Check(MathF.Sin(heading) * eyeDirection.X + MathF.Cos(heading) * eyeDirection.Z < .707107f,
            "Unsafe PR heading relative to locked eye.");
        Isolated();
        controller.Seek(4.5);
        Check(!Hints(playerPosition).Contains("Turn away from gaze!") && (bool)Value(Shown("carnivale/gaze/1/eye").Single(), "Safe"),
            "Beauty current viewer eye colour.");
        controller.Seek(7);
        var distant = gazePosition - Vector3.UnitZ * 10001;
        Check(Values("Gazes").Length == 1 && Shown("carnivale/gaze/").Length == 2
            && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [distant, 0f]) == null
            && !Hints(distant).Contains("Turn away from gaze!"), "Beauty actual default range or effect lifetime.");
        controller.Seek(8.5);
        Check(!Values("Gazes").Any() && Shown("carnivale/gaze/").Length == 0, "Gaze Finish cleanup.");
        controller.Seek(2.8);
        manager.SetMechanismEnabled(guid, "猩红死妖姬——卡蜜拉·第二幕", false);
        controller.Seek(2.9);
        Check(previews().Length == 0 && dangers() == 0 && !Values("Gazes").Any(), "Gaze disable owner.");
        manager.SetMechanismEnabled(guid, "猩红死妖姬——卡蜜拉·第二幕", true);
        stop();
        Console.WriteLine("PASS native Carnivale Stage13 Act2 production gaze source lock range10000 full eye pupil viewing arc deadline PR heading replay isolation observed end reverse Seek owner");

        load(guid, [Header(796), At(0,
                [Actor(1, 0x26F8, center, combat: true), Actor(100, 0, center),
                    Actor(2, 0x233C, center, cast: Cast(14888, 20, center, startedAt: 0)),
                    Actor(3, 0x233C, center, cast: Cast(14884, 20, center + Vector3.UnitX * 5, startedAt: 0))], true),
            Cancel(1, 2, 60000), At(2, []),
            At(3, [Actor(2, 0x233C, center, cast: Cast(14888, 20, center + Vector3.UnitZ * 5, startedAt: 0))]),
            Cancel(3.1, 2, 14884, eventTime: 2.5), Cancel(4, 2, 60000),
            At(5, [Actor(3, 0x233C, center)]), At(6, [])]);
        controller.Seek(.5);
        Check(dangers() == 2, "Group dual caster.");
        controller.Seek(2.5);
        Check(dangers() == 1, "Group arbitrary Finish or unchanged raw.");
        controller.Seek(3.5);
        Check(dangers() == 2, "Group generation old Finish.");
        controller.Seek(4.5);
        Check(dangers() == 1, "Group current arbitrary Finish.");
        controller.Seek(5.5);
        Check(dangers() == 0, "Group caster end.");
        controller.Seek(.5);
        Check(dangers() == 2, "Group reverse Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage13 Act2 production grouped AOEs arbitrary action Finish caster independence cancelled unchanged raw old Finish generation guard reverse Seek");

        foreach (var action in new uint[] { 15045, 14882 })
        {
            load(guid, [Header(796), At(0,
                    [Actor(1, 0x26F8, center, combat: true), Actor(100, 0, center), Actor(101, 0, center), Actor(102, 0, center, dead: true),
                        Actor(2, 0x26F7, center, isEnemy: true, objectIndex: 4,
                            cast: Cast(action, 3, center, startedAt: 0))], true),
                EffectEvent(4, 2, action, 1, center),
                At(5, [Actor(2, 0x26F7, center, targetable: false, isEnemy: true, objectIndex: 4,
                    cast: Cast(action, 3, center, startedAt: 0))]),
                At(8, [Actor(2, 0x26F7, center)]), At(9, [])]);
            controller.Seek(.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Hint became danger.");
            if (action == 15045)
                Check(Values("CarnivaleInterrupts").Length == 1
                    && (bool)Value(Values("CarnivaleInterrupts").Single(), "ShouldBeInterrupted")
                    && !(bool)Value(Values("CarnivaleInterrupts").Single(), "ShouldBeStunned")
                    && Values("PredictedDamage").Length == 0 && Hints().Contains("Interrupt!"), "Succubus interrupt flags.");
            else
                Check(Values("CarnivaleInterrupts").Length == 0 && Values("PredictedDamage").Length == 2
                    && Values("PredictedDamage").Select(p => ((ulong)Value(p, "TargetId"), (int)Value(p, "PartySlot"), (string)Value(p, "DamageType")))
                        .Order().SequenceEqual(new[] { (100ul, 0, "Raidwide"), (101ul, 1, "Raidwide") })
                    && Values("PredictedDamage").All(p => (DateTime)Value(p, "Activation") == Start.AddSeconds(3.3))
                    && Hints().Contains("Harmless raidwide unless you failed to kill succubus in time"), "Blood Rain damage/hint.");
            controller.Seek(6);
            Check(action == 15045
                ? Values("CarnivaleInterrupts").Length == 1 && !(bool)Value(Values("CarnivaleInterrupts").Single(), "ShouldBeInterrupted")
                : Values("PredictedDamage").Length == 2, "Hint effect/deadline ended component.");
            controller.Seek(8.5);
            Check(Values("CarnivaleInterrupts").Length == 0 && Values("PredictedDamage").Length == 0, "Hint Finish leak.");
            controller.Seek(.5);
            Isolated();
            stop();
            Console.WriteLine($"PASS native Carnivale Stage13 Act2 production {action} exact observed hint intent flags or party damage activation no AOE no automatic actions reverse Seek");
        }

        load(guid, [Header(796), At(0,
                [Actor(1, 0x25D5, center, combat: true), Actor(100, 0, center)], true),
            At(.1, [Actor(2, 0x26F8, center, combat: true, cast: Cast(14882, 3, center))]),
            At(1, [Actor(1, 0x25D5, center, dead: true)]),
            JsonSerializer.Serialize(new
            {
                t = 1.1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
                data = new { SourceId = 2ul, ActionId = 14882u, CastTime = 3.1f,
                    EffectPosition = Pose(center), Timestamp = Start.AddSeconds(.2) },
            }),
            Cancel(2, 2, 14882), At(3, []), At(4, [])]);
        const string bloodRain = "Harmless raidwide unless you failed to kill succubus in time";
        controller.Seek(.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x25D5
            && Values("Casts").Length == 1 && !Hints().Contains(bloodRain) && Values("PredictedDamage").Length == 0,
            "Background Blood Rain leaked selected-module hints or damage.");
        controller.Seek(1.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x26F8
            && Hints().Contains(bloodRain) && Values("PredictedDamage").Length == 1
            && (DateTime)Value(Values("PredictedDamage").Single(), "Activation") == Start.AddSeconds(3.4),
            "Blood Rain display handoff or raw finish after packet correction.");
        controller.Seek(3.5);
        Check(!Hints().Contains(bloodRain) && Values("PredictedDamage").Length == 0, "Background cancel raw revived.");
        controller.Seek(.5);
        Check(!Hints().Contains(bloodRain), "Background reverse Seek leaked.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage13 Act2 production background Blood Rain selected module isolation handoff raw damage deadline packet correction cancelled raw reverse Seek");

        var replacement = gazePosition + Vector3.UnitZ * 3;
        load(guid, [Header(796), At(0,
                [Actor(1, 0x26F8, center, combat: true), Actor(100, 0, playerPosition),
                    Actor(2, 0x233C, center, cast: Cast(14951, 20, gazePosition, startedAt: 0)),
                    Actor(3, 0x233C, center, cast: Cast(14951, 20, center, startedAt: 0))], true),
            At(1, [Actor(2, 0x233C, center, cast: Cast(14951, 20, replacement, startedAt: 0))]),
            Cancel(1.1, 2, 14951, eventTime: .5), Cancel(2, 2, 60000),
            EffectEvent(3, 2, 14951, 1, replacement), Cancel(4, 2, 14951), At(5, []),
            At(6, [Actor(3, 0x233C, center)]), At(7, [])]);
        controller.Seek(1.5);
        Check(Values("Gazes").Length == 2
            && (Vector3)Value(Values("Gazes").Single(c => (ulong)Value(c, "Source") == 2), "Location") == replacement,
            "Two gazes or old finish generation.");
        controller.Seek(3.5);
        Check(Values("Gazes").Length == 2, "Foreign action/effect cleared gaze.");
        controller.Seek(5.5);
        Check(Values("Gazes").Single() is { } remaining && (ulong)Value(remaining, "Source") == 3,
            "Gaze finish caster independence or cancelled unchanged raw.");
        controller.Seek(6.5);
        Check(!Values("Gazes").Any(), "Final gaze end leaked.");
        controller.Seek(1.5);
        Check(Values("Gazes").Length == 2, "Two gaze reverse Seek.");
        Isolated();
        stop();
        Console.WriteLine("PASS native Carnivale Stage13 Act2 production concurrent gazes raw generation lock old and foreign Finish effects cancellation unchanged raw and reverse Seek");
    }
}
