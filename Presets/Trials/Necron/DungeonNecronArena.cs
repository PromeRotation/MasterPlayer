using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private byte necronActiveTeleporters;
    private bool necronSplit;
    private bool necronGrand;
    private int necronPrison = -1;
    private string necronArenaKey = "";
    private readonly List<(uint Index, Vector3 Position, DateTime At, int Key)> necronTowers = [];
    private int necronTowersAdded;

    internal IReadOnlyList<SolverTeleporter> NativeTeleporters
    {
        get
        {
            if (IsDoomtrain) return DoomTeleporters();
            if (!IsNecron || Active == null || necronPrison < 0 || (necronActiveTeleporters & (1 << necronPrison)) == 0
                || _actors.GetValueOrDefault(PlayerId) is not { } player) return [];
            var center = NecronArenas.PRISONS[necronPrison];
            Vector3 P(float x, float z) => new(center.X + x, player.Position.Y, center.Y + z);
            return [new(P(0, -7.4f), P(-6, -18), 2, false, 0),
                new(P(-2.5f, -20), P(10, -15), 2, false, 0),
                new(P(15, -11.5f), P(19, -2), 2, false, 0)];
        }
    }

    private void NecronUpdateArena()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) return;
        var previousPrison = necronPrison;
        necronPrison = -1;
        if (player.Position.Y < -100)
            for (var i = 0; i < NecronArenas.PRISONS.Length; ++i)
            {
                var p = NecronArenas.PRISONS[i];
                if (MathF.Abs(player.Position.X - p.X) <= 50 && MathF.Abs(player.Position.Z - p.Y) <= 50)
                { necronPrison = i; break; }
            }
        if (previousPrison >= 0 && necronPrison < 0) necronSplit = false;
        var mode = necronPrison >= 0 ? $"prison/{necronPrison}" : necronGrand ? "grand" : necronSplit ? "split" : "normal";
        var y = necronPrison >= 0 ? player.Position.Y : _actors.GetValueOrDefault(PrimaryId)?.Position.Y ?? player.Position.Y;
        var key = $"{mode}/{y:R}";
        if (key == necronArenaKey) return;
        necronArenaKey = key;
        if (necronPrison >= 0) Arena = NecronArenas.Prison(necronPrison, y);
        else if (necronGrand) Arena = NecronArenas.Circle(y);
        else if (necronSplit) Arena = NecronArenas.Split(y);
        else Arena = new RectangleGeometry(new(100, y, 100), 0, 36, 30);
    }

    private void NecronMap(uint index, uint state)
    {
        if (!IsNecron || Active == null) return;
        if (index <= 7)
        {
            if (state == 0x00020001) necronActiveTeleporters |= (byte)(1 << (int)index);
            if (state == 0x00080004) necronActiveTeleporters &= (byte)~(1 << (int)index);
        }
        if (index == 0x18)
        {
            if (state == 0x00020001) { necronSplit = true; necronMemento = null; }
            if (state == 0x00080004) necronSplit = false;
            necronArenaKey = "";
        }
        if (state == 0x00020001 && necronTowersAdded < 4 && index is >= 0x1A and <= 0x1D)
        {
            Vector3[] positions = [new(94, 0, 90), new(106, 0, 90), new(94, 0, 100), new(106, 0, 100)];
            var position = positions[index - 0x1A] with { Y = Arena?.Position.Y ?? 0 };
            necronTowers.Add((index, QuantizedDungeonPosition(position), _now.AddSeconds(15), ++necronKey));
            ++necronTowersAdded;
        }
        else if (state is 0x00080004 or 0x08000400)
        {
            var i = necronTowers.FindIndex(t => t.Index == index);
            if (i >= 0)
            {
                necronTowers.RemoveAt(i);
                if (necronTowers.Count == 0) necronTowersAdded = 0;
            }
        }
        NecronUpdateArena();
    }

    private void NecronDirector(uint command, uint param1)
    {
        if (!IsNecron || Active == null) return;
        if (command == 0x80000029)
        {
            var primary = PrimaryId;
            ResetFight();
            necronWipePrimary = primary;
            return;
        }
        if (command != 0x8000000D) return;
        if (param1 == 2) { necronGrand = true; necronShrink = null; }
        if (param1 == 1) { necronGrand = false; necronSplit = false; }
        necronArenaKey = "";
        NecronUpdateArena();
    }

    private IEnumerable<DungeonAmbientAoe> NecronPrisonAoes()
    {
        if (necronPrison < 0 || (necronActiveTeleporters & (1 << necronPrison)) == 0
            || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var p = NecronArenas.PRISONS[necronPrison];
        var offsets = new[] { new Vector2(0, -7.4f), new Vector2(-2.5f, -20), new Vector2(15, -11.5f), new Vector2(20, 0) };
        for (var i = 0; i < offsets.Length; ++i)
            yield return new($"necron/prison/{i}/display", "监牢传送入口及出口", new CircleTemplate(i == 3 ? 1.5f : 2),
                new(p.X + offsets[i].X, player.Position.Y, p.Y + offsets[i].Y), Navigate: false, Safe: true);
        yield return new("necron/prison/goal", "前往监牢出口", new CircleTemplate(1),
            new(p.X + 20, player.Position.Y, p.Y), Display: false, Safe: true, GoalPriority: 9);
    }

    private void ResetNecronArena()
    {
        necronActiveTeleporters = 0; necronSplit = necronGrand = false; necronPrison = -1; necronArenaKey = "";
        necronTowers.Clear(); necronTowersAdded = 0;
    }
}
