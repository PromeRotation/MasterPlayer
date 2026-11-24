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



internal abstract class WalkingEncounter
{
    public readonly ulong[] Members = new ulong[8];
    public readonly List<Zone> Zones = [];
    protected readonly Dictionary<ulong,Vector2> positions = [];
    private readonly HashSet<ulong> meleeActors = [];
    public string Mechanic { get; protected set; } = "开场集合";
    public virtual void SetJob(ulong actor,uint job)
    {
        if(actor is 0 or 0xE0000000)return;
        if(CombatJobs.IsMelee(job))meleeActors.Add(actor);
        else meleeActors.Remove(actor);
    }
    public bool IsMelee(Role role)=>meleeActors.Contains(Members[(int)role]);
    public void Position(ulong actor,Vector2 point)
    {
        if(actor!=0 && float.IsFinite(point.X+point.Y))positions[actor]=point;
    }
    public virtual void ObservePosition(ulong actor,Vector2 point,double time)=>Position(actor,point);
    public void Prune(double time)=>Zones.RemoveAll(z=>z.Until<time-.5);
    public Action<Signal>? Recorder {get;set;}
    public void Submit(Signal e){Recorder?.Invoke(e);Feed(e);}
    public abstract void Feed(Signal e);
    public abstract Destination Goal(Role role,double time,Vector2 current);
    public virtual WalkPlan Plan(Role role,double time,Vector2 current,float speed)=>
        Walking.Solve(current,role,time,Goal(role,time,current),Zones,speed);
    public abstract bool WantsSprint(double time);
}

}
