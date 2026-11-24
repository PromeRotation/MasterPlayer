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



internal enum ArkveldRoom { None,Output,Number,Blade,Cross,Rush,Flight,Bait,Beam,Tail }

internal sealed partial class Arkveld
{
    private sealed class Stride
    {
        public ArkveldRoom Room;
        public int Window,Sequence;
        public Vector2 Goal,Previous,BeforePrevious,Home,Origin;
        public bool HeadingOut;
    }
    private sealed class Footwork
    {
        public ulong Actor;
        public int Sequence;
        public Vector2 LastFoot;
        public double Next,LastMove,Pause;
        public bool Moving;
        public uint Cast;
        public double CastEnd;
    }

    private readonly Footwork[] feet=Enumerable.Range(0,8).Select(_=>new Footwork()).ToArray();
    private readonly HashSet<ulong> physicalRanged=[];
    public override void SetJob(ulong actor,uint job)
    {
        if(job==0 || actor is 0 or 0xe0000000)return;
        base.SetJob(actor,job);
        if(job is 23 or 31 or 38)physicalRanged.Add(actor);
        else physicalRanged.Remove(actor);
    }
    private bool MobileJob(Role role)=>IsMelee(role) || physicalRanged.Contains(Members[(int)role]);
    internal double FreePause(Role role,int sequence)
    {
        double jitter=(Variation(role,sequence+149)%1001)/1000d;
        return MobileJob(role)?2+jitter:6+2*jitter;
    }
    private void SampleFooting(Role role,Vector2 here,double now)
    {
        var f=feet[(int)role];
        if(f.Actor!=Members[(int)role] || f.Pause==0){
            f.Actor=Members[(int)role];f.LastFoot=here;f.Moving=false;f.Sequence=0;
            f.Pause=FreePause(role,++f.Sequence);f.Next=now+f.Pause;
        }
        if(Vector2.DistanceSquared(here,f.LastFoot)>.06f*.06f){
            if(!f.Moving)f.Pause=FreePause(role,++f.Sequence);
            f.LastFoot=here;f.Moving=true;f.LastMove=now;f.Next=now+f.Pause;
        }else if(now-f.LastMove>.30)f.Moving=false;
        // Expired opportunities during casts are discarded, including the one immediately
        // after abandoning a voluntary leg. Nothing is queued to fire on the last cast tick.
        if(IsCasting(role,now) && f.Next<=now)SkipRoam(role,now);
    }
    private void SkipRoam(Role role,double now)
    {
        var f=feet[(int)role];f.Pause=FreePause(role,++f.Sequence);f.Next=now+f.Pause;
    }
    private bool CanRoam(Role role,double now)=>!IsCasting(role,now) && now>=feet[(int)role].Next;
    internal double CastEnd(Role role)=>feet[(int)role].CastEnd;
    internal bool IsCasting(Role role,double now)=>feet[(int)role].Cast!=0 && now<feet[(int)role].CastEnd;
    internal bool SetCasting(Role role,uint action,double end,double now)
    {
        var f=feet[(int)role];
        bool changed=action!=f.Cast || action!=0 && end>f.CastEnd+.45;
        if(changed && f.Cast!=0 && f.Pause>0 && f.Next<=Math.Min(now+.12,f.CastEnd+.12))
            SkipRoam(role,now);
        f.Cast=action;f.CastEnd=action==0?0:end;
        return changed;
    }
    internal bool EndCasting(Role role,uint action,double now)
    {
        var f=feet[(int)role];return (action==0 || f.Cast==action) && SetCasting(role,0,0,now);
    }

    private readonly Stride[] strides=Enumerable.Range(0,8).Select(_=>new Stride()).ToArray();
    private int movementWindow;
    private uint movementCue;
    private double cueAt=double.NegativeInfinity,quietUntil=double.NegativeInfinity;
    private ulong bossTarget;
    internal void ObserveTarget(ulong actor)=>bossTarget=actor!=0 && Members.Contains(actor)?actor:0;
    private void ObserveMovementCue(Signal e)
    {
        if(e.Id is not (43887 or 43888 or 45081 or 45082 or 43900 or 43903 or 45099 or 45101 or
            43908 or 43916 or 43918 or 45107 or 45108 or 43919 or 43926 or 43934 or 43936 or
            43940 or 43944 or 43947 or 43949 or 45109 or 43950 or 43951 or 43952 or 43955 or 43958 or 45202))return;
        if(movementCue==e.Id && e.Time-cueAt<7)return;
        movementWindow++;movementCue=e.Id;cueAt=e.Time;
        quietUntil=e.Id is 43934 or 43950 or 43951 or 45202?Snapshot(e)-1.25:double.NegativeInfinity;
    }

