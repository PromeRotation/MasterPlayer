using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAlteRoite(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.raid.alteroite.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var assembly = runtimeObject.GetType().Assembly;
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Encounter() => Read(Script(), "_encounter");
        object Model() => Read(Encounter(), "AlteRoite");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        object[] Predictions() => ((IEnumerable)Read(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        void Seek(double at)
        {
            Check((int)controller.RouterErrorCount == 0, "AlteRoite callback failed before Seek.");
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0, "AlteRoite callback failed during Seek.");
        }
        object[] roster = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] Initial() => [Actor(1, 0x1A6F, default, combat: true, hitboxRadius: 6.2f),
            Actor(2, 0x233C, new(2, 0, 3)), Actor(100, 0, new(1, 0, 1)), Actor(101, 0, new(10, 0, 0))];
        object Spell(uint action, double t, Vector3? p = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, p ?? new(2, 0, 3), .4f, target, t))!;
            node["TotalTime"] = 3; node["IsSpell"] = true;
            return node;
        }
        string Raw(double t, uint action, ulong target = 100, byte type = 1)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 1ul, MainTargetId = target, ActionId = action, ActionType = type, Timestamp = Start.AddSeconds(t) } });
        string Icon(double t, ulong source, uint icon)
            => JsonSerializer.Serialize(new { t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = source, TargetId = 999ul, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        string Director(double t, uint command)
            => JsonSerializer.Serialize(new { t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
                data = new { Command = command, Timestamp = Start.AddSeconds(t) } });

        var path = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(path), "AlteRoite metadata requires real FFXIV_SQPACK.");
        using (var game = new Lumina.GameData(path!, new Lumina.LuminaOptions
            { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified }))
            Check(game.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>()!.GetRow(252).TerritoryType.RowId == 691
                && game.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()!.GetRow(691).ContentFinderCondition.RowId == 252,
                "AlteRoite CFC252/T691 bidirectional map.");
        Check((bool)manager.IsScriptEnabled(id), "AlteRoite default enable.");
        manager.SetScriptEnabled(id, false); manager.RefreshCatalog();
        Check(!(bool)manager.IsScriptEnabled(id), "AlteRoite explicit disable survives refresh.");
        manager.SetScriptEnabled(id, true);
        Console.WriteLine("PASS native AlteRoite client CFC252 T691 exact ownership default enabled explicit disable refresh");

        foreach (var action in new uint[] { 9186, 9176, 9182, 7896 })
        {
            load(id, [Header(691), Frame(691, 0, Initial(), true, party: roster),
                Frame(691, .1, [Actor(2, 0x233C, new(2, 0, 3), cast: Spell(action, .1))], party: roster),
                CancelHoH(.2, 2, action), Raw(.3, action), Frame(691, 20, [], party: roster),
                Frame(691, 21, [Actor(2, 0x233C, default)], party: roster)]);
            Seek(20.1);
            var ordinary = action is 9186 or 9176;
            Check(dangers() == (ordinary ? 1 : 0), "Ordinary or KB hard navigation boundary.");
            Check(Items(ordinary ? "Ordinary" : "Knockbacks").Length == 1, "Packet Cancel/Effect/timeout changed raw lifetime.");
            if (!ordinary) Check(Shown("alteroite/knockback/").Length > 0, "KB missing display projection.");
            Seek(21.1); Check(dangers() == 0 && Items("Ordinary").Length == 0 && Items("Knockbacks").Length == 0, "Real raw Finish.");
            Seek(.15); Check(Items(ordinary ? "Ordinary" : "Knockbacks").Length == 1, "Inverse Seek did not rebuild.");
            stop();
            Console.WriteLine($"PASS native AlteRoite action {action} raw clock lock watched Finish cancel effect timeout independent projection navigation inverse Seek owner");
        }

        load(id, [Header(691), Frame(691, 0, Initial(), true, party: roster),
            Frame(691, .1, [Actor(10, 0x1A71, new(5, 0, 5))], party: roster),
            Frame(691, 2.1, [Actor(11, 0x1A71, new(-5, 0, -5))], party: roster),
            Frame(691, 3, [Actor(1, 0x1A6F, default, combat: true, hitboxRadius: 6.2f, cast: Spell(7896, 3))], party: roster),
            Frame(691, 5, [Actor(1, 0x1A6F, default, combat: true, hitboxRadius: 6.2f, cast: Spell(9182, 5))], party: roster),
            Frame(691, 12, [Actor(10, 0x1A71, new(9.123f, 0, 7.456f), dead: true)], party: roster),
            Frame(691, 20, [], party: roster), Frame(691, 21, [], removed: [10, 11], party: roster)]);
        Seek(2.2); Check(Items("Orbs").Length == 2 && Shown("alteroite/aoe/").Length == 0, "Orb delayed visibility.");
        Seek(12.1); Check(Shown("alteroite/aoe/").Length == 2 && dangers() == 2, "Driver timing or dead moving orb visibility.");
        Seek(20.1); Check(dangers() == 2, "Predicted orb timeout consumed.");
        Seek(21.1); Check(dangers() == 0 && Items("Orbs").Length == 0, "Actual orb destruction.");
        Seek(12.1); Check(dangers() == 2, "Orb reverse Seek.");
        stop();
        Console.WriteLine("PASS native AlteRoite moving fire orbs warning window retained past prediction dead actors driver precedence destruction inverse Seek owner");

        load(id, [Header(691), Frame(691, 0, Initial(), true, party: roster),
            Icon(.1, 100, 62), Icon(.2, 100, 62), Icon(.3, 101, 108), Icon(.4, 101, 108),
            Raw(.5, 9178, 101), Raw(.6, 9177, 555), Raw(.7, 9185, 555),
            Frame(691, .8, [Actor(101, 0, new(10, 0, 0), dead: true)], party: roster),
            Frame(691, .9, [Actor(101, 0, new(10, 0, 0))], party: roster),
            Raw(1, 9185), Raw(1.1, 9185), Icon(1.2, 100, 62), Raw(1.3, 9185),
            JsonSerializer.Serialize(new { t = 1.4, kind = "BattleEnded",
                type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent", data = new { Timestamp = Start.AddSeconds(1.4) } }),
            Director(1.6, 0x40000005)]);
        Seek(.45); Check(Items("Stacks").Length == 2 && Items("Spreads").Length == 2, "Duplicate source icons or wrong TargetId.");
        Seek(.55); Check(Items("Spreads").Length == 2, "9178 consumed spread.");
        Seek(.75); Check(Items("Stacks").Length == 1 && Items("Spreads").Length == 1, "Independent FIFO fallback.");
        Seek(.95); Check(Items("Spreads").Length == 0 && Items("Stacks").Length == 1, "Dead spread Update removal/revival.");
        Seek(1.45); Check(Items("Stacks").Length == 1 && (int)Read(Model(), "StackCounter") == 2
            && !(bool)Read(Script(), "_suspended"), "Empty Effect counter or BattleEnded history.");
        Seek(1.7); Check(dangers() == 0 && Items("Stacks").Length == 0, "Director cleanup.");
        stop();
        Console.WriteLine("PASS native AlteRoite repeated source icons 9177 only FIFO fallback empty stack counter death spread Update BattleEnded Director owner");

        load(id, [Header(691), Frame(691, 0, [Actor(1, 0x1A6F, default, combat: true),
                Actor(2, 0x233C, default), Actor(100, 0, new(19, 0, 0), stackedStatus: 911, rotation: MathF.PI / 2),
                Actor(101, 0, default)], true, party: roster),
            Frame(691, .4, [Actor(100, 0, new(19, 0, 0))], party: roster)]);
        Seek(.2); Check((ulong)Read(Model(), "IceMask") == 1 && Shown("alteroite/ice/").Length > 0
            && dangers() == 0, "Cold ThinIce mask/projection or hard navigation.");
        Seek(.5); Check((ulong)Read(Model(), "IceMask") == 0 && Shown("alteroite/ice/").Length == 0, "ThinIce status loss.");
        stop();
        Console.WriteLine("PASS native AlteRoite cold status ThinIce mask six-yalm stop-wall projection no hard navigation status loss owner");

        load(id, [Header(691), Frame(691, 0, Initial(), true, party: roster), Icon(.1, 100, 62),
            Frame(691, .2, [], party: roster), Frame(691, 1, [], party: roster)]);
        Seek(.2);
        Check(Items("Stacks").Length == 1 && dangers() == 0 && Shown("alteroite/stack/").Length == 1,
            "Own-stack buddy complement must preserve actual source converter coverage rejection.");
        Check(Predictions().Length == 0, "Default stack invented Shared damage.");
        stop();
        Console.WriteLine("PASS native AlteRoite own-stack R3 buddy model display retained source converter coverage rejection no hard navigation");

        load(id, [Header(691), Frame(691, 0, Initial(), true, party: roster), Icon(.1, 101, 62),
            Frame(691, .2, [], party: roster), Frame(691, 1, [], party: roster)]);
        Seek(.2);
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [new Vector3(1, 0, 1), 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && Read((object)result, "TargetPosition") is Vector3,
            $"Real stack buddy solver: {result.DescribeOutcome()}, dangers={dangers()}, stacks={Items("Stacks").Length}.");
        var end = (Vector3)Read((object)result, "TargetPosition");
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance(end, new(10, 0, 0)) <= 6.01f, "Real unmarked viewer joins stack R6 solver.");
        Check(Predictions().Length == 0, "Default stack invented Shared damage.");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"), "Replay emitted facing.");
        const string mechanism = "欧米茄时空狭缝 德尔塔幻境1";
        manager.SetMechanismEnabled(id, mechanism, false); Seek(.21);
        Check(dangers() == 0 && Shown("alteroite/").Length == 0, "Mechanism off cleanup.");
        manager.SetMechanismEnabled(id, mechanism, true);
        controller.Play(); controller.Update(.2f); controller.Pause();
        Check(Items("Stacks").Length == 0, "Cold activation replayed historical icon.");
        stop();
        Console.WriteLine("PASS native AlteRoite real unmarked R6 join stack solver no fallback no invented Shared damage replay isolation mechanism off owner cold no historical icon");
    }
}
