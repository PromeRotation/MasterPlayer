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



internal readonly record struct WalkAppointment(double Hit,double Latest);
internal readonly record struct ArkveldTiming(uint Action,string Name,double Hit,double? Latest,double? Arrived,bool Safe)
{
    internal static string Clock(double? t)=>t is {} s?TimeSpan.FromSeconds(s).ToString(@"mm\:ss\.ff"):"—";
    public string Text
    {
        get
        {
            string result=!Safe?"未到安全位":Arrived is {} at?$"余量 {Hit-at:F2}s"+(Hit-at<Arkveld.SafetyLead?" · 不足半秒":""):"";
            var departure=Latest.HasValue?$" · 最晚出发 {Clock(Latest)}":"";
            return $"{Name}{departure} · 模拟实际到位 {Clock(Arrived)} · 实录判定 {Clock(Hit)} · {result}";
        }
    }
}

internal sealed partial class Arkveld
{
    private readonly Dictionary<(ulong,uint),Signal> lastCasts=[];
    private uint dualAction;
    internal Signal LastCast(Signal hit)=>lastCasts.GetValueOrDefault((hit.Source,hit.Id));
    internal bool Available(ulong actor)=>actor!=0 && !unavailable.Contains(actor);
    internal int[] StackMarkers(Signal e,ulong[] roles)
    {
        if(e.Id==44812)
        {
            int index=Array.IndexOf(roles,singleActor);
            return index>=0?[index]:[];
        }
        return dual.Keys.Select(id=>Array.IndexOf(roles,id)).Where(i=>i>=0).ToArray();
    }

    private WalkPlan MarkAppointment(WalkPlan plan,Destination goal,Role role,double time,Vector2 current,float speed)
    {
        if(!engaged)return plan;
        double hit=double.PositiveInfinity;
        void Due(double t){if(t>=time && t<hit)hit=t;}
        foreach(var z in Zones)if((z.Roles & 1<<(int)role)!=0 && z.Snapshot is {} at)Due(at);
        if(Tower(role)!=null)Due(towerEnd);
        if(dualAction!=0)Due(dualEnd);
        if(waitingStack)Due(singleEnd);
        Due(spreadEnd);
        if(NumberActive(time) && Number(Members[(int)role]) is var n && n>jumped)Due(firstImpact+(n-1)*3.09);
        if(!double.IsFinite(hit) || hit-time>12)return plan;
        // Reverse the actual routed polyline. Fixed gate departures and sprint expiry
        // count, including a detour when the two groups are still crossing the arena.
        double latest=hit-PlanLead;
        int count=plan.Steps.Length;
        if(plan.Departures!=null)
            for(int i=1;i<count;i++)if(plan.Departures[i]>=hit-PlanLead-.04){count=i;break;}
        for(int i=count-1;i>=0;i--)
        {
            var from=i==0?current:plan.Steps[i-1];
            float distance=Vector2.Distance(from,plan.Steps[i]);
            double after=Math.Min(distance/Walking.Speed,Math.Max(0,latest-SprintUntil));
            latest-=after+(distance-after*Walking.Speed)/speed;
            if(plan.Departures!=null && plan.Departures[i]>latest+.02 && distance>.35f)
                return plan with {Appointment=new(hit,double.NaN)};
        }
        if(goal.Unavailable || !plan.Feasible)return plan with {Appointment=new(hit,double.NaN)};
        return plan with {Appointment=new(hit,latest)};
    }
}

