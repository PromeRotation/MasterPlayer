using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private void TitanExBuildPhase(int phase)
    {
        switch (phase)
        {
            case 0:
                for (var i = 0; i < 3; ++i)
                {
                    TitanExAddCast("地裂", 1467, i == 0 ? 7.1f : 4.1f, 2.2f);
                    TitanExAddWeight(6.1f); TitanExAddBuster(3.6f); TitanExAddTumult(5.1f);
                    TitanExAddCast("地裂", 1467, 4.1f, 2.2f);
                    TitanExAddBuster(4.1f); TitanExAddWeight(4.1f); TitanExAddBuster(7.6f);
                }
                break;
            case 1: TitanExAddCrush(0, 15); break;
            case 2:
                TitanExAddCast("地裂", 1467, 7.1f, 2.2f);
                for (var i = 0; i < 4; ++i)
                {
                    TitanExAddGaols(4.1f, false);
                    TitanExAddTumult(6.2f); TitanExAddWeight(3.1f); TitanExAddBuster(3.6f);
                    TitanExAddCast("地裂", 1467, 5.1f, 2.2f); TitanExAddWeight(7.1f);
                    TitanExAddBuster(6.4f, enter: () => titanExMaxBombs = 0,
                        bombPattern: i % 2 == 1 ? 5 : 8);
                    TitanExAddCast("炸弹与地裂", 1467, 8.1f, 2.2f, endExit: () => titanExMaxBombs = 9);
                }
                break;
            case 3:
                TitanExAddCrush(0, 12);
                titanExSteps.Add(new("心核前消失", 2.1f, () => TitanExBoss?.Targetable == false));
                titanExSteps.Add(new("心核出现", .2f, () => TitanExHeart?.Targetable == true));
                break;
            case 4:
                TitanExAddWeight(7.5f); TitanExAddGaols(5.5f, true); TitanExAddTumult(6.1f);
                TitanExAddWeight(3.1f); TitanExAddBuster(8.6f, rock: true);
                TitanExAddCast("心核炸弹与地裂", 1467, 6.7f, 2.2f,
                    startEnter: () => titanExMaxBombs = 0, startExit: () => titanExMaxBombs = 5);
                TitanExAddTumult(4.2f); TitanExAddWeight(3.3f);
                titanExSteps.Add(new("心核结束", 9.5f, () => TitanExHeart?.Targetable == false));
                break;
            case 5:
                titanExSteps.Add(new("重现与全屏", 5.3f, () => TitanExBoss?.Targetable == true));
                break;
            case 6:
                for (var i = 0; i < 5; ++i)
                {
                    titanExSteps.Add(new("花岗岩狱卒", i == 0 ? 12.9f : 11.2f,
                        () => _actors.Values.Any(a => a.Oid == 0x8F2 && a.Targetable), MaxOverdue: 1));
                    TitanExAddBuster(3.6f);
                    TitanExAddCast("四加五炸弹地裂", 1467, 11.8f, 2.2f,
                        startEnter: () => titanExMaxBombs = 0, startExit: () => titanExMaxBombs = 4);
                    titanExSteps.Add(new("第一组四炸弹", 2.2f, () => TitanExBurstCount <= 5,
                        Exit: () => titanExMaxBombs = 9, MaxOverdue: 1));
                    TitanExAddBuster(3); TitanExAddWeight(6.1f); TitanExAddGaols(5.7f, false);
                    TitanExAddBuster(10.1f); TitanExAddTumult(4.1f); TitanExAddWeight(3.1f, true);
                    TitanExAddBuster(5.3f);
                    TitanExAddCast("三排炸弹地裂", 1467, 8.8f, 2.2f,
                        startEnter: () => titanExMaxBombs = 0, endExit: () => titanExMaxBombs = 6);
                    titanExSteps.Add(new("三排起爆", .2f, () => TitanExBurstCount > 6, MaxOverdue: 1));
                    titanExSteps.Add(new("第一排炸弹", 2, () => TitanExBurstCount <= 6,
                        Exit: () => titanExMaxBombs = 9, MaxOverdue: 1));
                    TitanExAddBuster(6);
                    titanExSteps.Add(new("击杀中央炸弹", 13.6f,
                        () => _actors.Values.Any(a => a.Oid == 0x5E0 && a.Targetable),
                        Exit: () => titanExKillBomb = true, MaxOverdue: 1));
                    TitanExAddTumult(4.2f); TitanExAddWeight(3.1f);
                    TitanExAddCrush(4.6f, 12);
                    TitanExAddCast("地裂", 1467, 3.1f, 2.2f);
                }
                break;
        }
    }

    private void TitanExAddCast(string name, uint action, float delay, float duration,
        Action? startEnter = null, Action? startExit = null, Action? endExit = null)
    {
        // Like ActorCastStart, an observed unexpected boss cast still advances this step.
        titanExSteps.Add(new(name + "起读", delay, () => TitanExCasting, startEnter, startExit));
        titanExSteps.Add(new(name + "结束", duration, () => !TitanExCasting, Exit: endExit));
    }

    private void TitanExAddWeight(float delay, bool twice = false)
    {
        TitanExAddCast("大地之重", 1469, delay, 2);
        titanExSteps.Add(new("大地之重落点结束", twice ? 3 : .5f, () => !_casts.Values.Any(c => c.Rule.Action == 1470),
            MaxOverdue: 1, CheckDelay: twice ? 1 : 0));
    }

    private void TitanExAddBuster(float delay, bool rock = false, Action? enter = null, Action? exit = null, int? bombPattern = null)
    {
        titanExSteps.Add(new(rock ? "岩石崩溃" : "山崩", delay, () => titanExBusterHits > 0,
            Enter: () =>
            {
                titanExBuster = rock ? 1463u : 1464u;
                titanExBusterHits = 0;
                titanExBusterAt = _now.AddSeconds(delay);
                enter?.Invoke();
            },
            Exit: () =>
            {
                titanExBuster = 0;
                if (bombPattern is { } count) titanExMaxBombs = count;
                else exit?.Invoke();
            }, MaxOverdue: rock ? 1 : 3));
    }

    private void TitanExAddTumult(float delay)
    {
        titanExSteps.Add(new("激震第一击", delay, () => titanExTumultHits > 0,
            Enter: () => { titanExTumult = true; titanExTumultHits = 0; titanExTumultAt = _now.AddSeconds(delay); },
            MaxOverdue: 10));
        for (var hit = 2; hit <= 4; ++hit)
        {
            var expectedHits = hit;
            titanExSteps.Add(new($"激震第{hit}击", 1.2f, () => titanExTumultHits >= expectedHits,
                Exit: hit == 4 ? () => titanExTumult = false : null, MaxOverdue: 1));
        }
    }

    private void TitanExAddGaols(float delay, bool heart)
    {
        titanExSteps.Add(new("牢狱点名", delay, () => titanExFetters.Count > 0, MaxOverdue: 1));
        TitanExAddBuster(heart ? 5.5f : 4.1f, rock: heart);
        TitanExAddCast("地动", 1466, heart ? 2 : 4.1f, 5);
        TitanExAddCast("牢狱后地裂", 1467, 2.8f, 2.2f);
    }

    private void TitanExAddCrush(float delay, float radius)
    {
        titanExSteps.Add(new("大地粉碎前消失", delay, () => TitanExBoss?.Targetable == false,
            Enter: () => titanExCrushRadius = radius));
        TitanExAddCast("大地粉碎", 1472, 3.3f, 3, endExit: () =>
        {
            titanExCrushRadius = 0;
            Arena = new CircleGeometry(new(0, TitanExBoss?.Position.Y ?? 0, 0), radius);
        });
        titanExSteps.Add(new("大地粉碎后重现", .4f, () => TitanExBoss?.Targetable == true));
    }
}
