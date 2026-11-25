using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class PalaceGudannaRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -220);
    internal static void Run(Action<string, Action> run)
    {
        run("Palace Gudanna exact native active rules no Maelstrom cast or invented damage", Rules);
        run("Palace Gudanna native effect eight rotation no cast finish or legacy clock advancement", Rotation);
        run("Palace Gudanna precise weak positioning score thresholds source identity no new navigation", Positioning);
        run("Palace Gudanna all tornado raw obstacles death state7 movement and real destroy", Tornadoes);
        run("Palace Gudanna constructed guide shared module lifetime reset director and pending", Guide);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(595);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(bool combat = true)
        {
            Actors = [new(1, 0x1816, Center, InCombat: combat, TargetId: 100, Name: "古丹纳"),
                new(10, 0x18F0, Center + Vector3.UnitX * 12),
                new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0, Player: true)];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, null, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change) => Actors[Actors.FindIndex(a => a.Id == id)] = change(Actors.Single(a => a.Id == id));
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Rules()
    {
        var f = new Fixture();
        var rules = f.E.Definitions.Single().Casts;
        Check(rules.Length == 4 && rules.Single(r => r.Action == 7095).Shape == new FanTemplate(16.6f, 2 * MathF.PI / 3)
            && rules.Single(r => r.Action == 7098).Shape == new FanTemplate(51.6f, MathF.PI / 3)
            && rules.All(r => r.Action is not (7164 or 7167 or 6497)), "Inactive Maelstrom cast/auto added or exact cones lost.");
        Check(rules.Single(r => r.Action == 7099).Shape == null && !f.E.PredictedDamage.Any(),
            "CastHint became Raidwide prediction or danger circle.");
    }
    private static void Rotation()
    {
        var f = new Fixture(false);
        f.E.Effect(99, 7095, 1, f.Now);
        Check(f.E.GudannaCastIndex == 0, "Inactive rotation consumed effects.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        f.E.StartCast(1, new(7095, Center, 0, Start, Start.AddSeconds(3), 100), Start);
        f.E.Finish(1, 7095, Start.AddSeconds(3));
        Check(f.E.GudannaCastIndex == 0, "Cast Finish advanced event counter.");
        for (uint i = 1; i <= 8; ++i)
        {
            f.E.Effect(99, i % 2 == 0 ? 7096u : 7098u, i, f.Now);
            Check(f.E.GudannaCastIndex == i % 8, "Eight-effect loop lost.");
        }
        f.E.Effect(99, 7095, 0, f.Now);
        f.E.Effect(99, 7095, 0, f.Now);
        Check(f.E.GudannaCastIndex == 2, "Sequence zero independent events merged.");
        f.Set(1, a => a with { Hp = 1 });
        f.Sync();
        Check(!f.E.GudannaRotationDisabled, "Invented HP threshold disabled cycle.");
        f.E.Effect(99, 7099, 15, f.Now);
        f.E.Effect(99, 7095, 16, f.Now);
        Check(f.E.GudannaRotationDisabled && f.E.GudannaPositioning == null && f.E.GudannaCastIndex == 2,
            "Meteor did not latch rotation disabled.");
        f.E.ResetFight();
        f.Sync();
        Check(f.E.GudannaRotationDisabled && f.E.GudannaCastIndex == 2, "Output reset reconstructed a surviving source component.");
        f.E.Synchronize(f.Actors.Where(a => a.Id != 100), null, f.Now, null, 0);
        Check(f.E.GudannaRotationDisabled && f.E.GudannaCastIndex == 2 && f.E.Active != null,
            "Absent perspective reset surviving component.");
        f.Sync();
        f.E.PalaceGuideDirector(0x40000005);
        Check(!f.E.GudannaRotationDisabled && f.E.GudannaCastIndex == 0, "Actual module unload retained cycle.");
    }
    private static void Positioning()
    {
        var f = new Fixture();
        f.E.Synchronize(f.Actors.Where(a => a.Id != 100), null, f.Now, null, 0);
        f.E.Effect(99, 7095, 77, f.Now);
        Check(f.E.GudannaCastIndex == 1, "Absent viewer stopped global effect history.");
        f.Sync();
        f.E.Effect(99, 7095, 78, f.Now);
        for (uint i = 0; i < 6; ++i) f.E.Effect(99, 7095, 80 + i, f.Now);
        var p = f.E.GudannaPositioning!;
        Check(p.NextAction == PalaceGudannaNextAction.Thunderbolt && p.Score(Center + Vector3.UnitX * 6) == .5f
            && p.Score(Center + Vector3.UnitX * 6.01f) == 0, "Raw R6 preference added hitbox/distance smoothing.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX });
        f.Sync();
        Check(ReferenceEquals(p.Source, f.E.GudannaPositioning!.Source) && p.Score(Center - Vector3.UnitX * 6) == 0,
            "Source position did not remain dynamic.");
        f.E.Effect(99, 7095, 1, f.Now);
        p = f.E.GudannaPositioning!;
        var c = p.ArenaCenter;
        Check(p.NextAction == PalaceGudannaNextAction.Charybdis && p.Score(c + Vector3.UnitX * 22) == .5f
            && p.Score(c + Vector3.UnitX * 21.99f) == 0, "Arena AABB-centered R22 complement boundary.");
        f.E.Effect(99, 7095, 2, f.Now);
        f.E.Effect(99, 7095, 3, f.Now);
        p = f.E.GudannaPositioning!;
        Check(p.NextAction == PalaceGudannaNextAction.TrounceEast && p.Score(c + Vector3.UnitX * 20) == .5f
            && p.Score(c + Vector3.UnitX * 19.99f) == 0, "East half-plane cutoff.");
        for (uint i = 4; i <= 7; ++i) f.E.Effect(99, 7095, i, f.Now);
        p = f.E.GudannaPositioning!;
        Check(p.NextAction == PalaceGudannaNextAction.TrounceWest && p.Score(c - Vector3.UnitX * 20) == .5f
            && p.Score(c - Vector3.UnitX * 19.99f) == 0, "West half-plane cutoff.");
        Check(!f.E.AmbientAoes().Any(a => a.Safe), "Source .5 weak goal expanded navigation.");
        Check(MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertGoalZones([_ => .5f],
            new BossMod.WPos(Center.X, Center.Z), 25, 0).Count == 0, "Existing bridge .5 consumption boundary changed.");
    }
    private static void Tornadoes()
    {
        var f = new Fixture();
        f.Set(10, a => a with { Dead = true, Targetable = false, EventState = 7, Position = Center + Vector3.UnitX * 3.2f });
        f.Sync();
        var a = f.E.AmbientAoes().Single(a => a.Id.StartsWith("gudanna/tornado"));
        Check(a.Obstacle && a.Shape == new CircleTemplate(10) && a.Position == f.Actors.Single(a => a.Id == 10).Position
            && f.E.Hints(Center).Contains("GTFO from voidzone!"), "Source raw all-object Tornado semantics.");
        f.Set(10, a => a with { Position = Center });
        f.Sync();
        Check(!f.E.Hints(Center + Vector3.UnitX * 10.001f).Contains("GTFO from voidzone!"),
            "Voidzone text quantized the raw player.");
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("gudanna/")), "Real destruction retained tornado.");
    }
    private static void Guide()
    {
        var f = new Fixture(false);
        Check(f.E.PalaceGuidePrecombatHint?.Contains("古丹纳") == true, "Constructor guide absent.");
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("gudanna/precombat/")) == 2
            && f.E.AmbientAoes().All(a => !a.Navigate), "Precombat arena/marker scope.");
        f.E.Synchronize(f.Actors, Center, Start, _ => false, 100);
        Check(f.E.PalaceGuidePrecombatHint == null, "Disabled guide remained visible.");
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint != null, "Settings toggle reconstructed consumed guide.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null && f.E.Active != null, "Pull did not consume constructor guide.");
        f.E.ResetFight();
        f.Set(1, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null, "Reset resurrected constructor guide.");
        f.E.PalaceGuideDirector(0x40000005);
        f.E.PalaceGuideDirector(0x40000011);
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null && f.E.Active == null, "Director resume rebuilt old input owner.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x1816, Center, Name: "新古丹纳"));
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint?.Contains("新古丹纳") == true, "New identity could not construct guide.");
    }
}
