using Alexandria.Battle;
using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class BattleEngineTests
{
    private static BattleUnit Unit(String name, Int32 speed, Int32 hp,
        Int32 str = 20, Int32 attackPower = 12) => new()
    {
        Name = name, Level = 5, MaxHP = hp, MaxMP = 0, CurrentHP = hp, CurrentMP = 0,
        Speed = speed, Strength = str, Magic = 10, Spirit = 10,
        PhysicalDefense = 0, PhysicalEvade = 0, MagicalDefense = 0, MagicalEvade = 0,
        AttackPower = attackPower,
    };

    [Fact]
    public void Turn_order_emerges_from_the_clock()
    {
        // Rigged initial rolls put both gauges at 0; Speed 50 (gauge 400) fills in
        // 40 ticks, Speed 10 (gauge 2000) needs 200 — the fast unit must act first.
        var fast = Unit("Fast", speed: 50, hp: 100);
        var slow = Unit("Slow", speed: 10, hp: 100);
        var engine = new BattleEngine([fast], [slow], EncounterKind.Normal, new RiggedDice(0, 0));

        Assert.Same(fast, engine.TickUntilReady());
    }

    [Fact]
    public void Preemptive_strike_hands_players_the_opening_turn()
    {
        var player = Unit("Hero", 20, 100);
        var enemy = Unit("Goblin", 45, 100);   // faster, but preemptive zeroes it
        var engine = new BattleEngine([player], [enemy], EncounterKind.Preemptive, new Random(7));

        Assert.Same(player, engine.TickUntilReady());
    }

    [Fact]
    public void Acting_resets_the_gauge()
    {
        var player = Unit("Hero", 20, 100);
        var enemy = Unit("Goblin", 20, 100);
        var engine = new BattleEngine([player], [enemy], EncounterKind.Preemptive, new Random(7));

        var ready = engine.TickUntilReady()!;
        engine.ExecuteAttack(ready, enemy);
        Assert.Equal(0, ready.ATBGauge);
    }

    [Fact]
    public void A_full_battle_reaches_a_verdict()
    {
        var hero = Unit("Hero", speed: 40, hp: 500, str: 40, attackPower: 30);
        var goblin = Unit("Goblin", speed: 10, hp: 60, str: 5, attackPower: 3);
        var engine = new BattleEngine([hero], [goblin], EncounterKind.Normal, new Random(42));

        for (var safety = 0; safety < 10_000 && !engine.IsOver; safety++)
        {
            var ready = engine.TickUntilReady();
            if (ready is null)
                break;
            var target = engine.IsEnemy(ready) ? engine.Players[0] : engine.Enemies[0];
            engine.ExecuteAttack(ready, target);
        }

        Assert.True(engine.Victory);
        Assert.False(engine.Defeat);
    }
}