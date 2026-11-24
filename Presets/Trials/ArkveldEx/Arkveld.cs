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
using System.Numerics;



internal sealed partial class Arkveld(bool personal=false) : WalkingEncounter
{
    private static readonly Vector2 Center=Arena.Center;
    private static Vector2 Right(Vector2 direction)=>new(direction.Y,-direction.X);
    private static float Facing(Vector2 direction)=>MathF.Atan2(direction.X,direction.Y);
    private static bool OnField(Vector2 p)=>Vector2.DistanceSquared(p,Center)<24*24;
    private static bool Far(Role r)=>r is Role.ST or Role.H2 or Role.D3 or Role.D4;
    public static bool IsAction(uint id)=>id is >=43887 and <=43960 or >=45077 and <=45111 or 45125 or 45126 or 45202
        or 44805 or 44806 or >=44809 and <=44812 or 44878 or 44792 or 44793;
    public readonly ArkveldStacks Stacks=new();
    private readonly Dictionary<ulong,(Vector2 Point,float Radius)> crystals=[];
    private readonly Dictionary<ulong,(Vector2 Point,Vector2 Velocity,double At)> motions=[];
    public bool NeedsPartyUpdate(ulong actor,double time)
    {
        return (NumberActive(time) || time<=spreadEnd+.25 && spreadEnd-time<4.5 ||
            waitingStack && singleImpact==null || dual.Count==2 && dualEnd-time is >=0 and <=4.3) && Members.Contains(actor);
    }
    public bool PositionChanged(ulong actor,Vector2 point)=>
        !positions.TryGetValue(actor,out var previous) || Vector2.DistanceSquared(previous,point)>.3f*.3f;
    public override void ObservePosition(ulong actor,Vector2 point,double time)
    {
        Position(actor,point);
        if(!personal)return;
        motions.TryGetValue(actor,out var old);
        double dt=time-old.At;var delta=point-old.Point;
        if(delta.LengthSquared()<.003f && dt<.55)return;
        var v=dt is >.03 and <1?delta/(float)dt:Vector2.Zero;
        // ARR occasionally reports a correction a few frames after a normal
        // position. Use its new position without reversing the observed heading.
        if(dt is >0 and <.18 && v.LengthSquared()>4*Walking.SprintSpeed*Walking.SprintSpeed)v=old.Velocity;
        if(v.LengthSquared()>Walking.SprintSpeed*Walking.SprintSpeed)v=Vector2.Normalize(v)*Walking.SprintSpeed;
        motions[actor]=(point,v,time);
    }
    private Vector2 Projected(ulong id,Vector2 p,double time,double hit)
    {
        if(!motions.TryGetValue(id,out var m) || time-m.At> .7)return p;
        float lead=(float)Math.Clamp(hit-time,0,.85);
        return ArkveldWalking.Clamp(p+m.Velocity*lead);
    }
    private Vector2 SpreadProjection(ulong id,Vector2 p,double time,double hit)
    {
        if(!motions.TryGetValue(id,out var m) || time-m.At>.8)return p;
        float lead=(float)Math.Clamp(hit-m.At,0,2.3);
        return ArkveldWalking.Clamp(p+m.Velocity*lead);
    }
    private readonly Dictionary<ulong,double> crystalIgnitions=[];
    private readonly Dictionary<(ulong Source,uint Action),Zone> awaitingArea=[];
    private double irregularUntil=-100,ringsBegan=-100;
    private readonly HashSet<(uint Sequence,uint Action,ulong Source)> received=[];
    private readonly Dictionary<ulong,Vector2> dual=[];
    private readonly HashSet<ulong> resolvedDual=[];
    private bool DualStackActive(double time)=>time<=dualEnd+.32 ||
        dual.Count==2 && resolvedDual.Count<2 && time<=dualEnd+1.65;
    private readonly HashSet<ulong> spreadMarks=[],resolvedSpread=[];
    private bool SpreadPending(double time)=>time<=spreadEnd+.27 && !spreadMarks.SetEquals(resolvedSpread);
    internal const ulong ActorHazard=0x100000003;
    public float LastCleave {get;private set;}
    public Vector2 LastLanding=>lastLanding;
    public Vector2? StackSite(Signal e)=>e.Id==44812?singleImpact??PointOf(singleActor):PointOf(e.Target);
    private readonly Dictionary<ulong,double> stackIcons=[];
    private readonly Dictionary<Role,bool> committedGroups=[];
    private ulong laserMarker;
    public ulong LaserMarker=>laserMarker;
    private readonly ulong[] numbers=new ulong[8];
    private readonly Dictionary<ulong,uint> recentIcons=[];
    private double recentIconTime=-100;
    private void ResolveNumberIcons()
    {
        if(recentIcons.Count!=8 || Math.Abs(numberStart-recentIconTime)>1)return;
        var ids=recentIcons.Values.Order().ToArray();
        if(ids.Distinct().Count()!=8 || (ulong)ids[^1]-ids[0]!=7)return;
        foreach(var (actor,id) in recentIcons)numbers[id-ids[0]]=actor;
    }
    private readonly List<Vector2> regular=[],tank=[];
    private bool waitingStack,straightLaser;
    private Vector2? burstOrigin;
    private double burstEnd=-100,laserHit;
    private float laserAngle;
    private int laserRepeats;
    private ulong currentLaserTarget;
    public ulong CurrentLaserTarget=>currentLaserTarget;
    internal const ulong GroundBait=0x100000004,LaserPrediction=0x100000005;
    private readonly HashSet<ulong> unavailable=[];
    public void SetAlive(ulong actor,bool alive)
    {
        if(alive)unavailable.Remove(actor);else unavailable.Add(actor);
    }
    private double singleEnd=-100,dualEnd=-100,flightEnd=-100,firstLane=-100,bladeFirst=-100,bladeEnd=-100;
    private double towerEnd=-100,towerStart=-100,lastBait=-100,spreadEnd=-100,spinEnd=-100;
    private double laserStart=-100,laserLock=-100,numberStart=-100,lastJump=-100,numberEnd=-100;
    private double crossStart=-100,stackIconTime=-100,numberIconTime=-100,firstImpact;
    private Vector2 bladeOrigin,observedWideOrigin;
    private double bladeWindupAt=double.NegativeInfinity;
    private bool displacedBlade;
    private readonly Vector2?[] outerBladePocket=new Vector2?[8];
    private readonly bool[] outerBladeChecked=new bool[8];
    private Vector2 flightAxis,flightSide,bladeSide,bladeAlong,spreadAxis,spinOrigin,laserOrigin,lastLanding;
    private Vector2 waveAxis;
    private double waveStart=-100,groundBait=-100;
    private int groundWaves;
    private bool narrowFlight;
    private double sprintAt=double.NegativeInfinity;
    public double SprintUntil {private get;set;}=double.PositiveInfinity;
    public float PreviewSpeed(double t)
    {
        if(WantsSprint(t) && t>=sprintAt+60){sprintAt=t;SprintUntil=t+10;}
        return t<sprintAt+10?Walking.SprintSpeed:Walking.Speed;
    }
    private float lastExaAngle;
    private int baits,jumped,turn,lasers;
    private ulong singleActor;
    private Vector2? singleImpact;
    public int Jumps=>jumped;
    public int Number(ulong actor)=>Array.IndexOf(numbers,actor)+1;
    public bool NumberActive(double t)=>t<=numberEnd && turn!=0;
    public bool Tracks(Signal e)=>e.Kind switch
    {
        Input.Cast or Input.Hit=>IsAction(e.Id),
        Input.Spawn=>e.Id is 0x48e5 or 0x48e6 or 0x48e7,
        Input.Gone or Input.Position=>crystals.ContainsKey(e.Source) || e.Source==bossId && bossId!=0,
        Input.Death=>e.Source==bossId && bossId!=0,
        Input.Icon=>Members.Contains(e.Source),
        _=>false
    };
    public Vector2 NumberPoint(int number)
    {
        Vector2 offset=((number-1)%4) switch {0=>new(18*turn,0),1=>new(0,18),2=>new(-18*turn,0),_=>new(0,-18)};
        return Center+offset;
    }
    public ulong JumpActor=>jumped<8?numbers[jumped]:0;
    private Vector2? PointOf(ulong id)=>!unavailable.Contains(id) && positions.TryGetValue(id,out var p) && OnField(p)?p:null;
    private static Vector2 Unit(Vector2 v,Vector2 fallback)=>v.LengthSquared()>.01f?Vector2.Normalize(v):fallback;
    private void Hazard(Shape shape,Vector2 p,float angle,float length,float width,double hit,ulong owner=0,int roles=255)
    {
        Zones.Add(new(shape,p,angle,length,width,hit-.16,hit+.20,roles,owner){Snapshot=hit});
    }
    private void Circle(Vector2 p,float r,double hit,ulong owner=0,int mask=255)=>Hazard(Shape.Circle,p,0,r+.32f,0,hit,owner,mask);
    private void Line(Vector2 p,float angle,float length,float width,double hit,ulong owner=0)
        =>Hazard(Shape.Line,p,angle,length,width+.32f,hit,owner);
    // A cast packet reports remaining time rounded down by the server (~0.3 s).
    public static double Snapshot(Signal e)=>e.Time+e.Duration+.24;
    public override bool WantsSprint(double time)=>NumberActive(time) && jumped>=1 && jumped<8 ||
        time>=laserStart && time<laserStart+18 ||
        time>=crossStart && time<crossStart+40 && time>=bladeFirst-1 && time<bladeEnd-.5 ||
        personal && lasers==3 && time>=laserLock+4 && time<laserLock+14;
    private bool CrystalSweep(double time)=>time>=laserStart && time<laserStart+35 && lasers<=3 &&
        crystals.Any(c=>!crystalIgnitions.TryGetValue(c.Key,out var t) || t>=time-.35) &&
        (lasers==1 || lasers>=2 && Zones.Any(z=>z.Shape==Shape.Circle && z.Until>=time && z.From<=time+5.5));
    private bool TightGrid(double time)=>time>=crossStart && time<=crossStart+39.5 &&
        (bladeEnd<crossStart || BladeActive(time));
    private sealed class OpenWalk
    {
        public double Last=-100,Stopped=-100,Until;
        public int Window=-1;
        public Vector2 Observed,Destination,Anchor,Foot,Direction; public bool HasFoot;
        public ArkveldRoom Room;
    }
    private readonly OpenWalk[] openWalks=Enumerable.Range(0,8).Select(_=>new OpenWalk()).ToArray();
    private Destination CommonStride(Role role,double now,Vector2 current,Destination activity,Zone[] zones)
    {
        var step=openWalks[(int)role];
        double dt=now-step.Last;
        if(dt<0 || dt>.9){step.HasFoot=false;step.Stopped=now;step.Direction=default;}
        else if(dt>.002 && Vector2.DistanceSquared(current,step.Observed)>.075f*.075f)
        {
            if(!step.HasFoot || now-step.Stopped>.30){step.Foot=step.Observed;step.HasFoot=true;}
            step.Direction=Unit(current-step.Observed,step.Direction);step.Stopped=now;
        }
        else if(now-step.Stopped>.30)step.HasFoot=false;
        step.Observed=current;step.Last=now;
        if(personal || activity.Room is not (ArkveldRoom.Cross or ArkveldRoom.Rush or ArkveldRoom.Tail or ArkveldRoom.Flight) ||
            activity.Required!=null || activity.ContactOnly || !bossAvailable || TightGrid(now) || activity.Room==ArkveldRoom.Rush && now<irregularUntil)
        {step.Window=-1;return activity;}
        // Discretionary extensions use the same clock as idle walking and formation shifts.
        if(IsCasting(role,now)){step.Window=-1;return activity;}
        var raw=activity.Point;
        double end=Math.Clamp(activity.Deadline+.24,now+1.1,now+4.1);
        bool Safe(Vector2 p)=>HasRoom(p,role,now,end,zones,.55f);
        if(step.Window==movementWindow && step.Room==activity.Room && now<step.Until && Vector2.DistanceSquared(raw,step.Anchor)<4 && Safe(step.Destination))
            return activity with {Point=step.Destination};
        if(!CanRoam(role,now))return activity;
        if(step.HasFoot && now-step.Stopped<=.24 && activity.Deadline-now>.70)
        {
            float traveled=Vector2.Distance(current,step.Foot);
            var dir=step.Direction;
            if(traveled is >.18f and <5.25f && Vector2.DistanceSquared(current,raw)<10.24f &&
                dir.LengthSquared()>.70f && Vector2.Dot(raw-current,dir)>-.25f)
            {
                var p=current+dir*(5.65f-traveled);
                float room=activity.Room==ArkveldRoom.Flight?1.85f:3.00f;
                if(Vector2.Distance(raw,p)<=room && Safe(p) && now+Vector2.Distance(current,p)/Walking.Speed<activity.Deadline-.23 &&
                    (!WantsMelee(role) || EdgeDistance(current)>MeleeLimit || EdgeDistance(p)<=MeleeLimit))
                {
                    float gap=9;foreach(ulong actor in Members)if(actor!=Members[(int)role] && PointOf(actor) is {} pt)gap=Math.Min(gap,Vector2.Distance(p,pt));
                    if(gap>.90f){
                        step.Window=movementWindow;step.Room=activity.Room;step.Anchor=raw;step.Destination=p;step.Until=now+.75+Vector2.Distance(current,p)/Walking.Speed;
                        SkipRoam(role,now);
                        return activity with {Point=p};}
                }
            }
        }
        if(now-step.Stopped<.25 || activity.Deadline-now<1.05)return activity;
        float distance=Vector2.Distance(raw,current);
        if(distance<.25f || distance>5.2f)return activity;
        var best=current;float value=float.PositiveInfinity;
        float reserve=activity.Room==ArkveldRoom.Flight?1.65f:2.8f;
        float crowd(Vector2 q)
        {
            float near=8;foreach(var actor in Members)if(actor!=0 && actor!=Members[(int)role] && PointOf(actor) is {} a)near=Math.Min(near,Vector2.Distance(q,a));
            return MathF.Max(0,1.35f-near);
        }
        if(distance<reserve+.1f && Safe(current))value=.9f*distance+3f*crowd(current);
        uint salt=Variation(role,62);
        for(int i=0;i<24;i++)
        {
            var p=current+Arena.Direction((i+(salt%79)*.03f)*MathF.Tau/24)*(5.55f+(salt%110)*.01f);
            float extra=Vector2.Distance(raw,p);
            if(extra>reserve || !Safe(p) || now+Vector2.Distance(current,p)/Walking.Speed>activity.Deadline-.25)continue;
            float score=extra*.62f+MeleePenalty(role,p)*1.7f+2.9f*crowd(p);
            if(score<value){best=p;value=score;}
        }
        if(!float.IsFinite(value))return activity;
        step.Window=movementWindow;step.Room=activity.Room;step.Anchor=raw;step.Destination=best;step.Until=now+1.0+Vector2.Distance(current,best)/Walking.Speed;
        SkipRoam(role,now);
        return activity with {Point=best};
    }
    public override WalkPlan Plan(Role role,double time,Vector2 current,float speed)
    {
        if(personal)localSpinRole=(int)role;
        SampleFooting(role,current,time);
        Stacks.Advance(time);
        Zones.RemoveAll(z=>z.Owner==ActorHazard);
        if(NumberActive(time) && jumped<8)ChaseForecast(role,time,current);
        if(personal && NumberActive(time) && JumpActor==Members[(int)role] && JumpActor!=0 &&
            firstImpact+jumped*3.09-time<2.0)
        {
            // Once our number becomes the jump target, another body in our
            // landing circle is an actual failed bait even if we reach our marker.
            double at=firstImpact+jumped*3.09;
            foreach(ulong id in Members)
                if(id!=JumpActor && PointOf(id) is {} p)
                    AvoidMovingActor(id,p,time,at,true);
        }
        if(personal && SpreadPending(time) && spreadEnd-time<5.0)
            foreach(var id in spreadMarks.Contains(Members[(int)role])?Members.AsEnumerable():spreadMarks)
                if(id!=Members[(int)role] && PointOf(id) is {} p)
                    AvoidMovingActor(id,p,time,spreadEnd);
        var goal=Goal(role,time,current);
        // The jump bait's occupied circle and following half-room move with its owner.
        // A stale forecast cannot occupy the next half-room before the jump actually locks.
        var timed=Zones.Select(z=>z.Snapshot is {} hit && (NumberActive(time)?z.Shape==Shape.Cone:z.Owner!=ActorHazard || CrystalPhase(time))
            ?z with {From=hit-(goal.Room==ArkveldRoom.Blade && z.Shape==Shape.Line && z.Width<4?.38:PlanLead)-(z.AwaitEffect && (time<irregularUntil || z.Shape==Shape.Ring) ? .62 : 0),
                Until=z.AwaitEffect?Math.Min(z.Until,Math.Max(hit+(time<irregularUntil?.7:.22),time+.29)):z.Until}:z).ToArray();
        bool uptime=WantsMelee(role) && (CrystalPhase(time) && !(personal && SpreadPending(time)) &&
            !timed.Any(z=>z.Shape==Shape.Circle && z.Until>=time && z.From<=time+5 && z.Contains(current)) || BladeActive(time));
        bool bait=IsLaserBait(role,time);
        bool formation=IsFormation(goal.Room);
        var activity=bait?goal:formation?FormationGoal(role,time,current,goal,timed,speed):ActivityGoal(role,time,current,goal,timed);
        if(!bait)activity=CommonStride(role,time,current,activity,timed);
        bool locked=bait || goal.Room==ArkveldRoom.Beam && CrystalSweep(time) || goal.Room==ArkveldRoom.Rush && time<irregularUntil ||
            goal.Room==ArkveldRoom.Cross && TightGrid(time);
        bool flex=!personal && !locked && (formation && goal.Room!=ArkveldRoom.Blade || CrystalPhase(time) && !SpreadPending(time));
        uint ord=FormationSeed(79);
        int index=(((int)role*5+(int)(ord&7))&7);
        var fine=flex?Arena.Direction((index+.21f)*MathF.Tau/8)*(.64f+((index&1)==0?.20f:0)):Vector2.Zero;
        bool follower=!personal && dual.Count==2 && !dual.ContainsKey(Members[(int)role]) && DualStackActive(time);
        bool flexibleTarget=!personal && !locked && (goal.Room is ArkveldRoom.Beam or ArkveldRoom.Flight or ArkveldRoom.Bait or ArkveldRoom.Cross or ArkveldRoom.Tail || follower);
        if(goal.Room==ArkveldRoom.Beam && Members[(int)role]==laserMarker)flexibleTarget=false;
        Vector2[]? neighbours=null;
        if(flexibleTarget || !personal && !locked && goal.Room==ArkveldRoom.Rush)
        {
            int rank(int slot)=>(slot*5+(int)(ord&7))&7;
            neighbours=Enumerable.Range(0,8).Where(s=>s!=(int)role && rank(s)<rank((int)role) && PointOf(Members[s]).HasValue)
                .Select(s=>{ulong id=Members[s];var p=PointOf(id)!.Value;return Vector2.Lerp(p,Projected(id,p,time,time+.55),.5f);}).ToArray();
        }
        bool holding=activity.Discretionary && !CanRoam(role,time) && Vector2.DistanceSquared(activity.Point,current)<.01f;
        var plan=ArkveldWalking.Solve(current,role,time,activity,timed,speed,SprintUntil,footHint:holding?default:fine,party:neighbours,freedom:flexibleTarget && !holding?(follower?.9f:2.35f):0,meleeBoss:uptime?bossPoint:null,
            meleeRadius:bossRadius+MeleeLimit,reactUntil:goal.Room==ArkveldRoom.None || formation?ReactionUntil(role,activity,current,speed,time):0,
            finishCastAt:!bait && !holding && (goal.Required!=null || goal.Room is ArkveldRoom.Blade or ArkveldRoom.Cross or ArkveldRoom.Tail ||
                goal.Room==ArkveldRoom.Rush && !timed.Any(z=>z.Shape==Shape.Ring && z.Until>=time && z.From<time+5)) ? CastEnd(role) : 0,
            overlappingRings:goal.Room==ArkveldRoom.Rush && rushLanes.Count>=2 &&
                timed.Count(z=>z.Shape==Shape.Ring && z.Until>=time && z.From<=time+5)>=2);
        // Only a purely discretionary step may wait for a cast. The required switch of sides always wins.
        if(activity.Discretionary && activity.Room!=ArkveldRoom.None && plan.Feasible && plan.Steps.Length==1 && Vector2.DistanceSquared(plan.Steps[0],activity.Point)<.0025f)
            plan=plan with {OptionalStride=true,RestJump=CanRestJump(role,time,current,activity,timed)};
        return MarkAppointment(plan,goal,role,time,current,speed);
    }
    private void AvoidMovingActor(ulong id,Vector2 p,double now,double hit,bool numbered=false)
    {
        void Avoid(Vector2 center)
        {
            // A numbered target keeps moving until the jump, including its last
            // fraction of a second. Use the jump's snapshot for player separation.
            if(numbered)Zones.Add(new(Shape.Circle,center,0,6.2f,0,hit-.035,hit+.06,Owner:ActorHazard){Snapshot=hit});
            else Circle(center,6,hit,ActorHazard);
        }
        Avoid(p);
        var lead=Projected(id,p,now,hit);
        if(Vector2.DistanceSquared(lead,p)>1)Avoid(lead);
    }
    public override void Feed(Signal e)
    {
        if(e.Kind==Input.Spawn)
        {
            Position(e.Source,e.Point);
            if(e.Id==0x48e5)ObserveBoss(e.Source,e.Point,7.8f,true);
            if(e.Id is 0x48e6 or 0x48e7)crystals[e.Source]=(e.Point,e.Id==0x48e7?12:6);
            return;
        }
        if(e.Kind==Input.Gone){if(e.Source==bossId)bossAvailable=false;crystals.Remove(e.Source);crystalIgnitions.Remove(e.Source);positions.Remove(e.Source);return;}
        if(e.Kind==Input.Death && e.Source==bossId){bossAvailable=false;return;}
        if(e.Kind==Input.Position){Position(e.Source,e.Point);if(e.Source==bossId)bossPoint=e.Point;return;}
        if(e.Kind==Input.Icon)
        {
            if(Members.Contains(e.Source))
            {
                if(e.Time-recentIconTime>1)recentIcons.Clear();
                recentIconTime=e.Time;recentIcons[e.Source]=e.Id;
                if(NumberActive(e.Time))ResolveNumberIcons();
            }
            if(e.Id==100)
            {
                if(e.Time-stackIconTime>1)stackIcons.Clear();
                stackIconTime=e.Time;stackIcons[e.Source]=e.Time;
                if(e.Time<=singleEnd && dual.Count==0)singleActor=e.Source;
            }
            else if(e.Id==470)
            {
                laserMarker=e.Source;
            }
            else if(e.Id is >=404 and <=411)
            {
                if(e.Time-numberIconTime>2)Array.Clear(numbers);
                numberIconTime=e.Time;numbers[e.Id-404]=e.Source;
                if(e.Time>numberEnd){numberStart=e.Time;numberEnd=e.Time+33;jumped=turn=0;Mechanic="麻将 · 回中集合";}
            }
            return;
        }
        if(e.Kind==Input.Hit)
        {
            if(e.Value!=0 && !received.Add((e.Value,e.Id,e.Source)))return;
            if(lastCasts.TryGetValue((e.Source,e.Id),out var sent) &&
                Math.Abs(e.Time-Snapshot(sent)) is >.63 and <1.85)irregularUntil=e.Time+20;
            if(awaitingArea.Remove((e.Source,e.Id),out var resolved))Zones.Remove(resolved);
            if(e.Id==43911)ringsBegan=e.Time;
            if(e.Id==43937)firstBeamResolved=true;
            if(e.Id is 43891 or 43892 or 45085 or 45088)
            {
                var started=LastCast(e);
                if(started.Id!=0)Zones.RemoveAll(z=>z.Shape==Shape.Line && z.Width>14 && z.Snapshot is {} at &&
                    Math.Abs(at-e.Time)<.5 && Vector2.DistanceSquared(z.Origin,started.Point)<.16f);
                bladeFirstResolved=true;
            }
            if(e.Id is 43897 or 43898 or 45093 or 45096)bladeSecondResolved=true;
            if(e.Id is 43926 or 43927 or 43952 or 43953 or 44809 or 44810)
                ConfirmBlast(e);
            if(e.Id==43932)
            {
                RecordSpinFormation(e.Time);
                if(spreadMarks.Contains(e.Target))resolvedSpread.Add(e.Target);
                if(crystalPhase && e.Time-lastCrystalSpread>1){lastCrystalSpread=e.Time;crystalSpreads++;}
            }
            if(e.Id is 43906 or 43907 && dual.ContainsKey(e.Target))resolvedDual.Add(e.Target);
            Stacks.Hit(e,Members);
            if(e.Id is 43956 or 43959 && NumberActive(e.Time) && jumped<8 && e.Time-lastJump>.8)
            {
                var p=OnField(e.Point)?e.Point:PointOf(JumpActor)??NumberPoint(jumped+1);
                var from=lastLanding;
                float aim=Facing(p-from)+turn*MathF.PI/2;
                LastCleave=aim;lastLanding=p;lastJump=e.Time;jumped++;
                Hazard(Shape.Cone,p,aim,60,MathF.PI/2+.045f,e.Time+2.02,JumpCone);
                firstImpact=e.Time+3.09-jumped*3.09;
                if(jumped==8)numberEnd=e.Time+2.26;
            }
            if(e.Id==44812)
            {
                waitingStack=false;singleEnd=-100;
                if(OnField(e.Point))singleImpact=e.Point;
            }
            if(e.Id==43948 && Members.Contains(e.Target)){singleActor=e.Target;singleImpact=PointOf(e.Target);}
            return;
        }
        if(e.Kind!=Input.Cast || !IsAction(e.Id))return;
        ObserveMovementCue(e);
        lastCasts[(e.Source,e.Id)]=e;
        if(e.Source==bossId && e.SourcePoint is {} actual && OnField(actual))bossPoint=actual;
        if(e.Id is 43934 or 43919)crystalPhase=false;
        var end=Snapshot(e);var axis=Arena.Direction(e.Angle);
        // A new major sequence is also a recovery point if the stack effect was lost.
        if(e.Id is 43919 or 43936 or 43940 or 43944 or 43952 or 43955 or 43958 or 43887 or 43888 or 45081 or 45082)
            waitingStack=false;
        switch(e.Id)
        {
            case 43887 or 43888 or 45081 or 45082:
                bladeWindupAt=e.Time;bladeOrigin=e.Point;bladeSide=Right(axis);Mechanic="锁刃敲打";break;
            case 43889 or 43890 or 43895 or 43896 or >=45077 and <=45080 or 45083 or 45084 or 45086 or 45087 or 45091 or 45092 or 45094 or 45095:
                Line(e.Point,e.Angle,40,2,end);
                if(e.Id is 43889 or 43890 or 45077 or 45078 or 45083 or 45084 or 45086 or 45087)
                {
                    var offset=e.Point-bladeOrigin;
                    var mirror=e.Point-2*Vector2.Dot(offset,bladeSide)*bladeSide;
                    var mirrored=axis-2*Vector2.Dot(axis,bladeSide)*bladeSide;
                    Line(mirror,Facing(mirrored),40,2,end+4.08,BladePrediction);
                }
                break;
            case 43891 or 43892 or 45085 or 45088:
            {
                bladeFirstResolved=bladeSecondResolved=false;
                bladeFirst=end;bladeEnd=end+4.1;bladeAlong=axis;
                var perp=Right(axis);
                observedWideOrigin=e.Point;
                var pivot=e.Time-bladeWindupAt<=1.5 && OnField(bladeOrigin)?bladeOrigin:
                    e.SourcePoint is {} sourceAt && OnField(sourceAt)?sourceAt:
                    bossAvailable && OnField(bossPoint)?bossPoint:Center;
                float side=Vector2.Dot(pivot-e.Point,perp);
                displacedBlade=side*Vector2.Dot(Center-e.Point,perp)<0;
                Array.Clear(outerBladePocket);Array.Clear(outerBladeChecked);
                bladeSide=perp*(side>=0?1:-1);
                Line(e.Point,e.Angle,80,14,end);
                AwaitArea(e,end);
                Line(e.Point+bladeSide*14,e.Angle,80,14,bladeEnd,BladePrediction);
                break;
            }
            case 43897 or 43898 or 45093 or 45096:
                Zones.RemoveAll(z=>z.Owner==BladePrediction);bladeEnd=end;
                float separation=Vector2.Dot(e.Point-observedWideOrigin,Right(axis));
                if(Math.Abs(separation) is >=10 and <=18)bladeSide=Right(axis)*Math.Sign(separation);
                Line(e.Point,e.Angle,80,14,end);AwaitArea(e,end);break;
            case 43900 or 43903 or 45099 or 45101:
                dual.Clear();resolvedDual.Clear();committedGroups.Clear();dualEnd=-100;
                Mechanic="飞翔 · 提前靠近分摊侧";flightAxis=axis;flightSide=-Right(axis);
                narrowFlight=e.Id is 43903 or 45101;firstLane=end;flightEnd=end+3.55;
                Line(e.Point,e.Angle,40,4,end);break;
            case 43901 or 45100:Line(e.Point,e.Angle,40,8,end);break;
            case 43904 or 43905 or 45102 or 45103:Line(e.Point,e.Angle,40,9,end);break;
            case 43906 or 43907:
                if(e.Time>dualEnd){dual.Clear();resolvedDual.Clear();committedGroups.Clear();waitingStack=false;singleEnd=-100;}
                dual[e.Target]=e.Point;dualAction=e.Id;dualEnd=end;Stacks.Expect(e.Id,end,Members);Mechanic="双组分摊";break;
            case 43947:
                Mechanic="全队分摊";waitingStack=true;burstOrigin=null;singleEnd=e.Time+8.35;singleImpact=null;singleActor=e.Time-stackIconTime<1?stackIcons.Keys.FirstOrDefault():0;
                dual.Clear();Stacks.Expect(44812,singleEnd,Members);break;
            case 43933:
                burstOrigin=e.Point;burstEnd=end;Circle(e.Point,12,end);
                Mechanic="分摊后撤出大圈";break;
            case 43908:
            {
                if(rushLanes.Count>0 && e.Time>rushLanes[^1].Hit+8){rushLanes.Clear();Array.Clear(rushRevisions);}
                rushEnd=Math.Max(rushEnd,e.Time+6.4);
                var from=e.SourcePoint??positions.GetValueOrDefault(e.Source,e.Point);
                var path=e.Point-from;
                if(path.LengthSquared()>1 && !rushLanes.Any(l=>Vector2.DistanceSquared(l.To,e.Point)<.2f && Math.Abs(l.Hit-e.Time-6.10)<.2))rushLanes.Add((from,e.Point,e.Time+6.10));
                Line(from,Facing(path),path.Length(),7.1f,e.Time+6.10);
                Mechanic="三连突进 · 提前穿过安全侧";break;
            }
            case 43911 or 43912 or 43913 or 43914:
            {
                rushEnd=Math.Max(rushEnd,end+.30);
                int wave=(int)e.Id-43911;
                if(wave==0)Circle(e.Point,8.65f,end);
                else Hazard(Shape.Ring,e.Point,0,8+6*wave+.85f,8+6*(wave-1)-.85f,end);
                Mechanic="突进与连续环形爆炸";break;
            }
            case 43916 or 43918 or 45107 or 45108:
                if(Math.Abs(end-spinEnd)>1)spinRound++;
                spinOrigin=e.Point;spreadAxis=-axis;spinEnd=end;BuildSpinPositions();
                Hazard(Shape.Cone,e.Point,e.Angle,40,MathF.PI/2+.012f,end);Mechanic="回旋锁刃 · 半场分散";break;
            case 43932:
                if(e.Time>spreadEnd){spreadMarks.Clear();resolvedSpread.Clear();}
                spreadMarks.Add(e.Target);
                spreadEnd=end;Mechanic="八方分散";break;
            case 43949 or 44806 or 44805 or 45109:
                tailUntil=end+.2;tailAxis=axis;
                Line(e.Point,e.Angle,60,3,end);Mechanic="避开尾刺直线";break;
            case 43919:
                if(Math.Abs(towerStart-e.Time)>.5)StartTowers(e.Time);
                break;
            case 43920 or 43921:
                if(e.Time>towerEnd)StartTowers(e.Time);
                var group=e.Id==43920?regular:tank;
                if(!group.Any(p=>Vector2.DistanceSquared(p,e.Point)<.25f))
                {
                    group.Add(e.Point);
                    if(regular.Count==4 && tank.Count==2)SortTowers();
                }
                towerEnd=end;break;
            case 43923:
                if(e.Time-lastBait>.8)baits++;
                lastBait=e.Time;Circle(e.Point,6,end,GroundBait);break;
            case 43926 or 43952:
                lastExaAngle=MathF.Round(e.Angle/(MathF.PI/4))*MathF.PI/4 % (MathF.PI/2);
                if(lastExaAngle<0)lastExaAngle+=MathF.PI/2;
                if(lastExaAngle>MathF.PI/2-.01f)lastExaAngle=0;
                if(e.Id==43926)BeginCrystalWave(e.Time,end,lastExaAngle);
                else longFireEnd=end+5*1.1+.4;
                for(int k=0;k<(e.Id==43952?6:3);k++)
                {
                    var p=e.Point+axis*(8*k);double t=end+k*1.1;
                    Circle(p,6,t,e.Source);PredictCrystals(p,6,t+1.65);
                }
                Mechanic=e.Id==43952u?"末段波状龙闪":"龙脉爆发 · 地火与结晶";break;
            case 43924 or 43925:
                // This is the crystal's visual. The damaging helper snapshots about half
                // a second later; adjacent crystals are triggered by that blast.
                PredictCrystals(e.Point,e.Id==43925?12:6,end+.52+1.13);
                break;
            case 44809 or 44810:
                ConfirmCrystal(e.Point,e.Id==44810?12:6,end,e.Source);
                AwaitArea(e,end);break;
            case 43940:
                waveAxis=axis;waveStart=e.Time;
                for(int k=0;k<5;k++)Line(e.Point+8*k*axis,e.Angle,8,20,end+k*2.57);
                Mechanic="移动直线与诱导圈";break;
            case 43941:Line(e.Point,e.Angle,8,20,end);break;
            case 43942:
                if(e.Time-groundBait>6)groundWaves=0;
                if(e.Time-groundBait>.9)groundWaves++;
                groundBait=e.Time;Circle(e.Point,6,end,GroundBait);Mechanic="诱导圈 · 离开落点";break;
            case 43944:
                if(e.Time-crossStart>3){crossStart=e.Time;CrossLines(e.Angle,end);}
                Mechanic="交错移动直线";break;
            case 43945 or 43946:Line(e.Point,e.Angle,40,2,end);break;
            case 43955 or 43958:
                dispatchedNumber=0;dispatchedBait=null;jumped=0;turn=e.Id==43955?1:-1;numberStart=e.Time;numberEnd=e.Time+32.5;
                firstImpact=end;lastLanding=e.Point;lastJump=-100;ResolveNumberIcons();Mechanic="麻将 · 集合后按号出列";break;
            case 43957 or 43960:
                LastCleave=e.Angle;
                Zones.RemoveAll(z=>z.Owner==JumpCone && z.Until>=e.Time);
                Hazard(Shape.Cone,e.Point,e.Angle,60,MathF.PI/2+.01f,end,JumpCone);break;
            case 43936:
                laserStart=e.Time;lasers=0;straightLaser=false;laserOrigin=e.Point;laserLock=e.Time+8;Mechanic="龙闪炮 · 集合诱导";break;
            case 43937:
                firstBeamResolved=false;
                lasers++;laserLock=e.Time;laserOrigin=e.Point;currentLaserTarget=laserMarker;
                laserRepeats=0;straightLaser=false;UpdateLaser(e,end,true);break;
            case 43938:
                if(e.Time-laserLock<9)
                {
                    if(end<=laserHit+.08)break;
                    laserRepeats++;UpdateLaser(e,end,false);
                }
                else Line(e.Point,e.Angle,60,3,end);
                break;
            case 43934 or 43950 or 43951 or 45202:Mechanic="全屏伤害 · 集合";break;
            case 43954 or 44878:Mechanic="最终狂暴";break;
        }
        if(e.Id is >=43911 and <=43914 or 43945 or 43946)
            AwaitArea(e,end);
    }
    private void AwaitArea(Signal e,double end)
    {
        var key=(e.Source,e.Id);
        if(awaitingArea.Remove(key,out var previous))Zones.Remove(previous);
        var confirmed=Zones[^1] with {Until=end+1.65,AwaitEffect=true};
        Zones[^1]=confirmed;awaitingArea[key]=confirmed;
    }
    private const ulong JumpCone=0x100000001,BladePrediction=0x100000002;
    private void StartTowers(double t)
    {
        towerStart=t;towerEnd=t+11;baits=0;lastBait=-100;regular.Clear();tank.Clear();
        Mechanic="六座塔 · 集合放圈后进塔";
    }
    private void CrossLines(float angle,double at)
    {
        (float Offset,double Delay)[] waves=[(0,0),(6,2.6),(10,5.2),(14,7.8),(18,10.4),(18,13),
            (14,15.1),(10,17.7),(6,20.3),(2,22.9),(2,25.5),(6,28.1),(10,30.7),(14,33.3),(18,35.9)];
        for(int j=0;j<2;j++)
        {
            var a=angle+j*MathF.PI/2;var axis=Arena.Direction(a);var origin=Center-axis*20;
            foreach(var (off,delay) in waves)
            {
                var delta=Right(axis)*off;
                Line(origin+delta,a,40,off==0?4:2,at+delay);
                if(off>0)Line(origin-delta,a,40,2,at+delay);
            }
        }
    }
    private void PredictCrystals(Vector2 p,float r,double t)
    {
        foreach(var (id,c) in crystals)
            if(TouchesCrystal(p,r,c))
                Ignite(id,c.Point,c.Radius,t);
    }
    private static bool TouchesCrystal(Vector2 from,float radius,(Vector2 Point,float Radius) crystal)
    {
        // The chain is triggered at the crystal's collision ring. Its own blast is 6/12m.
        float reach=radius+(crystal.Radius>6?1.2f:.7f);
        return Vector2.DistanceSquared(from,crystal.Point)<=reach*reach;
    }
    private void Ignite(ulong id,Vector2 p,float radius,double hit)
    {
        if(crystalIgnitions.TryGetValue(id,out var earlier) && earlier<=hit+.025)return;
        crystalIgnitions[id]=hit;Zones.RemoveAll(z=>z.Owner==id);
        Circle(p,radius+.25f,hit,id);
        // An exploding crystal can trigger another crystal outside the original ground attack.
        foreach(var (other,c) in crystals)
            if(other!=id && TouchesCrystal(p,radius,c))Ignite(other,c.Point,c.Radius,hit+1.13);
    }
    private void UpdateLaser(Signal cast,double hit,bool first)
    {
        Zones.RemoveAll(z=>z.Owner==LaserPrediction);
        float delta=MathF.IEEERemainder(cast.Angle-laserAngle,MathF.Tau);
        straightLaser=!first && Math.Abs(delta)<.025f;
        float turnStep=first?MathF.Sign(Vector2.Dot(Center-cast.Point,Right(Arena.Direction(cast.Angle))))*MathF.PI/15:
            straightLaser?0:Math.Clamp(delta,-MathF.PI/15,MathF.PI/15);
        laserAngle=cast.Angle;laserHit=hit;
        for(int i=0;i<=8-laserRepeats;i++)
        {
            double at=hit+(i==0?0:first?1.0+(i-1)*.71:i*.71);
            float angle=cast.Angle+i*turnStep;
            Line(cast.Point,angle,60,3,at,i==0?0:LaserPrediction);
            var beam=new Zone(Shape.Line,cast.Point,angle,60,3,0,0);
            foreach(var (id,c) in crystals)
            {
                if(!beam.Contains(c.Point))continue;
                if(i==0)Ignite(id,c.Point,c.Radius,at+1.13);
                else if(!crystalIgnitions.ContainsKey(id) && !Zones.Any(z=>z.Owner==LaserPrediction && z.Shape==Shape.Circle && z.Origin==c.Point))
                    Circle(c.Point,c.Radius,at+1.13,LaserPrediction);
            }
        }
    }
}

}