// Retrospective only. Measure continuous safety from the simulated positions actually
// traversed; never use the recorded resolution to influence an earlier route.
internal sealed class ArkveldReview
{
    private readonly record struct Sample(double Time,Vector2[] Positions,int Present,WalkAppointment?[] Plans);
    private readonly Queue<Sample> history=[];
    public readonly ArkveldTiming?[] Last=new ArkveldTiming?[8];
    public void SampleAt(double time,Vector2[] positions,bool[] visible,ulong[] roles,Arkveld dragon,WalkPlan?[] plans)
    {
        while(history.TryPeek(out var s) && time-s.Time>12)history.Dequeue();
        int mask=0;for(int i=0;i<8;i++)if(visible[i] && dragon.Available(roles[i]))mask|=1<<i;
        history.Enqueue(new(time,(Vector2[])positions.Clone(),mask,plans.Select(p=>p?.Appointment).ToArray()));
    }
    public void Resolve(Signal e,Arkveld dragon,ulong[] roles,int controlled,Action<int,ArkveldTiming>? observe=null)
    {
        if(e.Kind!=Input.Hit || history.Count==0)return;
        var cast=dragon.LastCast(e);
        if(cast.Id!=0 && e.Time-cast.Time>Math.Max(2,cast.Duration+1.5))cast=default;
        var site=e.Point;
        if(Vector2.DistanceSquared(site,Arena.Center)>35*35)
            site=cast.Id!=0?cast.Point:e.SourcePoint??site;
        bool tower=e.Id is 43920 or 43921, spread=e.Id==43932, stack=e.Id is 43906 or 43907 or 44812;
        bool jump=e.Id is 43956 or 43959;
        bool fire=e.Id is 43926 or 43927 or 43952 or 43953,crystal=e.Id is 44809 or 44810;
        bool puddle=e.Id is 43923 or 43942 or 43933;
        // Player-directed puddles, beams and cleaves use the cast that was rebased to the
        // controlled role's actual simulated bait at lock-on.
        if(puddle && cast.Id!=0)site=cast.Point;
        (string Text,Shape Shape,float Length,float Width) area=e.Id switch {
            43891 or 43892 or 45085 or 45088 or 43897 or 43898 or 45093 or 45096=>("敲打",Shape.Line,80,14),
            43889 or 43890 or 43895 or 43896 or >=45077 and <=45080 or 45083 or 45084 or 45086 or 45087 or 45091 or 45092 or 45094 or 45095=>("敲打细线",Shape.Line,40,2),
            43900 or 43903 or 45099 or 45101=>("飞翔",Shape.Line,40,4),
            43901 or 45100=>("飞翔",Shape.Line,40,8),
            43904 or 43905 or 45102 or 45103=>("飞翔",Shape.Line,40,9),
            43916 or 43918 or 45107 or 45108=>("回旋锁刃",Shape.Cone,40,MathF.PI/2),
            43957 or 43960=>("麻将半场",Shape.Cone,60,MathF.PI/2),
            43937 or 43938=>("龙闪炮",Shape.Line,60,3),
            43945 or 43946=>("交错直线",Shape.Line,40,2),
            43940 or 43941=>("移动直线",Shape.Line,8,20),
            43949 or 44805 or 44806 or 45109=>("尾刺",Shape.Line,60,3),
            43911=>("环形爆炸",Shape.Circle,8,0),
            >=43912 and <=43914=>("环形爆炸",Shape.Ring,8+6*(e.Id-43911),8+6*(e.Id-43912)),
            _=>("",default,0,0)
        };
        bool hasArea=area.Text!="" && cast.Id!=0;
        if(!tower && !spread && !stack && !jump && !hasArea && !fire && !crystal && !puddle)return;
        if((fire || crystal || puddle || tower) && Vector2.DistanceSquared(site,Arena.Center)>35*35)return;
        float radius=e.Id is 44810 or 43933?12:tower?(e.Id==43920?2:4):6;
        Zone shape=new(area.Shape,cast.Point,cast.Angle,area.Length,area.Width,0,0);
        var frames=history.ToArray();
        string name=tower?"六塔":spread?"分散":stack?"分摊":jump?"麻将":hasArea?area.Text:fire?"地火":crystal?"结晶":"落地圈";
        int owner=Array.IndexOf(roles,e.Target);
        int[] marks=stack?dragon.StackMarkers(e,roles):[];
        for(int i=0;i<8;i++)
        {
            if(controlled>=0 && i!=controlled || (frames[^1].Present & 1<<i)==0)continue;
            if(tower && (dragon.Tower((Role)i) is not {} t || Vector2.DistanceSquared(t,site)>.09f))continue;
            if(spread && owner<0 || jump && dragon.JumpActor!=roles[i] || stack && marks.Length==0)continue;
            bool Safe(Sample f)
            {
                var p=f.Positions[i];
                if(hasArea)return !shape.Contains(p);
                if(tower)return Vector2.DistanceSquared(p,site)<=radius*radius;
                if(stack)return marks.Count(j=>(f.Present & 1<<j)!=0 && Vector2.DistanceSquared(p,f.Positions[j])<=36)==1;
                if(spread || jump)
                {
                    if(spread && owner!=i)return Vector2.DistanceSquared(p,f.Positions[owner])>36;
                    if(jump && Vector2.DistanceSquared(p,dragon.CurrentBaitPoint(dragon.Jumps+1))>1)return false;
                    for(int j=0;j<8;j++)if(j!=i && (f.Present & 1<<j)!=0 && Vector2.DistanceSquared(p,f.Positions[j])<=36)return false;
                    return true;
                }
                return Vector2.DistanceSquared(p,site)>radius*radius;
            }
            bool safe=Safe(frames[^1]);double? arrived=null,latest=null;
            if(safe)
                for(int j=frames.Length-1;j>=0 && (frames[j].Present & 1<<i)!=0 && Safe(frames[j]);j--)arrived=frames[j].Time;
            foreach(var f in frames)
                if(f.Plans[i] is {} a && Math.Abs(a.Hit-e.Time)<.35 && double.IsFinite(a.Latest))
                {latest=a.Latest;break;}
            var row=new ArkveldTiming(e.Id,name,e.Time,latest,arrived,safe);
            observe?.Invoke(i,row);
            // Concurrent AoEs must display the limiting commitment, not whichever helper was last.
            if(Last[i] is {} prior && e.Time-prior.Hit<.12 && (!prior.Safe || safe && prior.Hit-prior.Arrived<=e.Time-arrived))continue;
            Last[i]=row;
        }
    }
}

}
