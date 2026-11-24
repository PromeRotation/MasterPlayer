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

namespace MasterPlayer.Mechanics.Arkveld
{
using System.Reflection;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;

public enum PersonalRole { 未指定,MT,ST,H1,H2,D1,D2,D3,D4 }
internal sealed record PartyEntry(string Key,ulong Actor,string Name,uint Job,string World="")
{
    public string Label=>$"{JobNames.Chinese(Job)} {Name}"+(World.Length==0?"":" / "+World);
}
// This is a USER confirmation, not an entitlement gate. No inferred job order
// becomes authoritative until all eight distinct members have been confirmed.
internal sealed class RoleLock
{
    public string[] Seats {get;set;}=new string[8];
    public bool Locked {get;set;}
    public string Error {get;private set;}="请分配并确认八人职能";
    public string Encode()=>"v1|"+(Locked?"1":"0")+"|"+string.Join(",",Seats);
    public static RoleLock Decode(string text)
    {
        var fields=text.Split('|');if(fields.Length!=3 || fields[0]!="v1")return new();
        var seats=fields[2].Split(',');return seats.Length==8?new(){Seats=seats,Locked=fields[1]=="1"}:new();
    }
    public bool Confirm(IReadOnlyList<PartyEntry> party,ulong self,int own,bool replay)
    {
        if(Seats.Length!=8 || party.Count!=8 || party.Select(p=>p.Key).Distinct().Count()!=8 ||
           Seats.Any(string.IsNullOrEmpty) || Seats.Distinct().Count()!=8 || Seats.Any(k=>party.All(p=>p.Key!=k)))
        {Locked=false;Error="需要完整八人，每个人只能占一个职能";return false;}
        if(!replay && (own is <0 or >7 || party.Single(p=>p.Key==Seats[own]).Actor!=self))
        {Locked=false;Error="本人职能与名单不一致，请把自己分配到所选职能";return false;}
        Locked=true;Error="已确认并锁定";return true;
    }
    public bool Resolve(IReadOnlyList<PartyEntry> party,ulong self,int own,bool replay,out ulong[] roles)
    {
        roles=new ulong[8];
        if(!Locked)return false;
        if(party.Count!=8){Error="名单暂未读齐，保留锁定，等待八人恢复";return false;}
        if(Seats.Length!=8 || Seats.Distinct().Count()!=8 || Seats.Any(k=>party.All(p=>p.Key!=k)))
        {Locked=false;Error="队伍已换人，请重新分配并确认";return false;}
        for(int i=0;i<8;i++)roles[i]=party.Single(p=>p.Key==Seats[i]).Actor;
        if(roles.Any(id=>id is 0 or 0xe0000000) || roles.Distinct().Count()!=8)
        {Error="等待八人对象可用，不按职业猜职能";return false;}
        if(!replay && (own is <0 or >7 || roles[own]!=self))
        {Locked=false;Error="本人职能已改变，请重新确认名单";return false;}
        Error="已确认并锁定";return true;
    }
}

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

// Keep the expiry read private in the original; this tiny accessor is local only.
internal sealed partial class Arkveld
{
    internal bool SprintUntilForExport(double time)=>SprintUntil>time;
}

}

