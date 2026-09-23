namespace BossMod.Stormblood.Alliance.A11Mateus;

class IceBubbleBlizzardIIITowers(BossModule module) : Components.GenericTowers(module)
{
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.AquaBubble)
            Towers.Add(new(actor.Position, 1f, 1, 1, default, WorldState.FutureTime(10d)));
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID == (uint)OID.BlizzardIIITowers)
        {
            if (state == 0x00040008)
                Towers.Add(new(actor.Position, 2f, 3, 3, default, WorldState.FutureTime(10d)));
            else if (state is 0x00010002 or 0x04000800)
                RemoveTower(actor.Position);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.DreadTide)
            RemoveTower(caster.Position);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.AquaBubble)
            RemoveTower(actor.Position);
    }

    private void RemoveTower(WPos position)
    {
        var count = Towers.Count;
        for (var i = 0; i < count; ++i)
        {
            var tower = Towers[i];
            if (tower.Position == position)
            {
                Towers.Remove(tower);
                break;
            }
        }
    }
}