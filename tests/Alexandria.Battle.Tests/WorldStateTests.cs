using Alexandria.Rules;

namespace Alexandria.Battle.Tests;

public class WorldStateTests
{
    private static String TempPath() => Path.Combine(Path.GetTempPath(), $"world-{Guid.NewGuid():N}.json");

    [Fact]
    public void A_missing_save_is_a_new_game_rather_than_an_error()
    {
        var world = WorldState.Load(Path.Combine(Path.GetTempPath(), "no-such-world.json"));

        Assert.Equal(0, world.Gil);
        Assert.Equal(0, world.Tonberries);
        Assert.Equal(0, world.Kills[EnemyCategory.Dragon]);
        Assert.Empty(world.Items.Contents);
    }

    [Fact]
    public void Everything_that_changes_survives_a_round_trip()
    {
        var path = TempPath();
        try
        {
            var world = new WorldState { Gil = 250_000, Frogs = 45, Thefts = 180 };
            world.Kills[EnemyCategory.Dragon] = 40;
            world.Kills[EnemyCategory.Humanoid] = 2;
            world.Items.Add(ItemID.Potion, 12);
            world.Items.Add(ItemID.Ore, 20);
            world.Save(path);

            var loaded = WorldState.Load(path);

            Assert.Equal(250_000, loaded.Gil);
            Assert.Equal(45, loaded.Frogs);
            Assert.Equal(180, loaded.Thefts);
            Assert.Equal(40, loaded.Kills[EnemyCategory.Dragon]);
            Assert.Equal(2, loaded.Kills[EnemyCategory.Humanoid]);
            Assert.Equal(12, loaded.Items[ItemID.Potion]);
            Assert.Equal(20, loaded.Items[ItemID.Ore]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Tonberries_ride_home_inside_the_blob_and_are_never_a_field_of_their_own()
    {
        // the album's lesson again: store only what changes, derive the rest
        var path = TempPath();
        try
        {
            var world = new WorldState { Tonberries = 7 };
            world.Scripts.Global[1584] = 1;              // Ozma's Friendly-Monsters bit
            world.Save(path);

            Assert.DoesNotContain("Tonberries", File.ReadAllText(path));

            var loaded = WorldState.Load(path);

            Assert.Equal(7, loaded.Tonberries);
            Assert.Equal(1, loaded.Scripts.Global[1584]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_scene_scratchpad_is_not_saved()
    {
        // EbState.Map is zeroed at every scene load, so persisting it would be a lie
        var path = TempPath();
        try
        {
            var world = new WorldState();
            world.Scripts.Map[0] = 99;
            world.Save(path);

            Assert.Equal(0, WorldState.Load(path).Scripts.Map[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void All_eight_categories_are_written_even_when_nothing_has_died()
    {
        // vanilla writes humanoid_no through flight_no unconditionally (JsonParser.cs:936-943)
        var path = TempPath();
        try
        {
            new WorldState().Save(path);
            var text = File.ReadAllText(path);

            Assert.All(Enum.GetValues<EnemyCategory>().Where(c => c != EnemyCategory.None),
                c => Assert.Contains(c.ToString(), text));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_hand_edited_tally_of_two_kinds_at_once_is_discarded_rather_than_throwing()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, """
                {
                  "Gil": 5,
                  "Kills": [ { "Category": "Dragon, Undead", "Count": 900 } ]
                }
                """);

            var loaded = WorldState.Load(path);

            Assert.Equal(5, loaded.Gil);
            Assert.Equal(0, loaded.Kills[EnemyCategory.Dragon]);
            Assert.Equal(0, loaded.Kills[EnemyCategory.Undead]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

