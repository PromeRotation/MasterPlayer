using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class EurekaMinotaurRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-600, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1104);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x3DA1, Center, InCombat: true, Hp: 100),
            new(2, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 8, PartySlot: 0, Player: true),
        ];
        internal DateTime Now = Start;
        internal Fixture()
        {
            E.CurrentPartyMembers = [new(0, "First", 100)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Cast(uint action, ulong source = 2, float rotation = 0)
        {
            Now = Now.AddMilliseconds(10);
            Set(source, a => a with { Cast = new(action, a.Position, rotation, Now, Now.AddSeconds(5.3), TotalTime: 5) });
            Sync();
        }
        internal void End(ulong source = 2)
        {
            Set(source, a => a with { Cast = null });
            Sync();
        }
    }
    private static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka60 unlimited swipe FIFO original casts first two risky sticky color and remaining-count clock", Swipes);
        run("Eureka60 Shock created original quantized position thirteen seconds no TTL destroy deletion and any real Finish all-clear", Shocks);
        run("Eureka60 Groan15 immunity only cancels projection not source five-yard navigation true Finish", Groan);
        run("Eureka60 real production output both views native solver and owner cleanup", Output);
        run("Eureka60 missing viewer and source party preserves creation clocks and source risky geometry hints", MissingViewer);
    }
    private static void Swipes()
    {
        var f = new Fixture();
        for (var i = 0; i < 11; ++i)
        {
            f.Cast(31867, rotation: i * .2f);
            f.End();
        }
        var first = f.E.EurekaSwipes[0];
        Check(f.E.EurekaSwipes.Count == 11 && first.Cast is { Source: 2, Observed: true }
            && first.Activation == first.Cast.FinishAt.AddSeconds(8.7d), "Unlimited first cast model lost.");
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("eureka/swipe/")) == 2
            && f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/swipe/")).All(a => a.Navigate)
            && first.Danger && !f.E.EurekaSwipes[1].Danger, "Source first-two risk/color differs.");
        for (var i = 1; i < 11; ++i)
            Check(f.E.EurekaSwipes[i].Activation == first.Activation.AddSeconds(i * 2d), "Queue clock recomputed from current cast.");
        f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaSwipes.Count == 11, "Natural Finish or time consumed swipe.");
        f.E.Effect(1, 31872, 8, f.Now);
        f.E.Effect(1, 31868, 8, f.Now);
        Check(f.E.EurekaSwipes.Count == 9 && f.E.EurekaSwipes[0].Danger, "Repeated full effect not consumed twice.");
        var head = f.E.EurekaSwipes[0].Activation;
        f.Cast(31867);
        Check(f.E.EurekaSwipes[^1].Activation == head.AddSeconds(18), "Post-consumption append used initial ordinal.");
    }
    private static void Shocks()
    {
        var f = new Fixture();
        var born = Center + new Vector3(1.137f, 0, 2.137f);
        for (ulong id = 20; id < 38; ++id) f.Actors.Add(new(id, 0x3DA2, born, Dead: id == 20));
        f.Sync();
        Check(f.E.EurekaShocks.Count == 18 && f.E.EurekaShocks[0].Origin == DungeonEncounter.QuantizedDungeonPosition(born)
            && f.E.EurekaShocks[0].Activation == f.Now.AddSeconds(13d), "Birth clock capacity dead filtering or quantization differs.");
        var original = f.E.EurekaShocks[0].Source;
        f.Actors.RemoveAll(a => a.Id == 20); f.Now = f.Now.AddSeconds(50); f.Sync();
        f.Actors.Add(new(20, 0x3DA2, Center)); f.Sync();
        Check(f.E.EurekaShocks.Count == 19 && original.Removed
            && !ReferenceEquals(original, f.E.EurekaShocks[^1].Source), "Destroy deleted history or rebound original identity.");
        f.E.StartCast(2, new(31874, Center, 0, f.Now.AddSeconds(.1), f.Now.AddSeconds(3)), f.Now);
        f.E.Finish(2, 31874);
        Check(f.E.EurekaShocks.Count == 19, "Packet-only cancel faked component Finish.");
        f.Cast(31874); f.End();
        Check(f.E.EurekaShocks.Count == 0, "Any real Shock Finish failed whole-component clear.");
        f.Sync();
        Check(f.E.EurekaShocks.Count == 0, "Old objects re-created Shock prediction.");
    }
    private static void Groan()
    {
        var f = new Fixture(); f.Cast(31876);
        var model = f.E.EurekaGroans.Single();
        Check(model.Distance == 15 && !model.IgnoreImmunes && !model.StopAtWall && model.Cast.Observed, "Original movement parameters lost.");
        var player = f.Actors.Single(a => a.Id == 100);
        var movement = f.E.EurekaGroanMovements(player).Single();
        Check(Vector3.Distance(movement.From, movement.To) == 15 && movement.Unsafe, "Unbounded radial movement was clipped at wall.");
        f.Set(100, a => a with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = model.Cast.FinishAt.AddSeconds(1) } });
        f.Sync();
        Check(!f.E.EurekaGroanMovements(f.Actors.Single(a => a.Id == 100)).Any()
            && f.E.AmbientAoes().Any(a => a.Id == "eureka/groan/navigation"), "Immunity removed source independent navigation.");
        f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaGroans.Count == 1, "Forecast time removed movement model.");
        f.End();
        Check(f.E.EurekaGroans.Count == 0, "True Finish failed withdraw.");
    }
    private static void Output()
    {
        var f = new Fixture(); f.Cast(31876);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi();
        var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        var solution = new SafeZoneSolver().Solve(snapshot,
            new(Center + Vector3.UnitZ * 8, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
        Check(draw.Previews.Values.Any(p => p.Id.Contains("eureka/groan/"))
            && solution.IsSuccess && !solution.IsLeastDangerFallback, "Production output or solver missing.");
        output.Clear();
        Check(draw.Previews.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0, "Owner cleanup retained geometry.");
    }
    private static void MissingViewer()
    {
        var f = new Fixture();
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Now = f.Now.AddSeconds(1);
        f.Actors.Add(new(20, 0x3DA2, Center));
        f.E.Synchronize(f.Actors, null, f.Now, playerId: 0);
        var shock = f.E.EurekaShocks.Single();
        Check(shock.PreparedAt == f.Now && shock.Activation == f.Now.AddSeconds(13), "Missing viewer delayed Created.");
        f.Now = f.Now.AddSeconds(5); f.Set(20, a => a with { Position = Center + Vector3.UnitX * 8 });
        f.Actors.Add(new(100, 0, Center, PartySlot: 0, Player: true)); f.Sync();
        Check(shock.Origin == DungeonEncounter.QuantizedDungeonPosition(Center)
            && f.E.Hints(Center).Contains("GTFO from aoe!"),
            "Viewer recovery rebound birth origin or omitted source Shock warning.");
    }
}
