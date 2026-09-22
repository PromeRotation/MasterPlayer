using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace MasterPlayer.Mechanics.Dungeons;

public abstract class DungeonScript(uint territory) : MasterPlayerScript
{
    private DungeonEncounter? _encounter;
    private DungeonOutput? _output;
    private DungeonEventAdapter? _events;
    private readonly Dictionary<string, DateTime> _messages = [];
    private DateTime _nextScan;
    private bool _suspended;
    private long _replayVersion;

    public override IEnumerable<string> GetMechanisms() => ArrDungeonCatalog.Create(territory).Select(d => d.Name)
        .Concat(territory == 1036 ? ["水纹预览", "文字提示"] : ["文字提示"]);

    protected override void OnStart()
    {
        _encounter = new(territory, LogError);
        _output = new(SafeZone, Draw, MasterPlayerPlugin.Instance!.SafeZone.Manager);
        _events = new(_encounter, () => Scan(CurrentTime), e => CastRotation.TryGet(e, out var r) ? r : null, LogError);
        SuppressGenericDodge(ArrDungeonCatalog.ClaimedActions(_encounter.Definitions).ToArray());
        OnLogEvent(HandleEvent);
        OnBattleEnded(Suspend);
        OnPlayerDied(Suspend);
        OnBattleStarted(() => { _suspended = false; _nextScan = default; });
        OnPlayerRevived(() => { _suspended = false; _nextScan = default; });
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected override void OnUpdate(float deltaTime)
    {
        var now = CurrentTime;
        if (now < _nextScan && (ReplayWorld == null || ReplayWorld.Version == _replayVersion)) return;
        Scan(now);
        Render(now);
    }

    protected override void OnStop()
    {
        _output?.Clear();
        _encounter?.ResetFight();
        _output = null;
        _encounter = null;
        _events = null;
        _messages.Clear();
    }

    private void Suspend()
    {
        _suspended = true;
        _encounter?.ResetFight();
        _output?.Clear();
    }

    private unsafe void Scan(DateTime now)
    {
        _nextScan = now.AddSeconds(0.1);
        if (_encounter == null) return;
        if (ReplayWorld is { } world)
        {
            _replayVersion = world.Version;
            var party = world.Party.ToDictionary(p => p.EntityId);
            var replayActors = world.Actors.Values.Select(a =>
            {
                party.TryGetValue(a.Id, out var member);
                var cast = a.Cast is { } c ? new DungeonCastObservation(c.ActionId, c.Location, c.Rotation, c.StartedAt, c.FinishAt, c.TargetId) : null;
                return new DungeonActor(a.Id, a.Oid, a.Position, a.Dead, a.Targetable, a.InCombat, cast, a.TargetId, a.Name, member?.Slot ?? -1, member?.Role ?? "");
            });
            _encounter.Synchronize(replayActors, !_suspended && world.LocalPlayer is { Dead: false } p ? p.Position : null, now, IsMechanismEnabled);
            foreach (var a in world.Actors.Values)
                if (a.EventAnimation is { } state) _encounter.Animation(a.Id, state);
            return;
        }
        var actors = new List<DungeonActor>();
        var liveParty = ReplayWorldCapture.CaptureParty().ToDictionary(p => p.EntityId);
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == 0) continue;
            var id = obj.EntityId is not (0 or 0xE0000000) ? obj.EntityId : obj.GameObjectId;
            if (id is 0 or 0xE0000000) continue;
            var combat = false;
            DungeonCastObservation? cast = null;
            if (obj is IBattleChara battle)
            {
                combat = (battle.StatusFlags & StatusFlags.InCombat) != 0;
                if (battle.IsCasting && battle.CastActionId != 0)
                {
                    var native = (NativeBattleChara*)battle.Address;
                    var location = native->CastInfo.TargetLocation;
                    var remaining = Math.Max(0, battle.TotalCastTime - battle.CurrentCastTime);
                    cast = new(battle.CastActionId, new(location.X, location.Y, location.Z),
                        float.IsFinite(native->CastRotation) ? native->CastRotation : null,
                        now.AddSeconds(-battle.CurrentCastTime), now.AddSeconds(remaining + 0.3), battle.CastTargetObjectId);
                }
            }
            liveParty.TryGetValue(id, out var member);
            actors.Add(new(id, obj.BaseId, obj.Position, obj.IsDead, obj.IsTargetable, combat, cast,
                obj.TargetObjectId, obj.Name.TextValue, member?.Slot ?? -1, member?.Role ?? ""));
        }
        var player = Svc.Objects.LocalPlayer;
        _encounter.Synchronize(actors, !_suspended && player is { IsDead: false } ? player.Position : null, now, IsMechanismEnabled);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        if (_encounter == null || _suspended) return;
        var now = CurrentTime;
        _events?.Handle(ev, now);
        Render(now);
    }

    private void Render(DateTime now)
    {
        if (_encounter == null) return;
        _output?.Apply(_encounter, now, IsMechanismEnabled("水纹预览"));
        if (!IsMechanismEnabled("文字提示")) return;
        var position = ReplayWorld is { } world ? world.LocalPlayer?.Position : Svc.Objects.LocalPlayer?.Position;
        foreach (var hint in _encounter.Hints(position))
        {
            if (_messages.TryGetValue(hint, out var last) && now - last < TimeSpan.FromSeconds(10)) continue;
            _messages[hint] = now;
            Log(hint);
        }
    }
}
