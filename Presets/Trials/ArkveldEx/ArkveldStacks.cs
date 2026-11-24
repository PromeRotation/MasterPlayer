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


internal sealed record StackCheck(double Time,uint Action,int[] Counts,Role[] Missing,Role[] Twice,bool Complete,bool Passed)
{
    public string Text
    {
        get
        {
            string detail=(Missing.Length==0?"":" · 漏分摊 "+string.Join("、",Missing))+
                (Twice.Length==0?"":" · 重复分摊 "+string.Join("、",Twice));
            return $"{TimeSpan.FromSeconds(Time):mm\\:ss\\.fff} {(Action==44812?"全队分摊":"双组分摊")} {string.Join("＋",Counts)} · {(Passed?"通过":Complete?"失败":"数据不全")}{detail}";
        }
    }
}

// Each light-party cast has two distinct helper actors. A packet retransmission is one hit.
internal sealed class ArkveldStacks
{
    public readonly List<StackCheck> Results=[];
    private readonly Dictionary<ulong,HashSet<ulong>?> groups=[];
    private readonly Dictionary<ulong,int> seatOrder=[];
    private uint action;
    private double due;
    private ulong[] roster=[];
    public void Expect(uint id,double at,ulong[] members)
    {
        if(action!=0 && (id!=action || Math.Abs(at-due)>1.2))Finish();
        action=id;due=at;roster=(ulong[])members.Clone();
    }
    public void Hit(Signal e,ulong[] members)
    {
        if(e.Id is not (43906 or 43907 or 44812))return;
        if(action!=0 && (e.Id!=action || Math.Abs(e.Time-due)>1.2))Finish();
        if(action==0 && Results.LastOrDefault() is {} last && last.Action==e.Id && Math.Abs(last.Time-e.Time)<.8)return;
        action=e.Id;due=e.Time;roster=(ulong[])members.Clone();
        var key=e.Source==0?e.Target:e.Source;
        groups[key]=e.Targets?.ToHashSet();
        int slot=Array.IndexOf(members,e.Target);
        if(slot>=0)seatOrder[key]=slot;
        if(groups.Count==(e.Id==44812?1:2))Finish();
    }
    public void Advance(double now){if(action!=0 && now>due+1.2)Finish();}
    private void Finish()
    {
        var valid=groups.Values.OfType<HashSet<ulong>>().ToArray();
        bool complete=roster.Length==8 && roster.All(id=>id!=0) && roster.Distinct().Count()==8 &&
            valid.Length==(action==44812?1:2);
        // Display the two marked actors in stable seat order, including 5+3 in that order.
        var ordered=groups.OrderBy(g=>seatOrder.GetValueOrDefault(g.Key,int.MaxValue)).ThenBy(g=>g.Key);
        var counts=ordered.Where(g=>g.Value!=null).Select(g=>g.Value!.Intersect(roster).Count()).ToArray();
        var missing=new List<Role>();var twice=new List<Role>();
        for(int i=0;i<roster.Length;i++)
        {
            int n=valid.Count(g=>g.Contains(roster[i]));
            if(n==0 && complete)missing.Add((Role)i);
            if(n>1)twice.Add((Role)i);
        }
        bool pass=complete && missing.Count==0 && twice.Count==0 &&
            (action==44812?counts[0]==8:counts.All(n=>n is >=3 and <=5));
        Results.Add(new(due,action,counts,missing.ToArray(),twice.ToArray(),complete,pass));
        groups.Clear();seatOrder.Clear();action=0;
    }
}

}
