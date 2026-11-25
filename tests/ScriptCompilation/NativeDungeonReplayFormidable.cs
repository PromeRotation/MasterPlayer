using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFormidable(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string guid = "masterplayer.fate.formidable";
        object Property(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Select(s => Property(s, "Script"))
            .Single(s => s.GetType().Name == "Formidable");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        bool Active() => (bool)Property(Script(), "EncounterActive");
        object Boss(bool combat = true, object? cast = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x294D, default, combat: combat, cast: cast,
                objectIndex: 2, isEnemy: true))!;
            node["FateId"] = 1464;
            node["AggroPlayer"] = combat;
            return node;
        }
        string Native(double t, ulong source, uint action, uint sequence, ulong target = 100) =>
            JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, MainTargetId = target, GlobalSequence = sequence,
                    Targets = new ulong[] { target }, Timestamp = Start.AddSeconds(t) } });
        void Isolated()
        {
            var hints = Property(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Property(Property(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Formidable replay leaked facing, action prohibitions or movement hold.");
        }
        load(guid,
        [
            Header(814), Frame(814, 0, [Boss(false), Actor(100, 0, default)], full: true),
            Frame(814, 1, [Boss(), Actor(100, 0, new(5, 0, 5))]),
            Frame(814, 2, [Actor(100, 0, new(5.1f, 0, 4.5f))]),
            Frame(814, 3, [Actor(100, 0, new(6, 0, 5))]),
            Frame(814, 4, [Boss(false)]), Frame(814, 5, [Boss()]), Frame(814, 6, []),
        ]);
        controller.Seek(.2);
        Check(!Active() && dangers() == 0 && previews().Length == 2, "Formidable precombat display/navigation differs.");
        controller.Seek(1.2);
        object boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Active() && (Vector3)Property(boundary, "Position") == default
            && (Vector3)Property(boundary, "ViewCenter") == new Vector3(5, 0, 5)
            && !(bool)Property(boundary, "ShowBoundary") && (float)Property(boundary, "ViewRadius") == 30,
            "Formidable display circle/cached square or strict per-axis threshold differs.");
        controller.Seek(2.2);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((Vector3)Property(boundary, "Position") == new Vector3(5, 0, 4), "Formidable rounded cached center differs.");
        controller.Seek(3.2);
        Check((Vector3)Property((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, "Position")
            == new Vector3(5, 0, 4), "Display movement changed navigation origin.");
        controller.Seek(4.2);
        Check(!Active() && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "SimpleBoss deaggro did not clear formal navigation.");
        controller.Seek(5.2);
        Check(Active(), "Living primary did not repull.");
        controller.Seek(1.2);
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Formidable production territory814 source circle30 cached square strict5 rounding deaggro reset repull dual views Seek owner isolation");

        var records = new List<string>
        {
            Header(814), Frame(814, 0, [Boss(), Actor(100, 0, default)], full: true),
            Frame(814, .1, [Boss(cast: Cast(17395, 4, new(2, 0, 3)))]),
            Frame(814, 1, [Boss()]),
        };
        for (uint i = 1; i <= 7; ++i) records.Add(Native(2 + i, 1, i == 1 ? 17395u : 18002u, i));
        records.Add(Frame(814, 12, []));
        load(guid, records);
        controller.Seek(2.2);
        Check(dangers() == 1, "Cancelled Fires ring was removed before true effects.");
        controller.Seek(8.2);
        Check(dangers() == 1, "Sixth effect prematurely cleared Fires.");
        controller.Seek(9.2);
        Check(dangers() == 0 && previews().Length == 2, "Seventh native effect failed to clear Fires.");
        controller.Seek(2.2);
        Check(dangers() == 1, "Reverse Seek failed to rebuild frozen seven-hit state.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Formidable production effect-driven seven ring donut10 50 cancel retains true consumption clock Native routing dual previews navigation Seek");

        load(guid,
        [
            Header(814), Frame(814, 0, [Boss(), Actor(100, 0, default),
                Actor(10, 0x2A9C, new(2, 0, 3), targetable: false),
                Actor(11, 0x2BDC, new(5, 0, 6), targetable: false),
                Actor(12, 0x2BD5, default, targetable: false)], full: true),
            Frame(814, 1, [Actor(10, 0x2A9C, new(20, 0, 20), dead: true, targetable: false),
                Actor(12, 0x2BD5, new(3, 0, 4), targetable: false)]),
            Native(2, 10, 17404, 1), Native(3, 12, 18003, 2),
            Frame(814, 4, [], removed: [11]), Frame(814, 5, []),
        ]);
        controller.Seek(.2);
        Check(dangers() == 2 && previews().Length == 6, "Created charges or forced display-only missile was not published.");
        controller.Seek(1.2);
        Check(dangers() == 2, "Charge followed dead source or expired.");
        controller.Seek(2.2);
        Check(dangers() == 1, "Native charge effect failed to remove own prediction.");
        controller.Seek(3.2);
        Check(previews().Length == 3, "Missile native explosion retained outline.");
        controller.Seek(4.2);
        Check(dangers() == 0 && previews().Length == 2, "Destroyed charge retained navigation/display.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Formidable production created delayed charge donut9 60 circle8 frozen generation missile R6 display-only Native clear destruction owner");

        load(guid,
        [
            Header(814), Frame(814, 0, [Boss(), Actor(100, 0, new(15, 0, 15)),
                Actor(10, 0x233C, default), Actor(11, 0x233C, default), Actor(12, 0x233C, default)], full: true),
            Frame(814, .1, [Actor(10, 0x233C, default, cast: Cast(17408, 3, default, .2f)),
                Actor(11, 0x233C, default, cast: Cast(17397, 5, new(4, 0, 0))),
                Actor(12, 0x233C, default, cast: Cast(17411, 12, new(7, 0, 0)))]),
            Frame(814, 2, [Boss(cast: Cast(17394, 3, default))]),
            Frame(814, 5, [Boss(), Actor(10, 0x233C, default), Actor(11, 0x233C, default), Actor(12, 0x233C, default)]),
            Frame(814, 6, []),
        ]);
        controller.Seek(2.2);
        Check(dangers() == 3 && previews().Length > 5,
            $"Ordinary three geometry/navigation or steam projection missing: dangers={dangers()}, previews={previews().Length}.");
        Isolated();
        manager.SetMechanismEnabled(guid, "Formidable", false);
        controller.Seek(2.3);
        Check(dangers() == 0 && previews().Length == 0 && !Active(), "Disabled Formidable retained output.");
        manager.SetMechanismEnabled(guid, "Formidable", true);
        controller.Seek(5.2);
        Check(dangers() == 0 && previews().Length == 2, "Actual finish retained ordinary/Steam output.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Formidable production ordinary cone15 circle7 grenade12 real finish SteamDome15 display-only mechanic disable replay isolation");

        string Icon(double t) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 100ul, TargetId = 101ul, MarkerId = 62u, Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(guid,
        [
            Header(814), Frame(814, 0, [Boss(), Actor(100, 0, default, stackedStatus: 1269,
                    statusExpiresAt: Start.AddSeconds(10)), Actor(101, 0, default)], full: true, party: party),
            Icon(.1), Icon(.2), Native(2, 10, 17401, 1), Native(3, 10, 17401, 2),
            Frame(814, 4, [Actor(100, 0, default)], party: party), Frame(814, 5, [], party: party),
        ]);
        controller.Seek(.5);
        Check(previews().Length == 4 && dangers() == 2, "Duplicate source icons did not publish two independent min2 stacks.");
        var stay = Encounter().GetType().GetField("formidableStay", flags)!.GetValue(Encounter())!;
        Check(((IEnumerable)stay).Cast<object>().Count() == 1, "Source1269 clock not sampled.");
        Isolated();
        controller.Seek(2.2);
        Check(previews().Length == 3 && dangers() == 1, "Native main target did not consume first duplicate stack.");
        controller.Seek(4.2);
        Check(previews().Length == 2 && dangers() == 0
            && !((IEnumerable)Encounter().GetType().GetField("formidableStay", flags)!.GetValue(Encounter())!).Cast<object>().Any(),
            "Remaining native stack or real StatusLose leaked output.");
        Isolated();
        controller.Seek(.5);
        Check(previews().Length == 4 && dangers() == 2, "Reverse Seek did not rebuild stacks and Stay model.");
        stop();
        Console.WriteLine("PASS native fate Formidable production source icons independent min2 original clocks native MainTarget first-match Stay1269 gain lose no expanded control Seek");
    }
}