    // A pull/role/actor combination is repeatable when seeking an ARR backwards.
    // Only a new settled foot position samples a new stride.
    private uint Variation(Role role,int seq)
    {
        ulong id=Members[(int)role];
        uint h=unchecked((uint)bossId^(uint)id^(uint)(id>>32)^((uint)role+1)*0x85ebca6bu^(uint)movementWindow*0xc2b2ae35u^(uint)seq*0x9e3779b9u);
        h^=h>>16;h=unchecked(h*0x7feb352du);h^=h>>15;
        return h;
    }
    internal double MovementCueTime=>cueAt;
    internal double ReactionUntil(Role role,Destination goal,Vector2 current,float speed,double now)
    {
        double seed=(Variation(role,0)%997)/996d;
        double travel=Vector2.Distance(current,goal.Point)/speed;
        double latest=goal.Deadline-travel-.55;
        if (IsLaserBait(role,now) || goal.Room is ArkveldRoom.Flight or ArkveldRoom.Bait || latest<=now) return 0;
        double window=goal.Room==ArkveldRoom.Blade && FirstBladePending(now) ? 1.85 :
            goal.Room is ArkveldRoom.Output or ArkveldRoom.Cross or ArkveldRoom.Tail ? 1.25 : .33;
        double spare=Math.Clamp(latest-cueAt,0,window);
        return Math.Min(latest,cueAt+.12+seed*spare);
    }
    private bool IsLaserBait(Role role,double now)=>laserMarker!=0 && Members[(int)role]==laserMarker &&
        now>=laserStart && now<laserStart+34 && (lasers==0 || laserMarker!=currentLaserTarget);
    private static Vector2 GroupPocket(Role role,Vector2 anchor,float radius)
        =>ArkveldWalking.Clamp(anchor+Arena.Direction((int)role*MathF.Tau/8+.22f)*(radius*((int)role%2==0?.76f:1)));
    private Destination ActivityGoal(Role role,double now,Vector2 current,Destination goal,Zone[] danger)
    {
        var stride=strides[(int)role];
        if(!engaged || goal.Room==ArkveldRoom.None || !bossAvailable && goal.Room!=ArkveldRoom.Number)
        {stride.Room=ArkveldRoom.None;return goal;}
        bool number=goal.Room==ArkveldRoom.Number;
        bool melee=!number && WantsMelee(role);
        bool facing=melee && (bossTarget!=0?Members[(int)role]==bossTarget:role==Role.MT);
        float maxRadius=number?4.75f:melee?bossRadius+MeleeLimit-.30f:8.9f;
        if(personal)maxRadius=number?4.6f:melee?bossRadius+MeleeLimit-.35f:7.5f;
        var origin=melee?bossPoint:Center;
        bool newWindow=stride.Room!=goal.Room || stride.Window!=movementWindow || Vector2.DistanceSquared(stride.Origin,origin)>2.25f;
        if(!number && danger.Any(z=>z.Until>=now && z.From<=now+3.0 && (z.Roles & 1<<(int)role)!=0))
        {stride.Room=ArkveldRoom.None;return goal;}
        if(newWindow)
        {
            stride.Room=goal.Room;stride.Window=movementWindow;stride.Sequence=0;stride.Origin=origin;
            stride.Previous=stride.BeforePrevious=current;
            var radial=Unit(current-origin,Arena.Direction((int)role*MathF.Tau/8));
            stride.Home=melee?ArkveldWalking.Clamp(origin+radial*Math.Clamp(Vector2.Distance(current,origin),Math.Min(bossRadius*.7f,maxRadius),maxRadius)):
                GroupPocket(role,Center,number?2.1f:3.9f);
            stride.Goal=Vector2.Distance(current,origin)<=maxRadius?current:melee?goal.Point:stride.Home;
            stride.HeadingOut=false;
        }
        bool Clear(Vector2 p)
        {
            if(Vector2.DistanceSquared(p,origin)>maxRadius*maxRadius+.001f ||
                Vector2.DistanceSquared(p,Center)>ArkveldWalking.Radius*ArkveldWalking.Radius)return false;
            if(number)
            {
                // The player crossing centre to reach their own marker has priority.
                int n=Number(Members[(int)role]);
                if(n>jumped && Vector2.Distance(p,NumberPoint(n))/Walking.Speed+1.08>firstImpact+(n-1)*3.09-now)return false;
                if(jumped<8 && Vector2.DistanceSquared(p,ForecastLanding(jumped+1,now,Members[(int)role]))<7.2f*7.2f)return false;
            }
            return !danger.Any(z=>z.Until>=now && z.From<=now+1.8 && (z.Roles & 1<<(int)role)!=0 && z.Contains(p));
        }
        if(!Clear(stride.Goal))
        {stride.Room=ArkveldRoom.None;return goal;}
        bool safeHere=Clear(current);
        if(Vector2.DistanceSquared(current,stride.Goal)<.45f*.45f)stride.HeadingOut=false;
        if(IsCasting(role,now) && safeHere)
        {
            if(stride.HeadingOut)SkipRoam(role,now);
            stride.HeadingOut=false;stride.Goal=current;
        }
        if(CanRoam(role,now) && Vector2.DistanceSquared(current,stride.Goal)<.45f*.45f)
        {
            uint seed=Variation(role,++stride.Sequence);
            var best=current;float bestScore=float.NegativeInfinity;
            float leash=facing?5.85f:number?5.4f:melee?11.2f:10.5f;
            float length=facing?5.10f+(seed%690)/1000f:
                number?5.05f+(seed%2250)/1000f:8.10f+(seed%5250)/1000f;
            var own=Members[(int)role];
            // Linked actors coordinate their next safe lanes. The seven external actors in solo
            // mode keep their own bearings, so keep the solo lead-in inside its original range.
            if(personal)
            {
                leash=facing?1.75f:number?4.6f:melee?7.25f:6.8f;
                length=facing?1.15f+(seed%550)/1000f:number?2.35f+(seed%2000)/1000f:3.05f+(seed%3850)/1000f;
            }
            int directions=personal?12:24,passes=personal?1:3;
            float preferredLength=length;
            // Try a shorter legal chord before spending another whole idle window at the same foot position.
            for(int pass=0;pass<passes && float.IsNegativeInfinity(bestScore);pass++)
            {
                if(!personal)length=Math.Max(5.05f,preferredLength*(pass==0?1:pass==1?.82f:.66f));
                for(int i=0;i<directions;i++)
                {
                var q=current+Arena.Direction((i+(seed>>12)%(uint)directions)*MathF.Tau/directions+(seed%71)/100f)*length;
                if(!Clear(q) || Vector2.DistanceSquared(q,stride.Home)>leash*leash ||
                    Vector2.DistanceSquared(q,stride.Previous)<1.35f*1.35f || Vector2.DistanceSquared(q,stride.BeforePrevious)<1.15f*1.15f)continue;
                if(facing && (Vector2.DistanceSquared(current,origin)<16 || !personal && (Vector2.Distance(q,origin)<bossRadius*.6f || EdgeDistance(q)>Math.Max(EdgeDistance(current),1.60f)) ||
                    Vector2.Dot(Unit(q-origin,Vector2.UnitY),Unit(stride.Home-origin,Vector2.UnitY))<.989f))continue;
                float apart=3.5f;
                foreach(var actor in Members)
                    if(actor!=0 && actor!=own && PointOf(actor) is {} p)apart=Math.Min(apart,Vector2.Distance(q,p));
                float score=apart-.14f*Vector2.Distance(q,stride.Home)-.025f*i;
                if(melee)score-=.6f*Math.Max(0,EdgeDistance(q)-1.1f);
                if(number && jumped<8)
                {
                    var outwards=Unit(NumberPoint(jumped+1)-Center,Vector2.UnitY);
                    var d=q-Center;
                    if(Vector2.Dot(d,outwards)>.7f)score-=2*Math.Max(0,1.35f-Math.Abs(Vector2.Dot(d,Right(outwards))));
                    if(jumped>0)
                    {
                        var returning=Unit(lastLanding-Center,-outwards);
                        if(Vector2.Dot(d,returning)>.7f)score-=2*Math.Max(0,1.35f-Math.Abs(Vector2.Dot(d,Right(returning))));
                    }
                }
                if(score>bestScore){best=q;bestScore=score;}
                }
            }
            stride.BeforePrevious=stride.Previous;stride.Previous=current;stride.Goal=best;
            stride.HeadingOut=Vector2.DistanceSquared(current,best)>.45f*.45f;
            SkipRoam(role,now);
        }
        // Returning from the rim is mandatory. A caster may hold only once inside the room.
        if(Vector2.DistanceSquared(current,origin)>maxRadius*maxRadius)
            return goal with {Point=stride.Goal,Room=ArkveldRoom.None};
        return goal with {Point=stride.Goal,Discretionary=safeHere,Name=number?"麻将 · 中央五米内避让待命":melee?"输出空档 · 近战范围内走位":"输出空档 · 留在群疗范围"};
    }

    private bool CanRestJump(Role role,double now,Vector2 current,Destination activity,Zone[] danger)
        =>MobileJob(role) && !IsCasting(role,now) &&
            activity.Room==ArkveldRoom.Output && now>cueAt+1 && now+1.0<quietUntil &&
            Vector2.DistanceSquared(current,activity.Point)<.10f && (bossTarget==0?role!=Role.MT:Members[(int)role]!=bossTarget) &&
            (Variation(role,32)%5)==0 && !danger.Any(z=>z.Until>=now && z.From<=now+2.0 && (z.Roles & 1<<(int)role)!=0);
}

}
