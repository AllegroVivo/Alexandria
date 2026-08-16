using Alexandria.Battle;

namespace Alexandria.Battle.Tests;

public class EnemyBrainTests
{
    private static BattleUnit Mage(Int32 mp) => new()
    {
        Name = "Type C", Level = 13, MaxHP = 623, MaxMP = 447, CurrentHP = 623, CurrentMP = mp,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 10, PhysicalEvade = 3, MagicalDefense = 13, MagicalEvade = 4,
    };

    private static BattleUnit Hero() => new()
    {
        Name = "Hero", Level = 14, MaxHP = 400, MaxMP = 20, CurrentHP = 400, CurrentMP = 20,
        Speed = 16, Strength = 12, Magic = 6, Spirit = 14,
        PhysicalDefense = 6, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
    };
    
    private static BattleUnit Wolf() => new()
    {
        Name = "Bandersnatch", Level = 14, MaxHP = 899, MaxMP = 464, CurrentHP = 899, CurrentMP = 464,
        Speed = 21, Strength = 11, Magic = 11, Spirit = 16,
        PhysicalDefense = 10, PhysicalEvade = 3, MagicalDefense = 10, MagicalEvade = 4,
    };

    [Fact]
    public void The_shuffle_and_draw_follow_the_script()
    {
        // shuffle rolls (0,0,0): slot3←deck[0]=0, slot2←deck[0]=1, slot1←deck[0]=2,
        // remainder 3 → queue = [3,2,1,0]
        // draw roll 200 > 128 → pos 3 → head 3 rotates to back; card 3 = Thundara
        // target roll 0 → first living player; MP 447 affords cost 12
        var dice = new RiggedDice(0, 0, 0, 200, 0);
        var brain = EnemyBrains.TypeC(dice);

        var (attackId, target) = brain.Decide(Mage(447), [Hero()], dice);

        Assert.Equal(3, attackId);
        Assert.Equal("Hero", target.Name);
    }

    [Fact]
    public void A_broke_mage_walks_the_queue_to_strike()
    {
        // same shuffle → [3,2,1,0]; draw 200 → card 3, queue [2,1,0,3]
        // MP 5: Thundara 12 no → card 2 no → card 1 no → card 0 Strike, cost 0 — yes
        // one target roll per attempt: four zeros
        var dice = new RiggedDice(0, 0, 0, 200, 0, 0, 0, 0);
        var brain = EnemyBrains.TypeC(dice);

        var (attackId, _) = brain.Decide(Mage(5), [Hero()], dice);

        Assert.Equal(0, attackId);
    }
    
    [Fact]
    public void The_wolfs_deck_is_stacked_with_rush()
    {
        // shuffle (0,0,0) → queue [3,2,1,0]; draw 200 → pos 3, card 3 = Tongue
        // hero awake → awake-target roll 0 → hero; cost 0 affords
        var dice = new RiggedDice(0, 0, 0, 200, 0);
        var brain = EnemyBrains.Bandersnatch(dice);

        var (attackId, _) = brain.Decide(Wolf(), [Hero()], dice);

        Assert.Equal(6, attackId);   // Tongue, scene pool index 6
    }

    [Fact]
    public void Tongue_skips_a_sleeping_party()
    {
        // same shuffle and draw → card 3 Tongue; hero asleep → no valid target,
        // NO roll consumed → next card 2 = Thundara, plain-target roll 0 → hero
        var dice = new RiggedDice(0, 0, 0, 200, 0);
        var brain = EnemyBrains.Bandersnatch(dice);
        var hero = Hero();
        hero.Statuses = BattleStatus.Sleep;

        var (attackId, target) = brain.Decide(Wolf(), [hero], dice);

        Assert.Equal(5, attackId);   // Thundara — sleepers are still fair game for damage
        Assert.Equal("Hero", target.Name);
    }
}
