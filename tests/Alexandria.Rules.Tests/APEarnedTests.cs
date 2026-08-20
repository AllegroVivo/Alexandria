using Alexandria.Formats;
using Alexandria.Rules;

namespace Alexandria.Rules.Tests;

public class ApEarnedTests
{
    private static readonly RulesDB Db = RulesDB.Load(FF9Data.Data);

    [Fact]
    public void A_level_fourteen_vivi_knows_the_burmecia_loadout()
    {
        // budget 168, cheapest first: Sleep 20, Slow 20, Fire 25, Blizzard 25,
        // Thunder 25, Stop 25 = 140 spent; Demi's 30 > the remaining 28
        var learned = APEarned.LearnedActives(Db.Learnables["Vivi"], 14);

        Assert.Equal(6, learned.Count);
        Assert.Contains(28, learned);        // Sleep
        Assert.Contains(33, learned);        // Thunder
        Assert.DoesNotContain(26, learned);  // Fira stays out of reach
    }

    [Fact]
    public void Doomsday_is_never_learned_by_leveling_alone()
    {
        var learned = APEarned.LearnedActives(Db.Learnables["Vivi"], 99);

        Assert.Contains(47, learned);        // Flare makes it in at 99
        Assert.DoesNotContain(48, learned);  // Doomsday demands grinding
    }

    [Fact]
    public void The_spellbook_union_restores_vivis_arsenal()
    {
        // gear teaches Fire; the earned set adds the rest — the learned: hook at work
        var book = Spellbook.For(Db, "Vivi",
            APEarned.LearnedActives(Db.Learnables["Vivi"], 14));

        Assert.Contains(book, s => s.Name == "Sleep");
        Assert.Contains(book, s => s.Name == "Blizzard");
        Assert.DoesNotContain(book, s => s.Name == "Doomsday");
    }
}
