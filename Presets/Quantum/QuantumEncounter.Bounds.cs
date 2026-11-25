using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class QuantumEncounter
{
    private QuantumAoe? endAoe;
    private readonly List<int> pillars = [];
    private readonly List<(Vector3 Position, DateTime Activation, ulong Forbidden)> towers = [];
    private readonly List<(long Generation, QuantumActor Target, DateTime Activation)> stacks = [];
    private ulong forbidden = ulong.MaxValue;
    internal IReadOnlyList<int> Pillars => pillars;
    internal IReadOnlyList<QuantumAoe> Ordinary => ordinary;
    internal IReadOnlyList<QuantumAoe> Small => small;
    internal QuantumAoe? EndAoe => endAoe;
    internal IReadOnlyList<(Vector3 Position, DateTime Activation, ulong Forbidden)> Towers => towers;
    internal IReadOnlyList<(long Generation, QuantumActor Target, DateTime Activation)> Stacks => stacks;
    internal IReadOnlyList<QuantumActor> Party => party.OrderBy(p => p.Slot)
        .Select(p => Actors.GetValueOrDefault(p.EntityId)).OfType<QuantumActor>().ToArray();
    internal int CurrentSlot => Actors.GetValueOrDefault(PlayerId) is { } player ? Slot(player) : -1;

    internal void Map(uint index, uint state, DateTime time)
    {
        now = time;
        if (!Constructed) return;
        if (index == 0x1B && state is 0x00020001 or 0x00080004)
            boundsTowers = state == 0x00020001;
        if (endActive && index <= 0x17)
        {
            if (state == 0x00020001)
            {
                pillars.Add((int)index);
                Arena = QuantumGeometry.Arena(pillars);
            }
            else if (index is 0 or 0x0C && state == 0x00080004)
            {
                pillars.Clear();
                Arena = QuantumGeometry.RestoredFloor;
            }
        }
        if (!towerActive || index != 0x1B) return;
        if (state == 0x00080004) ++towerCasts;
        else if (state == 0x00020001)
        {
            foreach (var actor in Party)
                if (!actor.Statuses.Any(s => s?.Id == 4560)) SetBit(ref forbidden, Slot(actor), false);
            const float a45 = 45f * (MathF.PI / 180f);
            const float a90 = 90f * (MathF.PI / 180f);
            for (var i = 0; i < 4; ++i)
            {
                var (sin, cos) = Math.SinCos(i * a90 + a45);
                towers.Add((Arena.Position + 11f * new Vector3((float)sin, 0, (float)cos),
                    now.AddSeconds(14.2d), forbidden));
            }
        }
    }

    private void TowerStatus(QuantumActor actor, uint id, bool gained)
    {
        if (!towerActive || id != 4560) return;
        SetBit(ref forbidden, Slot(actor), !gained);
        for (var i = 0; i < towers.Count; ++i)
            towers[i] = (towers[i].Position, towers[i].Activation, forbidden);
    }
}
