namespace Alexandria.Rules;

public static class CharacterAbilities
{
    public static IReadOnlyList<LearnableAbility> Load(String path) =>
    [
        .. MemoriaCsv.ReadRows(path)
            .Select(f => new LearnableAbility(
                AbilityRef.Parse(f[0]),
                Int32.Parse(f[1])))
    ];
}

public sealed record LearnableAbility(AbilityRef Ability, Int32 APCost);
