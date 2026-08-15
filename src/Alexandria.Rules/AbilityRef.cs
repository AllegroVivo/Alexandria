namespace Alexandria.Rules;

public enum AbilityKind
{
    None,
    Active,
    Support
}

public readonly record struct AbilityRef(AbilityKind Kind, Int32 ID)
{
    public static AbilityRef Parse(String text) => text switch
    {
        "0" => new AbilityRef(AbilityKind.None, 0),
        _ when text.StartsWith("AA:")
            => new AbilityRef(AbilityKind.Active, Int32.Parse(text[3..])),
        _ when text.StartsWith("SA:")
            => new AbilityRef(AbilityKind.Support, Int32.Parse(text[3..])),
        _ => throw new FormatException($"Unrecognized ability reference: '{text}'")
    };
}
