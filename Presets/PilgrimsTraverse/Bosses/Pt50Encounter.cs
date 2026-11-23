using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt50Encounter() : PtBossEncounter(50, 0x4872)
{
    public override uint[] Actions { get; } = [43531, 43532, 43533, 43534, 43535, 43536, 43537, 45113, 43538];
    private bool quicksand;
    private int arenaStage;
    private readonly IGeometryShape smallArena = PtArenaData.Polygon(new(-300, 0, -300), 15, 72);
    private readonly GeometryTemplate sand = new PtShapeTemplate(new PtCompositeGeometry([PtArenaData.SandFloor], PtArenaData.SandIslands));
    private readonly GeometryTemplate land = new PtShapeTemplate(new PtCompositeGeometry(PtArenaData.SandIslands));
    private Cast? wind;
    private ulong chaseTarget;
    private ulong chaseSource;
    private Vector3 chaseOrigin;
    private DateTime chaseHit;
    private int chaseRemaining;
    private bool firstChase;
    public override IGeometryShape Boundary => arenaStage == 0 ? PtArenaData.SandFloor : smallArena;

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 43531: quicksand = true; break;
            case 43532: Hit(cast, new CircleTemplate(5), "Falling Rock"); break;
            case 43534:
                chaseSource = cast.Source;
                chaseOrigin = cast.Position;
                chaseHit = cast.Hit;
                chaseRemaining = 4;
                firstChase = true;
                break;
            case 43537:
                var key = "windraiser/rim";
                areas[key] = new(key, "Windraiser 缩场", new DonutTemplate(15, 20), new(-300, 0, -300), 0,
                    cast.Hit.AddSeconds(.8), DateTime.MaxValue, cast.Source);
                break;
            case 43538: wind = cast; break;
        }
    }
    protected override void OnMap(uint index, uint state, DateTime now)
    {
        if (index != 1) return;
        if (state == 0x00100004) arenaStage = 1;
        if (state == 0x00020001) arenaStage = 2;
    }
    protected override void OnIcon(ulong target, uint icon, DateTime now)
    { if (icon == 640) chaseTarget = target; }
    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (effect.ActionId is 43534 or 43535 or 43536 && chaseRemaining > 0 && effect.SourceId == chaseSource)
        {
            // TargetId is a damage recipient, not the packet's animation target.
            // The first hit is self-centered (its ground position is zero); later hits carry the actual ground center.
            chaseOrigin = effect.ActionId == 43534 ? effect.SourcePosition ?? chaseOrigin : effect.EffectPosition ?? chaseOrigin;
            chaseRemaining = Math.Max(0, chaseRemaining - 1);
            if (effect.ActionId == 43536) chaseRemaining = 0;
            chaseHit = now.AddSeconds(2.1);
            firstChase = false;
            if (chaseRemaining == 0) ClearChase();
        }
        if (effect.ActionId == 43538) wind = null;
    }
    protected override void SynchronizeActors(DateTime now)
    {
        if (chaseRemaining is > 0 and < 4 && (!actors.TryGetValue(chaseTarget, out var target) || target.Dead))
            ClearChase();
    }
    public override IEnumerable<(string Key, Vector3 Start, Vector3 End)> DrawingLines(DateTime now)
    {
        if (chaseRemaining > 0 && now <= chaseHit.AddSeconds(.15)
            && actors.TryGetValue(chaseTarget, out var target) && !target.Dead)
            yield return ("pit/line", chaseOrigin, target.Position);
    }
    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        if (arenaStage == 2)
            result.Add(new("wind/hole", "中央风眼", new CircleTemplate(3), new(-300, 0, -300), 0, now, DateTime.MaxValue, Obstacle: true));
        var windSoon = wind is { } w && now >= w.Hit.AddSeconds(-2) && now <= w.Hit.AddSeconds(.2);
        if (quicksand)
        {
            if (windSoon && !KnockbackImmune(player, wind!.Hit))
            {
                // The sand immunity is lost on land, so retain this condition even after gaining status 4625.
                result.Add(new("sand/enter", "进入流沙防击退", land, Center, 0, wind!.Hit.AddSeconds(-.2), wind.Hit.AddSeconds(.15), NavigationOnly: true));
            }
            if (Status(player, 567, now) is { } sinking)
                result.Add(new("sand/leave", "流沙沉没期限", sand, Center, 0, sinking.ExpiresAt.AddSeconds(-1), DateTime.MaxValue, NavigationOnly: true));
            if (windSoon)
                result.Add(new("sand/shelter", "进入流沙防击退", sand, Center, 0, now, wind!.Hit.AddSeconds(.15), Safe: true, PreviewOnly: true));
            else
                foreach (var (island, i) in PtArenaData.SandIslands.Select((s, i) => (s, i)))
                    result.Add(new($"sand/island/{i}", "流沙陆地", new PtShapeTemplate(island), Center, 0, now, DateTime.MaxValue, Safe: true, PreviewOnly: true));
        }
        if (wind is { } cast && now <= cast.Hit.AddSeconds(.2) && !quicksand && !WindImmune(player, cast.Hit))
            result.Add(new("wind/landing", "Biting Wind 击退落点", new PtKnockbackTemplate(Boundary, cast.Position, 20), Center, 0,
                cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.15), NavigationOnly: true));
        if (chaseRemaining > 0 && now <= chaseHit.AddSeconds(.15))
        {
            var position = chaseOrigin;
            if (!firstChase && actors.TryGetValue(chaseTarget, out var target))
            {
                var delta = target.Position - chaseOrigin;
                delta.Y = 0;
                if (delta.LengthSquared() > .001f) position += Vector3.Normalize(delta) * Math.Min(5, delta.Length());
            }
            var selfChase = !firstChase && chaseTarget == player;
            result.Add(new("pit/chase", $"破坑而出 {chaseRemaining}/4", new CircleTemplate(6), position, 0,
                chaseHit.AddSeconds(-.2), chaseHit.AddSeconds(.15), PreviewOnly: selfChase));
            if (selfChase)
                // Escaping only the current preview lets the next center follow the player's chosen endpoint.
                result.Add(new("pit/escape", "破坑而出追踪距离", new CircleTemplate(11), chaseOrigin, 0,
                    chaseHit.AddSeconds(-.2), chaseHit.AddSeconds(.15), NavigationOnly: true));
        }
    }
    public override IEnumerable<string> Hints(DateTime now, ulong player)
        => wind is { } cast && now <= cast.Hit && now >= cast.Hit.AddSeconds(-4) ? ["风击退前进入流沙，判定后立即返回陆地"] : [];
    public override IEnumerable<PtKnockbackTemplate> KnockbackSources(DateTime now, ulong player)
    {
        if (wind is { } cast && now <= cast.Hit.AddSeconds(.2) && !WindImmune(player, cast.Hit))
            yield return new(Boundary, cast.Position, 20);
    }
    private bool WindImmune(ulong player, DateTime hit) => KnockbackImmune(player, hit) || Status(player, 4625, hit) != null;
    public override void Cancel(ulong source)
    {
        base.Cancel(source);
        if (wind?.Source == source) wind = null;
        if (chaseSource == source) ClearChase();
    }
    private void ClearChase() { chaseTarget = 0; chaseSource = 0; chaseRemaining = 0; }
    protected override void ResetMechanics() { quicksand = false; arenaStage = 0; wind = null; ClearChase(); }
}
