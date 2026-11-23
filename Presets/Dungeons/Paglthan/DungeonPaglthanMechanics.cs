using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonActor> paglthanLightning = [];
    private readonly Dictionary<ulong, bool> paglthanSeenLightning = [];
    private readonly Dictionary<ulong, DateTime> paglthanStatusEvents = [];
    private readonly HashSet<ulong> paglthanSeenShocks = [];
    private readonly List<DungeonAmbientAoe> paglthanShocks = [];
    private readonly List<DungeonAmbientAoe> paglthanBlasters = [];
    private readonly List<DungeonAmbientAoe> paglthanCannons = [];
    private readonly HashSet<ulong> paglthanMissiles = [];
    private readonly HashSet<ulong> paglthanSeenMissiles = [];
    private readonly Dictionary<ulong, DateTime> paglthanBirths = [];
    private readonly Dictionary<ulong, (DungeonActor Target, DateTime Activation)> paglthanBaits = [];
    private readonly List<DungeonAmbientAoe> paglthanCrosses = [];
    private readonly List<DungeonPendingCast> paglthanBusters = [];
    private DungeonPendingCast? paglthanAkhMorn;
    private int paglthanAkhHits;
    private int paglthanShockHits;
    private int paglthanCrossHits;
    private bool paglthanFirstShock = true;
    private bool paglthanCore;
    private bool paglthanPlatform;
    private DateTime? paglthanLightningActivation;
    private long paglthanSerial;
    private bool IsPaglthanFortress => territory == PaglthanCatalog.TERRITORY && Active?.BossOid == 0x32FE;
    private bool PaglthanCanStart(DungeonActor a) => a.Oid == 0x32FE
        ? _actors.Values.Any(e => e.Oid == 0x31AF && e.InCombat && !e.Dead) : a.InCombat;

    private void SynchronizePaglthan()
    {
        if (territory != PaglthanCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x3169)
        {
            foreach (var actor in _actors.Values)
            {
                if (!paglthanStatusEvents.TryGetValue(actor.Id, out var changed) || _now >= changed.AddSeconds(.5))
                {
                    var status = HasStatus(actor, 2574);
                    if (paglthanSeenLightning.GetValueOrDefault(actor.Id) != status) PaglthanStatus(actor.Id, status, _now);
                }
                if (paglthanLightning.ContainsKey(actor.Id)) paglthanLightning[actor.Id] = actor;
                if (actor.Oid is 0x31A2 or 0x31A3 && paglthanSeenShocks.Add(actor.Id))
                    paglthanShocks.Add(new($"paglthan/shock/{actor.Id}", "雷球连续放电", new CircleTemplate(actor.Oid == 0x31A2 ? 5 : 10),
                        QuantizedDungeonPosition(actor.Position), Activation: paglthanBirths.GetValueOrDefault(actor.Id, _now).AddSeconds(3.7)));
            }
        }
        if (IsPaglthanFortress)
        {
            if (_actors.TryGetValue(PlayerId, out var player))
            {
                var core = PaglthanCatalog.FortressArena(Arena.Position.Y, true).Contains(player.Position, 0);
                if (core != paglthanCore)
                {
                    paglthanCore = core; Arena = PaglthanCatalog.FortressArena(Arena.Position.Y, core);
                }
            }
            foreach (var a in _actors.Values.Where(a => a.Oid == 0x31B2))
                if (paglthanSeenMissiles.Add(a.Id)) paglthanMissiles.Add(a.Id);
            paglthanMissiles.RemoveWhere(id => !_actors.ContainsKey(id));
        }
        foreach (var (id, bait) in paglthanBaits.ToArray())
            if (_actors.TryGetValue(id, out var target)) paglthanBaits[id] = (target, bait.Activation);
    }

    private void PaglthanStatus(ulong target, bool present, DateTime time)
    {
        if (Active?.BossOid != 0x3169 || !_actors.TryGetValue(target, out var actor)) return;
        paglthanStatusEvents[target] = time; paglthanSeenLightning[target] = present;
        if (present)
        {
            paglthanLightningActivation ??= time.AddSeconds(10.8);
            paglthanLightning[target] = actor;
        }
        else paglthanLightning.Remove(target);
    }

    private void PaglthanMap(uint index, uint state, DateTime time)
    {
        if (!IsPaglthanFortress || Arena == null) return;
        if (state == 0x00200010 && index is >= 0x08 and <= 0x0A)
            paglthanCannons.Add(new($"paglthan/cannon/{++paglthanSerial}", "魔导稳定炮", RayRectangleTemplate.Symmetric(10, 60),
                QuantizedDungeonPosition(new(-185 + 10 * (index - 0x08), Arena.Position.Y, 28.3f)),
                -.003f * MathF.PI / 180, Activation: time.AddSeconds(12.1)));
        if (index == 0x0D)
        {
            if (state == 0x00020001) { paglthanCannons.Clear(); paglthanPlatform = true; }
            else if (state == 0x00080004) paglthanPlatform = false;
        }
    }

    private void PaglthanCast(DungeonPendingCast cast)
    {
        if (territory != PaglthanCatalog.TERRITORY) return;
        if (cast.Rule.Action == 24773)
        {
            // A packet may correct an existing cast; replace that pair only.
            paglthanBlasters.RemoveAll(a => a.Id.StartsWith($"paglthan/blast/{cast.Source}/{cast.StartedAt.Ticks}/"));
            paglthanBlasters.Add(new($"paglthan/blast/{cast.Source}/{cast.StartedAt.Ticks}/wide", "广域吐息", cast.Rule.Shape!,
                _actors[cast.Source].Position, cast.Rotation, Navigate: paglthanBlasters.Count == 0, Activation: cast.FinishAt));
            paglthanBlasters.Add(new($"paglthan/blast/{cast.Source}/{cast.StartedAt.Ticks}/tail", "后续尾击", new FanTemplate(25, MathF.PI / 3),
                _actors[cast.Source].Position, cast.Rotation + MathF.PI, Navigate: false, Activation: cast.FinishAt.AddSeconds(2.6)));
        }
        if (cast.Rule.Action == 23381) { paglthanAkhMorn = cast; paglthanAkhHits = 0; }
        if (cast.Rule.Action == 23384)
        {
            paglthanBusters.RemoveAll(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            paglthanBusters.Add(cast with { TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId });
        }
    }

    private void PaglthanIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x316A && icon == 260 && _actors.TryGetValue(target, out var actor))
            paglthanBaits[target] = (actor, time.AddSeconds(5.6));
    }

    private void PaglthanEffect(ulong source, uint action, ulong target)
    {
        if (territory != PaglthanCatalog.TERRITORY) return;
        if (action == 23628) { paglthanLightning.Clear(); paglthanLightningActivation = null; }
        if (action is 23634 or 23635 && ++paglthanShockHits == (paglthanFirstShock ? 72 : 30))
        {
            paglthanShocks.Clear(); paglthanShockHits = 0; paglthanFirstShock = false;
        }
        if (action is 24773 or 23631 && paglthanBlasters.Count > 0)
        {
            paglthanBlasters.RemoveAt(0);
            if (paglthanBlasters.Count > 0) paglthanBlasters[0] = paglthanBlasters[0] with { Navigate = true };
        }
        if (action == 23700) paglthanCannons.Clear();
        if (action == 23704) paglthanMissiles.Remove(source);
        if (action is 23381 or 23382 && ++paglthanAkhHits == 4)
        {
            paglthanAkhMorn = null; paglthanAkhHits = 0;
        }
        if (action == 23377)
        {
            paglthanBaits.Clear();
            if (_actors.TryGetValue(source, out var caster))
                paglthanCrosses.Add(new($"paglthan/cross/{++paglthanSerial}", "天光轮回连续十字", new DungeonCrossTemplate(15, 3),
                    QuantizedDungeonPosition(caster.Position), -.003f * MathF.PI / 180));
        }
        if (action == 23376 && ++paglthanCrossHits == paglthanCrosses.Count * 20 && paglthanCrosses.Count > 0)
        {
            paglthanCrosses.Clear(); paglthanCrossHits = 0;
        }
        if (action == 23384)
        {
            var index = paglthanBusters.FindIndex(c => c.TargetId == target);
            if (index >= 0) paglthanBusters.RemoveAt(index);
        }
    }

    private IEnumerable<DungeonDamagePrediction> PaglthanDamage()
    {
        foreach (var cast in paglthanBusters)
            if (_actors.TryGetValue(cast.TargetId, out var t) && !t.Dead && t.PartySlot >= 0)
                yield return new(t.Id, t.PartySlot, t.PartyRole, t.Name, cast.FinishAt, "Tankbuster");
        if (paglthanAkhMorn is { } stack)
            foreach (var t in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(t.Id, t.PartySlot, t.PartyRole, t.Name, stack.FinishAt, "Raidwide");
    }

    private void ResetPaglthan()
    {
        paglthanLightning.Clear(); paglthanSeenLightning.Clear(); paglthanStatusEvents.Clear(); paglthanSeenShocks.Clear(); paglthanShocks.Clear();
        paglthanBlasters.Clear(); paglthanCannons.Clear(); paglthanMissiles.Clear(); paglthanSeenMissiles.Clear(); paglthanBirths.Clear();
        paglthanBaits.Clear(); paglthanCrosses.Clear(); paglthanBusters.Clear(); paglthanAkhMorn = null; paglthanAkhHits = 0;
        paglthanShockHits = 0; paglthanCrossHits = 0; paglthanFirstShock = true; paglthanCore = false; paglthanPlatform = false;
        paglthanLightningActivation = null; paglthanSerial = 0;
    }
}
