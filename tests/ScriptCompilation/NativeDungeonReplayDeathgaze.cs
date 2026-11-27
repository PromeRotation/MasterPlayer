using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDeathgazeReplay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        RunAllianceStandardReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers, deathgaze: true);
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.deathgazehollow";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object? Read(object obj, string key)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(key, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(key, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, key);
        }
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Rings() => previews().Where(p => ((string)Read(p, "Id")!).Contains("/deathgaze/green/")).ToArray();
        object[] Markers() => previews().Where(p => ((string)Read(p, "Id")!).Contains("/deathgaze/interesting/")).ToArray();
        var center = new Vector3(300, 0, 410);
        object Boss(bool combat = true) => Actor(1, 0x190F, center, combat: combat, subKind: 5);
        object Player(ulong actor = 100, bool dead = false) => Actor(actor, 0, center + Vector3.UnitX * 10, dead: dead, subKind: 4);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong viewer = 100, ulong slot0 = 100, ulong[]? removed = null)
        {
            var node = JsonNode.Parse(Frame(627, at, actors, full, party:
                [new { Slot = 0, EntityId = slot0 }, new { Slot = 1, EntityId = 101UL },
                 new { Slot = 23, EntityId = 102UL }, new { Slot = 24, EntityId = 103UL }, new { Slot = 63, EntityId = 104UL }],
                sourcePlayerId: 100, removed: removed))!;
            node["data"]!["LocalPlayerId"] = viewer;
            return node.ToJsonString();
        }
        string Icon(double at, ulong actor, ulong target = 101) => JsonSerializer.Serialize(new
        {
            t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = target, MarkerId = 70, Timestamp = Start.AddSeconds(at) },
        });
        string Effect(double at, ulong target, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, MainTargetId = target, ActionId = 7308, ActionType = 1,
                GlobalSequence = (uint)(at * 100), Timestamp = Start.AddSeconds(at) },
        });
        string Director(double at, uint command) => JsonSerializer.Serialize(new
        {
            t = at, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Deathgaze actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void NoNavigation()
        {
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0,
                "Deathgaze icon spread has no invented danger or join goal");
            Check(Items(Script(), "PredictedDamage").Length == 0, "Deathgaze icon spread no invented damage prediction");
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Deathgaze replay never executes facing target or action hold");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Deathgaze complete owner cleanup");
        }
        load(id, [Header(627), World(0, [Boss(), Player(), Player(101), Player(102), Player(103), Player(104), Player(105),
                Actor(20, 0x1911, center, subKind: 5)], true),
            Icon(1, 100, 101), World(1.1, []), Icon(2, 102, 100), World(2.1, []),
            Icon(3, 103), World(3.1, []), Icon(4, 104), World(4.1, []),
            World(5, [Player(102, dead: true)]), Effect(6, 102), World(6.1, []),
            World(7, [], viewer: 105, slot0: 105), Effect(8, 100), World(8.1, [], viewer: 105, slot0: 105),
            Effect(9, 105, source: 101), World(9.1, [], viewer: 105, slot0: 105),
            Effect(10, 105), World(10.1, [], viewer: 105, slot0: 105), World(1000, [], viewer: 105, slot0: 105)]);
        Seek(1.1);
        Check((ulong)Read(Model(), "DeathgazeGreenTargets")! == 1 && Rings().Length == 1 && Markers().Length == 0,
            "Deathgaze actual Icon.Source ID controls slot, not TargetId");
        Seek(4.1); NoNavigation();
        Check(Rings().Length == 4 && Markers().Length == 3, "Deathgaze draws alive slots23/24/63 in order, own marker remains PC");
        Check(Rings().Select(p => ((string)Read(p, "Id")!).Split('/').Last()).SequenceEqual(new[] { "0", "23", "24", "63" }),
            "Deathgaze foreground publishes exact slot order");
        var hint = (string)Read(Script(), "MechanicHintsText")!;
        Check(hint.Contains("Spread! (debuff: green)") && !hint.Contains("已满足"),
            "Deathgaze actual public green Hint is risky near slot23");
        foreach (var ring in Rings())
        {
            var own = ((string)Read(ring, "Id")!).EndsWith("/0", StringComparison.Ordinal);
            Check((bool)Read(ring, "Safe")! && (bool)Read(ring, "OutlineOnly")! && (bool)Read(ring, "Unclipped")!
                && (float)Read(Read(ring, "Geometry")!, "Radius")! == 5
                && (Vector3)Read(Read(ring, "Geometry")!, "Position")! == center + Vector3.UnitX * 10
                && (float?)Read(ring, "StrokeThickness") == (own ? 2f : 1f)
                && (Vector4?)Read(ring, "Color") == new Vector4(0, 1, 0, 1),
                "Deathgaze actual source safe outline radius color own2 other1");
            var styleMethod = ring.GetType().GetMethod("Style", flags)!;
            var style = styleMethod.Invoke(ring, [Activator.CreateInstance(styleMethod.GetParameters()[0].ParameterType)])!;
            Check((float)Read(style, "StrokeThickness")! == (own ? 2 : 1)
                && (Vector4?)Read(style, "FillColor") == Vector4.Zero,
                "Deathgaze common radar/world style retains source line width and no fill");
        }
        Check(Markers().All(p => (float)Read(Read(p, "Geometry")!, "Radius")! == .5f
            && (Vector4?)Read(p, "Color") == new Vector4(192 / 255f, 192 / 255f, 192 / 255f, 1)),
            "Deathgaze Interesting priority uses native point marker display adaptation");
        Seek(5.1); NoNavigation();
        Check(Rings().Length == 3 && ((ulong)Read(Model(), "DeathgazeGreenTargets")! & (1UL << 23)) != 0,
            "Deathgaze dead hides foreground while slot bit survives");
        hint = (string)Read(Script(), "MechanicHintsText")!;
        Check(hint.Contains("Spread! (debuff: green)") && hint.Contains("已满足"),
            "Deathgaze actual public green Hint excludes dead23 and slots24/63");
        Seek(6.1);
        Check(((ulong)Read(Model(), "DeathgazeGreenTargets")! & (1UL << 23)) == 0, "Deathgaze Effect clears MainTarget current slot");
        Seek(7.1);
        Check((ulong)Read(Script(), "CurrentPlayerId")! == 105 && Rings().Length == 3,
            "Deathgaze viewer replacement same slot inherits green history");
        Seek(8.1); Check(((ulong)Read(Model(), "DeathgazeGreenTargets")! & 1) != 0,
            "Deathgaze old ID absent current Party cannot clear replacement slot");
        Seek(9.1); Check(((ulong)Read(Model(), "DeathgazeGreenTargets")! & 1) != 0 && Rings().Length == 3,
            "Deathgaze source module rejects Player Effect");
        Seek(10.1); Check(((ulong)Read(Model(), "DeathgazeGreenTargets")! & 1) == 0 && Rings().Length == 2,
            "Deathgaze valid nonprimary NPC Effect clears replacement MainTarget slot");
        hint = (string)Read(Script(), "MechanicHintsText")!;
        Check(hint.Contains("Spread! (debuff: none)") && hint.Contains("已满足"),
            "Deathgaze actual public none Hint still appears without own green");
        Seek(999); NoNavigation();
        Check(Rings().Length == 2 && Markers().Length == 2, "Deathgaze remaining green slots never expire by timeout");
        Seek(4.1);
        manager.SetMechanismEnabled(id, "虚空死亡凝视", false); Seek(4.2);
        Check(previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Deathgaze mechanism disable releases markers and boundary");
        manager.SetMechanismEnabled(id, "虚空死亡凝视", true); Seek(4.1);
        Check(Rings().Length == 4, "Deathgaze reverse Seek rebuilds every Icon in order");
        Release();
        Console.WriteLine("PASS native Deathgaze actual Icon source MainTarget Effect Party64 dead slot replacement own line width Interesting markers no navigation inverse Seek");

        load(id, [Header(627), World(0, [Boss(false), Player(), Player(101)], true),
            Icon(1, 100), World(2, [Boss()]), World(3, [])]);
        Seek(2.1); NoNavigation();
        Check(Rings().Length == 0 && (ulong)Read(Model(), "DeathgazeGreenTargets")! == 0,
            "Deathgaze cold activation cannot restore historical Icon");
        Release();
        Console.WriteLine("PASS native Deathgaze actual cold Icon before activation no fabricated recovery owner replay isolation");

        load(id, [Header(627), World(0, [Boss(), Player(), Player(101),
                Actor(102, 0, center + new Vector3(40, 0, 25), subKind: 4),
                Actor(103, 0, center, subKind: 4, targetable: false)], true),
            Icon(.1, 100), Icon(.2, 102), Icon(.3, 103), World(.4, []),
            World(1, [], removed: [102]), World(2, []), Director(3, 0x40000005), World(3.1, []),
            Director(4, 0x40000011), World(4.1, []), World(5, [], removed: [1]),
            World(6, [Boss()]), World(7, [])]);
        Seek(.5); NoNavigation();
        Check(Rings().Length == 3 && Markers().Length == 1
            && (Vector3)Read(Read(Markers().Single(), "Geometry")!, "Position")! == center + new Vector3(30, 0, 15),
            "Deathgaze priority point excludes own and untargetable, clamps outside source rectangle");
        var version = (long)Read(Model(), "LoadVersion")!;
        Seek(1.1);
        Check(Rings().Length == 2 && Markers().Length == 0
            && ((ulong)Read(Model(), "DeathgazeGreenTargets")! & (1UL << 23)) != 0,
            "Deathgaze destroyed Party actor hidden without clearing green history");
        Seek(3.2);
        Check(!(bool)Read(Model(), "Active")! && (ulong)Read(Model(), "DeathgazeGreenTargets")! == 0
            && Rings().Length == 0 && Markers().Length == 0 && dangers() == 0,
            "Deathgaze Director clears mask and every foreground output");
        Seek(4.2); Check(!(bool)Read(Model(), "Active")!, "Deathgaze unlock cannot revive retired primary");
        Seek(6.1);
        Check((bool)Read(Model(), "Active")! && (long)Read(Model(), "LoadVersion")! > version
            && (ulong)Read(Model(), "DeathgazeGreenTargets")! == 0 && Rings().Length == 0,
            "Deathgaze fresh primary load has no historical icon");
        Release();
        Console.WriteLine("PASS native Deathgaze actual priority display filter clamp destroyed history Director retired generation fresh load");

        const string otherId = "masterplayer.alliance.ferdiadhollow";
        var definitions = ((IEnumerable)assembly.GetType("MasterPlayer.Mechanics.Bozja.AllianceStandardCatalog")!
            .GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>();
        var other = definitions.Single(d => (uint)Read(d, "PrimaryOid")! == 0x1928);
        var otherCenter = (Vector3)Read(other, "Center")!;
        manager.SetScriptEnabled(otherId, true);
        load(id, [Header(627), World(0, [Boss(), Actor(100, 0, (center + otherCenter) * .5f, subKind: 4),
            Actor(2, 0x1928, otherCenter, combat: true, subKind: 5),
            Actor(20, 0x1911, center, cast: Cast(7312, 5, center, startedAt: 0), subKind: 5),
            Actor(21, 0x1911, otherCenter, cast: Cast(7339, 5, otherCenter, startedAt: 0), subKind: 5)], true),
            Icon(.1, 100), World(1, [])]);
        Seek(.3); Check(dangers() == 2 && Rings().Length == 1,
            $"Deathgaze dual owners publish AOE and green marker: danger={dangers()} rings={Rings().Length} previews="
            + string.Join(",", previews().Select(p => Read(p, "Id"))) + " running=" + string.Join(",", manager.Running.Keys));
        manager.SetScriptEnabled(id, false); controller.Seek(.4);
        Check(dangers() == 1 && Rings().Length == 0 && manager.Running.ContainsKey(otherId), "Deathgaze release preserves Ferdiad");
        manager.SetScriptEnabled(id, true); Seek(.3); Check(dangers() == 2 && Rings().Length == 1, "Deathgaze reverse Seek rebuilds both owners");
        manager.SetScriptEnabled(otherId, false); Seek(.4); Check(dangers() == 1 && Rings().Length == 1, "Ferdiad release preserves Deathgaze");
        Release(); manager.SetScriptEnabled(otherId, true);
        Console.WriteLine("PASS native Deathgaze actual same territory Ferdiad dual owner bidirectional release with active green markers");
    }
}
