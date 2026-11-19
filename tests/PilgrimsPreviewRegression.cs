using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

internal static class PilgrimsPreviewRegression
{
    private static readonly DateTime Start = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Pilgrims previews PT10 consecutive jumps retain navigation and advance one circle", Leafmash);
        run("Pilgrims previews PT20 four bursts retain knockback constraints and advance one circle", Bursts);
        run("Pilgrims previews PT70 memory swipes advance without hiding independent attacks", Swipes);
        run("Pilgrims previews PT80 charges defer the final explosion and retain the half-arena attack", Lightning);
        run("Pilgrims previews simultaneous hits remain visible and expired hits do not block advancement", Simultaneous);
    }

    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position)
        => new() { Id = id, Oid = oid, Position = position, InCombat = true, Hp = 1000, MaxHp = 1000 };
    private static void Begin(PtBossEncounter encounter, params ReplayActorState[] more)
        => encounter.Synchronize([Actor(1, encounter.BossOid, encounter.Center), .. more], Start);
    private static PtBossEncounter.Cast Cast(ulong source, uint action, Vector3 position, DateTime hit, Vector3? origin = null)
        => new(source, action, position, 0, Start, hit, 100, origin);
    private static void Effect(PtBossEncounter encounter, ulong source, uint action, uint sequence, DateTime at, Vector3? position = null)
        => encounter.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action, GlobalSequence = sequence,
            EffectPosition = position, SourcePosition = position }, at);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static PtBossEncounter.Area Next(PtBossEncounter encounter, DateTime now, Func<PtBossEncounter.Area, bool> belongs, int count)
    {
        var areas = encounter.Collect(now, 100).Where(belongs).OrderBy(a => a.From).ToArray();
        Check(areas.Length == count && areas.All(a => !a.PreviewOnly), "Future navigation areas were removed or made preview-only.");
        Check(areas.Count(a => !a.NavigationOnly) == 1 && !areas[0].NavigationOnly, "Later hits are still drawn over the next hit.");
        return areas[0];
    }

    private static void Leafmash()
    {
        var encounter = new Pt10Encounter(); Begin(encounter);
        for (var i = 0; i < 4; ++i)
            Effect(encounter, 10, 44058, (uint)(i + 1), Start.AddSeconds(i), encounter.Center + new Vector3(i * 4 - 6, 0, 0));
        encounter.StartCast(Cast(90, 44855, encounter.Center, Start.AddSeconds(20)));
        bool Belongs(PtBossEncounter.Area area) => area.Key.StartsWith("leaf/") || area.Action == 44057;
        var now = Start.AddSeconds(3);
        for (var i = 0; i < 4; ++i)
        {
            var first = Next(encounter, now, Belongs, 4 - i);
            Check(first.Shape is CircleTemplate { Radius: 15 }, "Leafmash physical R15 changed.");
            encounter.StartCast(Cast((ulong)(20 + i), 44057, first.Position, first.From.AddSeconds(.2)));
            var locked = Next(encounter, now, Belongs, 4 - i);
            Check(locked.Position == first.Position && locked.From == first.From, "Actual cast changed sequence timing or duplicated its prediction.");
            now = first.From.AddSeconds(.2);
            Effect(encounter, (ulong)(20 + i), 44057, (uint)(10 + i), now, first.Position);
            Effect(encounter, (ulong)(20 + i), 44057, (uint)(10 + i), now, first.Position);
            Check(encounter.Collect(now, 100).Single(a => a.Action == 44855).NavigationOnly == false, "Independent Hedge Mazing was hidden.");
        }
        Check(!encounter.Collect(now, 100).Any(Belongs), "The fourth jump left a stale preview.");
        encounter.Reset();
        Check(encounter.Collect(now, 100).Count == 0, "Reset retained jump output.");
    }

    private static void Bursts()
    {
        var encounter = new Pt20Encounter(); Begin(encounter);
        for (ushort i = 0; i < 4; ++i)
            encounter.Apply(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 2056, Param = (ushort)(0x395 + i), Duration = 30 }, Start.AddSeconds(i));
        encounter.StartCast(Cast(90, 43455, encounter.Center, Start.AddSeconds(14.5)));
        bool Belongs(PtBossEncounter.Area area) => area.Key.StartsWith("burst/") || area.Action is >= 43456 and <= 43459;
        Check(encounter.Collect(Start.AddSeconds(3), 100).Any(a => a.Key == "touchdown/landing" && a.NavigationOnly), "Knockback navigation constraint disappeared.");
        var now = Start.AddSeconds(3);
        for (var i = 0; i < 4; ++i)
        {
            var first = Next(encounter, now, Belongs, 4 - i);
            Check(first.Shape is CircleTemplate { Radius: 11 }, "Burst physical R11 changed.");
            encounter.StartCast(Cast((ulong)(20 + i), first.Action, first.Position, first.From.AddSeconds(.2)));
            Next(encounter, now, Belongs, 4 - i);
            now = first.From.AddSeconds(.2);
            Effect(encounter, (ulong)(20 + i), first.Action, (uint)(i + 1), now, Vector3.Zero);
            Effect(encounter, (ulong)(20 + i), first.Action, (uint)(i + 1), now, Vector3.Zero);
        }
        Check(!encounter.Collect(now, 100).Any(Belongs), "The fourth burst left a stale preview.");
    }

    private static void Swipes()
    {
        var encounter = new Pt70Encounter(); Begin(encounter);
        for (var i = 0; i < 8; ++i)
            encounter.StartCast(Cast((ulong)(20 + i), 43437, encounter.Center, Start.AddSeconds(1 + .6 * i)) with { Rotation = i * MathF.PI / 2 });
        encounter.StartCast(Cast(90, 43429, encounter.Center, Start.AddSeconds(30)));
        bool Belongs(PtBossEncounter.Area area) => area.Key.StartsWith("swipe/");
        var now = Start.AddSeconds(5);
        for (var i = 0; i < 8; ++i)
        {
            var first = Next(encounter, now, Belongs, 8 - i);
            Check(first.Shape is FanTemplate { Radius: 40 } fan && Math.Abs(fan.AngleRadians - MathF.PI / 2) < .001f, "Swipe physical cone changed.");
            Check(!encounter.Collect(now, 100).Single(a => a.Action == 43429).NavigationOnly, "Independent Mina Swing was hidden.");
            now = first.From.AddSeconds(.2);
            Effect(encounter, 1, 43434, (uint)(i + 1), now);
            Effect(encounter, 1, 43434, (uint)(i + 1), now);
        }
        Check(!encounter.Collect(now, 100).Any(Belongs), "The eighth swipe left a stale preview.");
    }

    private static void Lightning()
    {
        var encounter = new Pt80Encounter();
        var balls = Enumerable.Range(0, 9).Select(i => Actor((ulong)(20 + i), 0x485E, encounter.Center + new Vector3(i - 4, 0, i % 2 * 10 - 5))).OrderByDescending(a => a.Id).ToArray();
        Begin(encounter, balls);
        encounter.StartCast(Cast(90, 43473, encounter.Center, Start.AddSeconds(20)));
        bool Belongs(PtBossEncounter.Area area) => area.Key.StartsWith("lightning/") || area.Action is 43795 or 44060;
        var now = Start;
        for (var i = 0; i < 8; ++i)
        {
            var first = Next(encounter, now, Belongs, 9 - i);
            Check(first.Shape is RayRectangleTemplate, "The far-future R30 final explosion is already previewed.");
            Check(!encounter.Collect(now, 100).Single(a => a.Key == "waul").NavigationOnly, "Independent Profane Waul was hidden.");
            encounter.StartCast(Cast((ulong)(40 + i), 43795, balls[i + 1].Position, first.From.AddSeconds(.2), balls[i].Position));
            Next(encounter, now, Belongs, 9 - i);
            now = first.From.AddSeconds(.2);
            Effect(encounter, (ulong)(40 + i), 43795, (uint)(i + 1), now);
            Effect(encounter, (ulong)(40 + i), 43795, (uint)(i + 1), now);
        }
        var end = Next(encounter, now, Belongs, 1);
        Check(end.Shape is CircleTemplate { Radius: 30 }, "Final R30 explosion is not revealed after the last charge.");
        encounter.StartCast(Cast(60, 44060, end.Position, end.From.AddSeconds(.2)));
        Next(encounter, now, Belongs, 1);
        Effect(encounter, 60, 44060, 9, end.From.AddSeconds(.2));
        Check(!encounter.Collect(end.From.AddSeconds(.2), 100).Any(Belongs), "Final explosion left stale charge output.");
    }

    private static void Simultaneous()
    {
        var encounter = new Pt20Encounter(); Begin(encounter);
        for (var i = 0; i < 3; ++i)
            encounter.StartCast(Cast((ulong)(20 + i), (uint)(43456 + i), encounter.Center + new Vector3(i * 5, 0, 0), Start.AddSeconds(i < 2 ? 5 : 7)));
        var areas = encounter.Collect(Start, 100);
        Check(areas.Count == 3 && areas.Count(a => !a.NavigationOnly) == 2, "Simultaneous attacks must remain visible as a complete wave.");
        Next(encounter, Start.AddSeconds(5.2), _ => true, 1);
        encounter.Cancel(22);
        Check(encounter.Collect(Start.AddSeconds(5.2), 100).Count == 0, "Cancelled final cast left a preview.");
    }
}
