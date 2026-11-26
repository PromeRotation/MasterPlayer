// 护锁刃龙上位狩猎战（Arkveld Extreme）· 个人走位。原单文件脚本 ArkveldExtreme.cs 按段拆分。
/*
BSD 3-Clause License

Copyright (c) 2022-2024, Andrew Gilewsky

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Arkveld;

internal sealed class ArkveldSession
{
    public Arkveld[] Fights {get;private set;}=Enumerable.Range(0,8).Select(_=>new Arkveld(true)).ToArray();
    public int FocusRole;
    public Arkveld Fight=>Fights[Math.Clamp(FocusRole,0,7)];
    public readonly Dictionary<ulong,ReplayActorState> Actors=[];
    public ulong[] Roles {get;private set;}=new ulong[8];
    private readonly HashSet<ulong> spawned=[];
    // PR 把一次 AOE 按目标拆成多条；按 (来源,技能,序号) 收齐名单后再当一次命中喂进去。
    private sealed class PendingHit
    {
        public required (ulong Source,uint Action,uint Sequence) Key;
        public required double Time;
        public required ulong Main;
        public required Vector2 Point;
        public required float Angle;
        public required Vector2? SourcePoint;
        public readonly List<ulong> Targets=[];
    }
    private const double HitWindow=.05;
    private readonly List<PendingHit> pending=[];
    public bool Waiting=>pending.Count>0;
    private readonly HashSet<(ulong,uint,uint)> resolved=[];
    public bool InCombat,Ended;
    public static Vector2 Flat(Vector3 p)=>new(p.X,p.Z);
    public static double Seconds(DateTime t)=>(t-DateTime.UnixEpoch).TotalSeconds;
    public void Reset(){Fights=Enumerable.Range(0,8).Select(_=>new Arkveld(true)).ToArray();spawned.Clear();pending.Clear();resolved.Clear();Actors.Clear();InCombat=Ended=false;Array.Clear(Roles);}
    public void Bind(ulong[] roles){Roles=(ulong[])roles.Clone();foreach(var fight in Fights)roles.CopyTo(fight.Members,0);}
    private void Feed(Signal signal){foreach(var fight in Fights)fight.Feed(signal);}
    public void Synchronize(IEnumerable<ReplayActorState> snapshot,DateTime now)
    {
        double t=Seconds(now);FlushHits(t-HitWindow);var current=snapshot.ToArray();var ids=current.Select(a=>a.Id).ToHashSet();
        foreach(var id in spawned.Where(id=>!ids.Contains(id)).ToArray())
        {FlushHits();Feed(new(t,Input.Gone,0,id));spawned.Remove(id);Actors.Remove(id);}
        foreach(var a in current)
        {
            Actors[a.Id]=a;
            int role=Array.IndexOf(Roles,a.Id);
            if(spawned.Add(a.Id))Feed(new(t,Input.Spawn,a.Oid,a.Id,Point:Flat(a.Position),Angle:a.Rotation));
            foreach(var fight in Fights)
            {
                fight.ObservePosition(a.Id,Flat(a.Position),t);fight.SetJob(a.Id,a.ClassJob);fight.SetAlive(a.Id,!a.Dead);
                if(a.Oid==0x48e5){fight.ObserveBoss(a.Id,Flat(a.Position),a.HitboxRadius,a.Targetable&&!a.Dead);fight.ObserveTarget(a.TargetId);}
                if(role>=0)
                {
                    uint action=a.Cast?.ActionId??0;
                    fight.SetCasting((Role)role,action,a.Cast is {} c?Seconds(c.FinishAt):0,t);
                    if(a.InCombat && !Ended){InCombat=true;fight.BeginCombat();}
                }
            }
        }
        foreach(var fight in Fights)fight.Prune(t);
    }
    // 喂出第一条不晚于 until 的命中。读条、图标、对象消失之前全部喂出，保持原来的先后顺序。
    private void FlushHits(double until=double.PositiveInfinity)
    {
        for(int i=0;i<pending.Count;)
        {
            var hit=pending[i];
            if(hit.Time>until){i++;continue;}
            pending.RemoveAt(i);resolved.Add(hit.Key);
            Feed(new(hit.Time,Input.Hit,hit.Key.Action,hit.Key.Source,hit.Main,hit.Point,hit.Angle,
                Value:hit.Key.Sequence,Count:hit.Targets.Count,Targets:hit.Targets.ToArray(),SourcePoint:hit.SourcePoint));
        }
    }
    public void Handle(LogSystemEvent ev,DateTime now,float? rotation=null)
    {
        double t=Seconds(now);
        if(ev is LogSystemCastStartEvent or LogSystemTargetIconEvent or LogSystemObjectRemoveEvent)FlushHits();
        switch(ev)
        {
            case LogSystemCastStartEvent c when Arkveld.IsAction(c.ActionId) && c.CastTime is >=0 and <=120:
                Feed(new(t,Input.Cast,c.ActionId,c.SourceId,c.TargetId,Flat(c.EffectPosition),rotation??c.SourceRotation,c.CastTime,SourcePoint:c.SourcePosition is {} p?Flat(p):null));break;
            case LogSystemTargetIconEvent i:
                Feed(new(t,Input.Icon,i.MarkerId,i.TargetId is 0 or 0xe0000000?i.SourceId:i.TargetId));break;
            case LogSystemActionEffectEvent e when Arkveld.IsAction(e.ActionId):
                if(e.TargetPosition is {} tp)foreach(var fight in Fights)fight.Position(e.TargetId,Flat(tp));
                var key=(e.SourceId,e.ActionId,e.GlobalSequence);
                if(resolved.Contains(key))break;
                var hit=e.GlobalSequence==0?null:pending.Find(p=>p.Key==key);
                if(hit==null)
                {
                    var cast=Fight.LastCast(new(t,Input.Hit,e.ActionId,e.SourceId));
                    ulong main=cast.Target is 0 or 0xe0000000?e.TargetId:cast.Target;
                    var point=e.EffectPosition is {} ep?Flat(ep):default;
                    Vector2? source=e.SourcePosition is {} sp?Flat(sp):null;
                    if(e.GlobalSequence==0){Feed(new(t,Input.Hit,e.ActionId,e.SourceId,main,point,e.SourceRotation??0,SourcePoint:source));break;}
                    hit=new(){Key=key,Time=t,Main=main,Point=point,Angle=e.SourceRotation??0,SourcePoint=source};
                    pending.Add(hit);
                }
                if(e.TargetId is not (0 or 0xe0000000) && !hit.Targets.Contains(e.TargetId))hit.Targets.Add(e.TargetId);
                break;
            case LogSystemObjectRemoveEvent r:
                Feed(new(t,Input.Gone,r.DataId,r.SourceId));spawned.Remove(r.SourceId);Actors.Remove(r.SourceId);break;
            case LogSystemBattleStartedEvent:InCombat=true;Ended=false;foreach(var fight in Fights)fight.BeginCombat();break;
            case LogSystemBattleEndedEvent:Ended=true;InCombat=false;break;
        }
    }
    public WalkPlan? Plan(int role,DateTime now)
    {
        if(role is <0 or >7 || !Actors.TryGetValue(Roles[role],out var a) || a.Dead || !InCombat || Ended)return null;
        double t=Seconds(now);
        var fight=Fights[role];
        fight.SprintUntil=a.Statuses.Where(s=>s.Id==50 && s.ExpiresAt>now).Select(s=>Seconds(s.ExpiresAt)).DefaultIfEmpty(t).Max();
        return fight.Plan((Role)role,t,Flat(a.Position),fight.SprintUntilForExport(t)?Walking.SprintSpeed:Walking.Speed);
    }
}
