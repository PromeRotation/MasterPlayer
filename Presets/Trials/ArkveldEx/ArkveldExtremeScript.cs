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
using ReplayWorldCapture = MasterPlayer.Mechanics.Arkveld.ReplayWorldCapture;

namespace MasterPlayer.Mechanics.Arkveld;

public abstract class ArkveldExtremeScript : MasterPlayerScript
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
