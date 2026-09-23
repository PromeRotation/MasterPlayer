namespace BossMod.Global.DeepDungeon;

public static class BadTraps
{
    // TODO: Bad traps should probably contain more information such as which deep dungeon and the level bracket, otherwise collisions with other traps on other floors seem likely...
    // or maybe a whole different approach to traps considering that it would take forever to identify all "bad" traps with over 28000 known trap locations
    // and combat AOEs complicate this matter further, often stepping into a potential trap location is preferable to other dangers
    public static readonly WPos[] BadTrapsArray = [new(-374.9f, 302.2f), new(258.5f, -229.3f), new(364.1f, -358.5f), new(-221.5f, 259.7f), new(383.9f, -341.9f), new(-366.5f, 316.1f)];
}
