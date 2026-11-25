using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureMolter => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GOLDEN_MOLTER;
    private bool treasureConcealed;
    private DungeonActor? treasureMolterPrimary;
    private bool treasureMolterPrimaryRemoved;
    private HuntStatusInput? treasureConcealmentInput;
    // Gain does not reveal or conceal; the source only handles Lose.
    private HuntStatusInput TreasureConcealmentInput =>
        treasureConcealmentInput ??= new(3997, (_, _) => { }, _ => treasureConcealed = false);
    internal (uint Action, int Priority, ulong Target)? TreasureSprintIntent =>
        IsTreasureMolter && treasureConcealed ? (3u, 4000, PlayerId) : null;

    private void TreasureMolterStart(DungeonPendingCast cast, bool correction)
    {
        if (IsTreasureMolter && cast.Rule.Action is 38265 or 38266 && !correction)
            treasureConcealed = true;
    }

    private void TreasureMolterObserve(DungeonActor actor)
    {
        if (!IsTreasureMolter) return;
        TreasureConcealmentInput.Observe(new Dictionary<ulong, ReplayActorState>
        {
            [actor.Id] = TreasureStatusActor(actor),
        }, _now, actor.Id);
    }

    private void TreasureMolterSynchronize()
    {
        if (!IsTreasureMolter) return;
        if (!treasureMolterPrimaryRemoved)
        {
            if (_actors.TryGetValue(PrimaryId, out var primary)) treasureMolterPrimary = primary;
            else if (treasureMolterPrimary != null) treasureMolterPrimaryRemoved = true;
        }
        TreasureConcealmentInput.RemoveMissing(_actors.Keys.ToHashSet());
    }

    private void TreasureMolterRemoved(ulong source)
    {
        if (!IsTreasureMolter) return;
        if (source == PrimaryId)
        {
            if (!treasureMolterPrimaryRemoved && _actors.TryGetValue(source, out var primary))
                treasureMolterPrimary = primary;
            treasureMolterPrimaryRemoved = true;
        }
        TreasureConcealmentInput.Remove(source);
    }

    private void TreasureMolterStatus(ulong target, uint status, bool present, DateTime at, ushort param,
        DateTime? expiration, ulong source)
    {
        if (!IsTreasureMolter || status != 3997 || !_actors.TryGetValue(target, out var actor)) return;
        TreasureConcealmentInput.Status(TreasureStatusActor(actor),
            present ? new ReplayStatus(status, param, source, expiration ?? DateTime.MaxValue) : null, source, at);
    }

    private void TreasureMolterEState(ulong source, ushort state)
    {
        if (IsTreasureMolter && _actors.ContainsKey(source) && state == 4 && treasureCreatedCircles.Count != 0)
            treasureCreatedCircles.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureMolterAoes()
    {
        if (!IsTreasureMolter || !treasureConcealed || Arena == null
            || treasureMolterPrimary is not { } primary) yield break;
        yield return new("treasure/crypsis/display", "Crypsis", new CircleTemplate(9), primary.Position,
            Navigate: false, OutlineOnly: true, Color: new Vector4(0, 1, 0, 1));
        yield return new("treasure/crypsis/enter", "进入9码内显形",
            new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(primary.Position, 9)]), Arena.Position,
            Display: false);
    }

    private void ResetTreasureMolter()
    {
        treasureConcealed = false;
        treasureMolterPrimary = null;
        treasureMolterPrimaryRemoved = false;
        treasureConcealmentInput = null;
    }
}
