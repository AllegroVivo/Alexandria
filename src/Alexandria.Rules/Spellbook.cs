namespace Alexandria.Rules;

/// <summary>Vanilla battle gate (mirror: BattleHUD.GetAbilityState): an active ability is
/// castable iff taught by currently equipped gear OR fully learned (AP paid off).
/// A freshly built character has learned nothing, so gear defines the starting book.</summary>
public static class Spellbook
{
    public static IReadOnlyList<BattleAction> For(RulesDB db, String name, IReadOnlySet<Int32>? learned = null)
    {
        var kit = EquipmentKit.Resolve(db.DefaultEquipment.First(e => e.Name == name),
            db.Items, db.Weapons, db.Armors, db.GrowthStats);
        var usable = kit.TaughtAbilities
            .Where(a => a.Kind == AbilityKind.Active)
            .Select(a => a.ID)
            .ToHashSet();
        if (learned is not null)
            usable.UnionWith(learned);

        return
        [
            .. db.Learnables.GetValueOrDefault(name, [])
                .Where(a => a.Ability.Kind == AbilityKind.Active && usable.Contains(a.Ability.ID))
                .Select(a => db.Actions[a.Ability.ID])
        ];
    }
}

