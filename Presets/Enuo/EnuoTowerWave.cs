using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoTowerWave
{
    private readonly Dictionary<ulong, EnuoCast> _towers = [];
    private readonly HashSet<ulong> _marked = [], _resolvedTowers = [], _resolvedCones = [];
    private int[] _assignment = [];
    private ulong[] _party = [];
    private DateTime _start, _until;
    private bool _closed;

    public void Reset()
    {
        _towers.Clear(); _marked.Clear(); _resolvedTowers.Clear(); _resolvedCones.Clear();
        _assignment = []; _party = []; _start = _until = default; _closed = false;
    }

    private void Begin(DateTime now)
    {
        Reset(); _start = now; _until = now.AddSeconds(10);
    }

    public void Cast(EnuoCast cast)
    {
        if (cast.Action is not (50013 or 50036)) return;
        // In all three ARR recordings, the four towers, cast bar and icons arrive within 0.2s.
        if (_start == default || cast.Start > _start.AddSeconds(2)) Begin(cast.Start);
        if (_closed) return;
        if (cast.Action == 50013) _towers.TryAdd(cast.Source, cast);
    }

    public void Icon(ulong target, DateTime now)
    {
        if (target is 0 or 0xE0000000) return;
        if (_start == default || now > _start.AddSeconds(12)) Begin(now);
        if (!_closed && _assignment.Length == 0) _marked.Add(target);
    }

    public void Effect(ulong source, uint action, DateTime now)
    {
        if (_start == default || _closed || now > _until) return;
        if (action == 50013 && _towers.ContainsKey(source)) _resolvedTowers.Add(source);
        if (action == 50038 && _resolvedCones.Add(source) && _resolvedCones.Count == 4) _closed = true;
    }

    // True prevents fallback movement to a previous add while this player's assignment is pending.
    public bool Apply(EnuoPlan plan, int role, ulong[] party, DateTime now)
    {
        if (_start == default || _closed || now > _until) return false;
        if (_assignment.Length == 0 && _towers.Count == 4)
        {
            var indices = _towers.Values.Select(c => Index(c.Position)).ToHashSet();
            var marked = _marked.Select(id => Array.IndexOf(party, id)).Where(i => i >= 0).ToHashSet();
            _assignment = EnuoStrategy.AssignTowers(indices, marked);
            if (_assignment.Length == 8) _party = [.. party];
        }
        if (_assignment.Length != 8 || !_party.SequenceEqual(party))
        {
            plan.Hints.Add("小怪：等待本轮四塔与四个点名，未完整时不猜分工。");
            return true;
        }
        var finish = _towers.Values.Max(c => c.Finish);
        for (var i = 0; i < 8; i++)
            if (i != role && _marked.Contains(party[i]))
                plan.Hazards.Add(new($"towerbait/{i}", new FanTemplate(60, MathF.PI / 4), EnuoStrategy.Center,
                    GeometryHelper.AngleTo(EnuoStrategy.Center, EnuoStrategy.Towers[_assignment[i]]), finish, _until, _start));
        var bait = _marked.Contains(party[role]);
        if (!bait)
        {
            var tower = _towers.Values.Single(c => Index(c.Position) == _assignment[role]);
            if (_resolvedTowers.Contains(tower.Source)) return false;
        }
        plan.Goal = new(bait ? "小怪：空位引导扇形" : "小怪：单人踩塔", EnuoStrategy.Towers[_assignment[role]],
            finish.AddSeconds(bait ? 1 : 0.3));
        return true;
    }

    private static int Index(Vector3 position) => Enumerable.Range(0, 8).MinBy(i => Vector3.DistanceSquared(position, EnuoStrategy.Towers[i]));
}
