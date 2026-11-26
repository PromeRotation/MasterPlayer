using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunWickedThunder(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.raid.wickedthunder.normal";
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
        object Model() => Read(Encounter(), "WickedThunder");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        object[] Predictions() => ((IEnumerable)Read(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        void Seek(double at)
        {
            Check((int)controller.RouterErrorCount == 0, "WickedThunder callback failed before Seek rebuild.");
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0, "WickedThunder callback failed during Seek.");
        }
        DateTime SourceClock(double start, double extra)
        {
            var type = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.ActorCastInfo")!;
            var value = Activator.CreateInstance(type)!;
            type.GetField("TotalTime")!.SetValue(value, 3f);
            return Start.AddSeconds(start).AddSeconds((float)type.GetProperty("NPCRemainingTime")!.GetValue(value)! + extra);
        }
        var center = new Vector3(100, 0, 100);
        object[] roster = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        object[] Initial(bool combat = true) => [Actor(1, 0x4263, center, combat: combat),
            Actor(2, 0x233C, center), Actor(3, 0x4264, center + Vector3.UnitZ * 5),
            .. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, center + Vector3.UnitX * (i * .1f)))];
        object Spell(uint action, double start, ulong target = 0, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, center, rotation, target, start))!;
            node["IsSpell"] = true;
            node["TotalTime"] = 3f;
            return node;
        }
        string Icon(double t, ulong source, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = 999ul, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        string Effect(double t, uint action, ulong source = 3, ulong target = 0, uint sequence = 0)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, MainTargetId = target, ActionId = action, ActionType = 1,
                    GlobalSequence = sequence, Timestamp = Start.AddSeconds(t) } });
        string Map(double t, ushort p1, ushort p2) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 0u, Param1 = p1, Param2 = p2, Timestamp = Start.AddSeconds(t) } });
        object CannonStatus(object actor, int slot, ushort param)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(new[] {
                new { Id = 2970u, Param = param, SourceId = 1ul, SlotIndex = slot, ExpiresAt = Start.AddSeconds(60) } });
            return node;
        }

        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(sqpack), "WickedThunder metadata verification requires FFXIV_SQPACK.");
        using (var game = new Lumina.GameData(sqpack!, new Lumina.LuminaOptions {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified }))
        {
            var cfc = game.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>()!.GetRow(991);
            var territory = game.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()!.GetRow(1231);
            Check(cfc.TerritoryType.RowId == 1231 && territory.ContentFinderCondition.RowId == 991,
                "WickedThunder CFC991 / Territory1231 bidirectional metadata differs.");
            Console.WriteLine("PASS native WickedThunder client metadata CFC991 Territory1231 bidirectional exact source OID4263 catalog");
        }
        load(id, [Header(1231), Frame(1231, 0, Initial(false), full: true, party: roster),
            Frame(1231, .1, [Actor(1, 0x4263, center, combat: true),
                Actor(2, 0x233C, center, cast: Spell(37560, .1, rotation: .4f))], party: roster),
            CancelHoH(.2, 2, 37560),
            Frame(1231, 20, [], party: roster),
            Frame(1231, 21, [Actor(2, 0x233C, center)], party: roster)]);
        Seek(.05);
        Check(Shown("wicked/enemy").Length == 1 && Shown("wicked/arena").Length == 1 && dangers() == 0,
            "WickedThunder precombat outputs.");
        Seek(20.1);
        Check(Items("Ordinary").Length == 1 && Shown("wicked/aoe/").Length == 1 && dangers() == 1,
            "WickedThunder packet cancellation/timeout changed raw-only lifecycle.");
        Seek(21.1);
        Check(Items("Ordinary").Length == 0 && dangers() == 0, "WickedThunder raw watched Finish.");
        Seek(.15);
        Check(Items("Ordinary").Length == 1 && dangers() == 1, "WickedThunder reverse Seek.");
        stop();
        Console.WriteLine("PASS native WickedThunder precombat display raw-only watched Finish cancellation timeout lock inverse Seek owner");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Frame(1231, .1, [Actor(2, 0x233C, center, cast: Spell(37549, .1))], party: roster),
            Frame(1231, .2, [CannonStatus(Actor(100, 0, center), 59, 0x2D3),
                CannonStatus(Actor(101, 0, center + Vector3.UnitX), 4, 0x2D4)], party: roster),
            Effect(.3, 20032), Effect(.4, 37551)]);
        Seek(.25);
        Check(Items("Cannons").Length == 2 && Shown("wicked/aoe/").Length == 2 && dangers() == 1,
            "WickedThunder status slots / front-two navigation.");
        Check((float)Read(Items("Cannons")[0], "Rotation") == MathF.PI
            && (DateTime)Read(Items("Cannons")[0], "Activation") == Start.AddSeconds(.2).AddSeconds(9.5f),
            "WickedThunder real status slot orientation/clock.");
        var angleType = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.Angle")!;
        var cardinals = (Array)angleType.GetField("AnglesCardinals")!.GetValue(null)!;
        Check((float)Read(Items("Cannons")[1], "Rotation") == (float)Read(cardinals.GetValue(1)!, "Rad"),
            "WickedThunder second cannon lost the source cardinal micro-angle.");
        Seek(.35);
        Check(Items("Cannons").Length == 1 && dangers() == 1, "WickedThunder cannon promotion.");
        Seek(.45);
        Check(Items("Cannons").Length == 0 && dangers() == 0, "WickedThunder native Effect consumption.");
        stop();
        Console.WriteLine("PASS native WickedThunder real slots59 and4 delay angle first2 display first1 risk native cross-source effects promotion owner");

        var huntRecords = new List<string> { Header(1231), Frame(1231, 0, Initial(), full: true, party: roster) };
        for (var i = 0; i < 14; ++i)
        {
            var t = .1 + i * .1;
            huntRecords.Add(Frame(1231, t, [Actor(2, 0x233C, center, cast: Spell(37557, t))], party: roster));
            huntRecords.Add(Frame(1231, t + .05, [Actor(2, 0x233C, center)], party: roster));
        }
        huntRecords.Add(Effect(2, 37558));
        load(id, huntRecords);
        Seek(1.5);
        Check(Items("Hunts").Length == 14 && Shown("wicked/aoe/").Length == 14 && dangers() == 12,
            "WickedThunder Hunt full display / risk prefix.");
        Check(Items("Hunts").Select((item, i) => (DateTime)Read(item, "Activation") == SourceClock(.1 + i * .1, 6.3d)).All(x => x),
            "WickedThunder Hunt must add extra delay before one DateTime conversion.");
        Seek(2.1);
        Check(Items("Hunts").Length == 13 && Shown("wicked/aoe/").Length == 13 && dangers() == 12,
            "WickedThunder Hunt hidden promotion.");
        stop();
        Console.WriteLine("PASS native WickedThunder Hunt14 display first12 risk native effect FIFO hidden promotion original clocks owner");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Icon(.1, 100, 316), Icon(.11, 101, 161),
            .. Enumerable.Range(0, 4).Select(i => Effect(.2 + .01 * i, 37571, target: 102)),
            Effect(.3, 37569, target: 999), Effect(.4, 37571, target: 102)]);
        Seek(.15);
        Check(Items("Stacks").Length == 2 && Shown("wicked/stack/").Length == 2 && Predictions().Length == 0,
            "WickedThunder independent exact8 stacks Source icons / invented prediction.");
        Seek(.25);
        Check(Items("Stacks").Length == 2
            && (ulong)Read(Items("Stacks")[0], "TargetId") == 102, "WickedThunder four hits/target transfer.");
        Seek(.35);
        Check(Items("Stacks").Length == 1 && (uint)Read(Items("Stacks")[0], "Action") == 37571,
            "WickedThunder independent one-hit component.");
        Seek(.45);
        Check(Items("Stacks").Length == 0, "WickedThunder fifth hit.");
        stop();
        Console.WriteLine("PASS native WickedThunder Source icon target transfer exact8 independent5 and1 counters no invented damage native effects owner");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Frame(1231, .1, [Actor(2, 0x233C, center, cast: Spell(37576, .1, target: 101))], party: roster),
            Frame(1231, .2, [Actor(2, 0x233C, center), Actor(101, 0, center + Vector3.UnitX * 10)], party: roster),
            Effect(.3, 37576), Effect(.4, 37576, source: 2)]);
        Seek(.25);
        Check(Items("Baits").Length == 1 && Shown("wicked/bait/").Length == 1 && dangers() == 1,
            "WickedThunder dynamic Jolt finish retention.");
        Seek(.35);
        Check(Items("Baits").Length == 1, "WickedThunder wrong-source Effect consumed Jolt.");
        Seek(.45);
        Check(Items("Baits").Length == 0 && dangers() == 0, "WickedThunder Jolt native watched effect.");
        stop();
        Console.WriteLine("PASS native WickedThunder dynamic original Jolt target full60 raw Finish retains wrong-source isolation native effect owner");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Map(.1, 1, 2), Frame(1231, .2, [Actor(2, 0x233C, center, cast: Spell(37553, .2))], party: roster),
            Frame(1231, .3, [Actor(2, 0x233C, center)], party: roster),
            .. Enumerable.Range(0, 10).Select(i => Effect(10 + i * .1, 37555))]);
        Seek(10.85);
        Check(Read(Model(), "Hyper") != null && Shown("wicked/aoe/").Length == 1 && dangers() == 0
            && Predictions().Length == 8, "WickedThunder safe Hyper ten-hit boundary / damage.");
        Check((DateTime)Read(Read(Model(), "Hyper"), "Activation") == SourceClock(.2, .6d),
            "WickedThunder Hyper must preserve the original source clock after nine effects.");
        Seek(11);
        Check(Read(Model(), "Hyper") == null && Predictions().Length == 0, "WickedThunder tenth Hyper effect.");
        stop();
        Console.WriteLine("PASS native WickedThunder map reduced platform safe Hyper independent tenth effect damage diagnostic owner");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Frame(1231, .1, [Actor(2, 0x233C, center, cast: Spell(37557, .1))], party: roster),
            Frame(1231, .2, [Actor(2, 0x233C, center)], party: roster), Icon(.21, 100, 316),
            JsonSerializer.Serialize(new { t = .3, kind = "BattleEnded",
                type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent", data = new { Timestamp = Start.AddSeconds(.3) } }),
            Effect(.4, 37558),
            JsonSerializer.Serialize(new { t = .5, kind = "DirectorUpdate",
                type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
                data = new { Command = 0x40000005u, Timestamp = Start.AddSeconds(.5) } })]);
        Seek(.35);
        Check(Items("Hunts").Length == 1 && Items("Stacks").Length == 1
            && !(bool)Read(Script(), "_suspended"), "WickedThunder BattleEnded discarded living primary history.");
        Seek(.45);
        Check(Items("Hunts").Length == 0 && Items("Stacks").Length == 1,
            "WickedThunder Effect input stopped after BattleEnded.");
        Seek(.55);
        Check(!(bool)Read(Model(), "Active") && Items("Stacks").Length == 0 && dangers() == 0,
            "WickedThunder Director wipe failed to unload history.");
        stop();
        Console.WriteLine("PASS native WickedThunder actual script BattleEnded preserves living boss queues and effect routing Director wipe clears history owner");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Frame(1231, .1, [Actor(2, 0x233C, center, cast: Spell(37575, .1)),
                Actor(101, 0, center, dead: true)], party: roster),
            Frame(1231, 20, [], party: roster),
            Frame(1231, 21, [Actor(2, 0x233C, center)], party: roster)]);
        Seek(20.1);
        var clockType = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.ActorCastInfo")!;
        var clock = Activator.CreateInstance(clockType)!;
        clockType.GetField("TotalTime")!.SetValue(clock, 3f);
        clockType.GetField("ElapsedTime")!.SetValue(clock, 3f);
        var expectedHit = ((DateTime)runtime.CurrentTime).AddSeconds((float)clockType.GetProperty("NPCRemainingTime")!.GetValue(clock)!);
        Check(Predictions().Select(p => (ulong)Read(p, "TargetId")).SequenceEqual(new ulong[] { 100, 102, 103, 104, 105, 106, 107 })
            && Predictions().All(p => (DateTime)Read(p, "Activation") == expectedHit),
            "WickedThunder production saturated clock/roster differs from actual source.");
        Check(Shown("wicked/aoe/").Length == 0 && dangers() == 0
            && ((string?)Read(Script(), "PredictedDamageText"))?.Contains("7人") == true,
            "WickedThunder diagnostic-only Raidwide missing or invented navigation.");
        Seek(21.1);
        Check(Predictions().Length == 0, "WickedThunder Raidwide real Finish.");
        stop();
        Console.WriteLine("PASS native WickedThunder production damage seven living identities exact source NPCRemainingTime ticks raw Finish diagnostic only");

        load(id, [Header(1231), Frame(1231, 0, Initial(), full: true, party: roster),
            Frame(1231, .1, [Actor(2, 0x233C, center, cast: Spell(37574, .1))], party: roster)]);
        Seek(.2);
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback"),
            "WickedThunder production solver failed actual R5 constraint.");
        var endpoint = (Vector3)Read((object)result, "TargetPosition");
        Check(Vector2.Distance(new(endpoint.X, endpoint.Z), new(100, 100)) > 5, "WickedThunder solver endpoint in danger.");
        var hints = Read(Script(), "Hints");
        Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest"), "WickedThunder replay sent real heading.");
        stop();
        Console.WriteLine("PASS native WickedThunder real production SafeZoneSolver R5 escape no least-danger fallback replay control isolation owner");
    }
}