// MP 只从 MasterPlayer.Scripts 及其子命名空间收内置脚本，入口类必须放在这里。
namespace MasterPlayer.Scripts.Trials
{
using System.Reflection;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Mechanics.Arkveld;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using ReplayWorldCapture=MasterPlayer.Mechanics.Arkveld.ReplayWorldCapture;

[MechanicScript("护锁刃龙上位狩猎战 · 个人走位",[1306u],"masterplayer.trial.arkveld-ex","1.1.0","Ahxq",
    "仅移动本人；职能默认读 PR 小队职业，设置了本人职能就按设置；实战/回放独立；末段地火自行步行处理")]
public sealed class ArkveldExtreme : MasterPlayerScript
{
    [UserSetting("本人职能（未指定 = 读 PR 小队职业）")]
    public PersonalRole OwnRole {get;set;}=PersonalRole.未指定;
    [UserSetting("显示职能确认与路线窗口")]
    public bool ShowWindow {get;set;}=true;
    [UserSetting("绘制八职能路线（仅本人自动移动）")]
    public bool ShowAllRoles {get;set;}=true;
    [UserSetting("机制需要时自动冲刺")]
    public bool AutomaticSprint {get;set;}=true;
    private readonly ReplayWorldCapture capture=new();
    private readonly Dictionary<string,RoleLock> banks=[];
    internal ArkveldSession Session=new();
    internal List<PartyEntry> Party=[];
    internal RoleLock Bank=new();
    // 未手动锁定名单时生效的自动名单：PR 小队职业，本人职能有设置就覆盖本人位置。
    internal RoleLock Auto=new();
    internal RoleLock Active=>Bank.Locked?Bank:Auto;
    private string bankKey="",notice="",identity="";
    private ArkveldMpApi? api;
    private object? ui;private EventInfo? drawEvent;
    private DateTime nextScan,last,nextSprint;
    private readonly HashSet<string> drawing=[];
    private WalkPlan?[] plans=new WalkPlan?[8];
    private bool replay,lateStart,haveBound;
    private ulong perspective;
    private static bool MovementEnabled()
    {
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var settings=MasterPlayerSettings.Instance;var t=settings.GetType();
        foreach(var name in new[]{"EnableSmartMovement","EnableAutoDodge"})
        {if(t.GetField(name,flags) is {} f)return f.GetValue(settings) is true;if(t.GetProperty(name,flags) is {} p)return p.GetValue(settings) is true;}
        return false;
    }
    private bool Replaying=>ReplayWorld!=null || api?.ReplayActive==true || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback];
    private ulong Perspective
    {
        get
        {
            // New MP follows the selected ARR player. Older hosts still expose ReplayWorld.
            var property=typeof(MasterPlayerScript).GetProperty("CurrentPlayerId");
            return property?.GetValue(this) is ulong id?id:ReplayWorld?.LocalPlayerId??ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
        }
    }
    private int Own=>(int)Settings.Get<PersonalRole>(nameof(OwnRole),PersonalRole.未指定)-1;
    // 本人职能：设置了就用设置，没设置就看本人在这份名单里的位置（PR 小队职业）。
    private int OwnFor(RoleLock bank)=>Own>=0?Own:Party.FirstOrDefault(p=>p.Actor==perspective) is {} me?Array.IndexOf(bank.Seats,me.Key):-1;
    public override IEnumerable<string> GetMechanisms()=>["自动处理机制","文字提示"];
    protected override void OnStart()
    {
        api=new(SafeZone,Draw);
        ui=typeof(Dalamud.Plugin.IDalamudPluginInterface).GetProperty("UiBuilder")!.GetValue(Svc.PluginInterface);
        drawEvent=ui!.GetType().GetEvent("Draw")??typeof(Dalamud.Plugin.IDalamudPluginInterface).GetProperty("UiBuilder")!.PropertyType.GetEvent("Draw");
        drawEvent!.AddEventHandler(ui,(Action)Render);
        SuppressGenericDodge(Enumerable.Range(43887,74).Concat(Enumerable.Range(45077,35)).Select(i=>(uint)i).Concat(new uint[]{45125,45126,45202,44805,44806,44809,44810,44811,44812,44878,44792,44793}).ToArray());
        OnLogEvent(Handle);
        OnBattleStarted(()=>{Session.Reset();haveBound=false;lateStart=false;nextScan=default;});
        OnBattleEnded(()=>{Session.Ended=true;Session.InCombat=false;Release();});
        OnPlayerDied(()=>{if(!Replaying)Release();});
        Scan();lateStart=!replay && Party.Any(p=>Session.Actors.GetValueOrDefault(p.Actor)?.InCombat==true);
    }
    protected override void OnUpdate(float deltaTime)
    {
        if(CurrentTime<last || replay!=Replaying)
        {Release();Session.Reset();bankKey="";haveBound=false;lateStart=false;nextScan=default;}
        last=CurrentTime;
        if(!MovementEnabled() || Replaying)Release();
        if(CurrentTime>=nextScan)Refresh();
    }
    protected override void OnMovementDisabled()=>Release();
    private List<PartyEntry> ReadParty(ReplayWorldFrame frame)
    {
        if(replay)return frame.Party.Select(p=>frame.Actors.FirstOrDefault(a=>a.Id==p.EntityId)).OfType<ReplayActorState>()
            .DistinctBy(a=>a.Id).Select(a=>new PartyEntry("replay:"+a.Id,a.Id,a.Name,a.ClassJob)).ToList();
        var result=new List<PartyEntry>();
        foreach(var p in Svc.Party)
        {
            var actor=p.GameObject;ulong id=p.EntityId;
            if(id is 0 or 0xe0000000)id=ReplayWorldCapture.Id(actor);
            var a=frame.Actors.FirstOrDefault(a=>a.Id==id);
            if(p.ContentId==0)continue;
            result.Add(new(p.ContentId.ToString(),id,p.Name.TextValue,a?.ClassJob??0,p.World.Value.Name.ToString()));
        }
        if(result.Count==0 && Svc.Objects.LocalPlayer is {} self)
            result.Add(new(Svc.PlayerState.ContentId.ToString(),ReplayWorldCapture.Id(self),self.Name.TextValue,self.ClassJob.RowId,self.HomeWorld.Value.Name.ToString()));
        return result;
    }
    private void Scan()
    {
        replay=Replaying;perspective=Perspective;
        ReplayWorldFrame frame=ReplayWorld is {} w?new(){Party=w.Party.ToArray(),Actors=w.Actors.Values.ToArray(),LocalPlayerId=w.LocalPlayerId}:
            capture.Capture(CurrentTime,perspective);
        Party=ReadParty(frame);
        string key=replay?"ReplayRoles:"+string.Join("-",Party.Select(p=>p.Key).Order()):"LiveRoles:"+Svc.PlayerState.ContentId;
        if(bankKey!=key)
        {
            bankKey=key;
            if(!banks.TryGetValue(key,out var bank))
            {bank=RoleLock.Decode(Settings.GetString(key,""));banks[key]=bank;}
            Bank=banks[key];
        }
        Auto=new(){Seats=PrSeats()};Auto.Confirm(Party,perspective,OwnFor(Auto),replay);
        if(Active.Resolve(Party,perspective,OwnFor(Active),replay,out var roles))
        {
            if(haveBound && !Session.Roles.SequenceEqual(roles) && Session.InCombat && !replay)
            {lateStart=true;notice="战斗中角色对象发生变化，本轮暂停移动，下一次开战重新核对";}
            Session.Bind(roles);haveBound=true;
        }
        Session.Synchronize(frame.Actors,CurrentTime);
        if(!Session.Ended && Party.Any(p=>Session.Actors.GetValueOrDefault(p.Actor)?.InCombat==true))Session.InCombat=true;
        identity=string.Join("|",Party.Select(p=>p.Key).Order());
    }
    private void Handle(LogSystemEvent ev)
    {
        capture.Observe(ev);
        if(ev is LogSystemDirectorUpdateEvent {Command:0x40000005 or 0x40000006})
        {Release();Session.Reset();haveBound=false;lateStart=false;nextScan=default;return;}
        if(ev is LogSystemCastStartEvent or LogSystemTargetIconEvent)Scan();
        float? rotation=ev is LogSystemCastStartEvent c && CastRotation.TryGet(c,out var r)?r:null;
        Session.Handle(ev,CurrentTime,rotation);nextScan=default;
    }
    private void Refresh()
    {
        Scan();nextScan=CurrentTime.AddSeconds(Session.Waiting?.05:.10);
        bool ready=Active.Resolve(Party,perspective,OwnFor(Active),replay,out _);
        plans=new WalkPlan?[8];
        if(ready)for(int i=0;i<8;i++)if(Settings.GetBool(nameof(ShowAllRoles),true) || Session.Roles[i]==perspective)plans[i]=Session.Plan(i,CurrentTime);
        DrawTargets();
        int role=Array.IndexOf(Session.Roles,perspective);var me=Session.Actors.GetValueOrDefault(perspective);
        Session.FocusRole=Math.Clamp(role,0,7);
        bool loaded=!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] && !Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
        bool openingUnlocked=replay || PromeRotation.Core.GameData.GetCountdown()<=0;
        if(!CanMove(replay,MovementEnabled(),IsMechanismEnabled("自动处理机制"),ready,!lateStart&&loaded&&openingUnlocked&&Session.InCombat&&!Session.Ended&&me is {Dead:false,Targetable:true},role) || plans.ElementAtOrDefault(role) is not {} plan)
        {Release();return;}
        var here=ArkveldSession.Flat(me!.Position);var next=plan.Next(here,ArkveldSession.Seconds(CurrentTime));
        bool hold=Vector2.DistanceSquared(next,here)<.01f || plan.OptionalStride&&me.Cast!=null;
        api!.RequestAutomaticMovement(this,true,hold);api.RequestDirectTraversal(this,hold?null:new Vector3(next.X,me.Position.Y,next.Y),CurrentTime);
        if(Settings.GetBool(nameof(AutomaticSprint),true) && CurrentTime>=nextSprint && me.Cast==null && Session.Fight.WantsSprint(ArkveldSession.Seconds(CurrentTime)))Sprint();
    }
    internal static bool CanMove(bool replay,bool enabled,bool mechanism,bool roles,bool alive,int role)=>!replay&&enabled&&mechanism&&roles&&alive&&role is >=0 and <8;
    private void Sprint()
    {
        var me=Svc.Objects.LocalPlayer;
        if(me==null || !PromeRotation.Helpers.ActionHelper.CanCast(3,me) || PromeRotation.Helpers.ActionHelper.GetAnimationLock()>0)return;
        nextSprint=CurrentTime.AddSeconds(1);
        PromeRotation.Updaters.ActionUpdaterRouter.UseAction(new PromeRotation.Data.PAction(3,PromeRotation.Data.ActionType.OffGcd,PromeRotation.Data.ActionTargetType.Self){NetworkTid=me.EntityId});
    }
    private void Save()=>Settings.Set(bankKey,Bank.Encode());
    // PR 小队职业 → 八职能；实战里本人职能有设置时把本人换到设置的位置。
    private string[] PrSeats()
    {
        var seats=new string[8];
        var map=ReplayWorld?.Party??ReplayWorldCapture.CaptureParty();
        foreach(var member in map)
            if(Enum.TryParse<Role>(member.Role,out var role) && Party.FirstOrDefault(p=>p.Actor==member.EntityId) is {} p)seats[(int)role]=p.Key;
        if(!replay && Own is >=0 and <8 && Party.FirstOrDefault(p=>p.Actor==perspective) is {} own)
        {int prior=Array.IndexOf(seats,own.Key);if(prior>=0)(seats[prior],seats[Own])=(seats[Own],seats[prior]);else seats[Own]=own.Key;}
        return seats;
    }
    private void Suggest(){Bank.Seats=PrSeats();Bank.Locked=false;Save();}
    private void Render()
    {
        if(!Settings.GetBool(nameof(ShowWindow),true))return;
        ImGui.SetNextWindowSize(new(640,760),ImGuiCond.FirstUseEver);
        if(ImGui.Begin("锁刃龙 · 个人走位与职能###LocalArkveldPersonal"))
        {
            ImGui.TextUnformatted(replay?"ARR / MP 回放 · 只预览，不移动":"实战 · 仅移动本人");
            ImGui.TextWrapped("默认按 PR 小队职业自动分配；设置了本人职能，本人按设置。也可以在下面手动排八人并锁定，锁定后按手动名单。实战与回放名单独立。");
            bool editing=!Bank.Locked && (replay || !Session.InCombat);
            if(!replay)
            {
                int selected=Own+1;
                ImGui.BeginDisabled(Bank.Locked||Session.InCombat);
                if(ImGui.Combo("本人职能",ref selected,"未指定（读 PR）\0MT\0ST\0H1\0H2\0D1\0D2\0D3\0D4\0"))Settings.Set(nameof(OwnRole),(PersonalRole)selected);
                ImGui.EndDisabled();
            }
            ImGui.TextUnformatted($"读取到 {Party.Count}/8 人 · "+(Bank.Locked?"手动名单 · "+Bank.Error:(Own>=0?"自动（PR 小队职业 + 本人设置）· ":"自动（PR 小队职业）· ")+Auto.Error));
            if(!Bank.Locked && Auto.Locked)
                ImGui.TextWrapped("当前生效："+string.Join("  ",Enumerable.Range(0,8).Select(i=>$"{(Role)i} {Party.FirstOrDefault(p=>p.Key==Auto.Seats[i])?.Name}")));
            ImGui.BeginDisabled(!editing);
            for(int i=0;i<8;i++)
            {
                var member=Party.FirstOrDefault(p=>p.Key==Bank.Seats.ElementAtOrDefault(i));
                if(ImGui.BeginCombo(((Role)i).ToString(),member?.Label??"请选择角色"))
                {
                    foreach(var p in Party)if(ImGui.Selectable(p.Label+"##"+p.Key,member?.Key==p.Key))
                    {
                        int old=Array.IndexOf(Bank.Seats,p.Key);
                        if(old>=0)(Bank.Seats[old],Bank.Seats[i])=(Bank.Seats[i],Bank.Seats[old]);else Bank.Seats[i]=p.Key;
                        Save();
                    }
                    ImGui.EndCombo();
                }
            }
            if(ImGui.Button("从宿主预填（仍需确认）"))Suggest();
            ImGui.SameLine();
            if(ImGui.Button("确认并锁定八人职能"))
            {
                if(Bank.Confirm(Party,perspective,OwnFor(Bank),replay))
                {bool fighting=Session.InCombat;Save();Session.Reset();haveBound=false;nextScan=default;lateStart=!replay&&fighting;}
            }
            ImGui.EndDisabled();
            ImGui.BeginDisabled(!replay&&Session.InCombat);
            if(Bank.Locked && ImGui.Button("解锁并调整")){Release();Bank.Locked=false;Save();}
            ImGui.EndDisabled();
            if(lateStart)ImGui.TextWrapped("本轮缺少开场历史或名单改变，暂停自动走位；下次开战重新接入。");
            if(notice.Length>0)ImGui.TextWrapped(notice);
            ImGui.Separator();ImGui.TextWrapped(Session.Fight.Mechanic);
            if(ImGui.CollapsingHeader("八职能路线与真实危险范围"))RenderMap();
        }
        ImGui.End();
    }
    private void DrawTargets()
    {
        var wanted=new HashSet<string>();
        for(int i=0;i<8;i++)if(plans[i] is {} p)
        {string id="arkveld/goal/"+i;float y=Session.Actors.GetValueOrDefault(Session.Roles[i])?.Position.Y??0;Draw.AddAoe(id,new CircleGeometry(new(p.Goal.X,y,p.Goal.Y),.35f),true);wanted.Add(id);}
        foreach(string id in drawing.Except(wanted).ToArray())Draw.Remove(id);
        drawing.Clear();drawing.UnionWith(wanted);
    }
    private void RenderMap()
    {
        float width=Math.Clamp(ImGui.GetContentRegionAvail().X,260,600),scale=width/42;
        var origin=ImGui.GetCursorScreenPos();var canvas=ImGui.GetWindowDrawList();
        Vector2 At(Vector2 p)=>origin+(p-new Vector2(79,79))*scale;
        canvas.AddCircleFilled(At(Arena.Center),ArkveldWalking.Radius*scale,0xFF292929);
        canvas.AddCircle(At(Arena.Center),ArkveldWalking.Radius*scale,0xFFFFFFFF,80,1);
        double t=ArkveldSession.Seconds(CurrentTime);
        var zones=Session.Fight.Zones.Where(z=>z.Until>=t && z.From<=t+3).ToArray();
        for(float z=80;z<120;z+=.6f)for(float x=80;x<120;x+=.6f)
        {var p=new Vector2(x+.3f,z+.3f);if(Vector2.DistanceSquared(p,Arena.Center)<400 && zones.Any(a=>a.Contains(p)))canvas.AddRectFilled(At(new(x,z)),At(new(x+.6f,z+.6f)),0x554040EE);}
        for(int i=0;i<8;i++)
        {
            if(!Session.Actors.TryGetValue(Session.Roles[i],out var actor))continue;
            uint color=i<2?0xFFFFAA55u:i<4?0xFF77DD88u:0xFF8888FFu;
            var start=ArkveldSession.Flat(actor.Position);canvas.AddCircleFilled(At(start),3,color);
            if(plans[i] is not {} p)continue;
            foreach(var step in p.Steps){canvas.AddLine(At(start),At(step),color,1.5f);start=step;}
            canvas.AddCircle(At(p.Goal),5,color,20,2);canvas.AddText(At(p.Goal),color,((Role)i).ToString());
        }
        ImGui.Dummy(new(width,width));
        ImGui.TextWrapped("上北下南；红格为未来3秒危险范围的并集，不等于此刻全部生效。实际寻路逐段检查判定时间。");
        for(int i=0;i<8;i++)ImGui.TextWrapped($"{(Role)i}: {plans[i]?.Name??"等待开战或职能确认"}");
    }
    private void Release(){api?.RequestDirectTraversal(this,null,CurrentTime);api?.RequestAutomaticMovement(this,false);}
    protected override void OnStop()
    {
        Release();if(ui!=null)drawEvent?.RemoveEventHandler(ui,(Action)Render);ui=null;
        foreach(string id in drawing)Draw.Remove(id);drawing.Clear();Session.Reset();
    }
}
}
