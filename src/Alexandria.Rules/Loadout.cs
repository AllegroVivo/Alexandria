namespace Alexandria.Rules;

public class Loadout
{
    public static Int32[] Best(RulesDB db, String character, Byte threat = 0)
    {
        var slots = new Int32[5];
        for (var slot = 0; slot < slots.Length; slot++)
        {
            var pick = db.Items.Values
                .Where(i => i.Slot == (EquipSlot)slot
                            && i.Wearers is not null && i.Wearers.Contains(character))
                .OrderByDescending(i => Score(i, db, character, threat))
                .ThenByDescending(i => i.Price)
                .FirstOrDefault();
            slots[slot] = pick?.ID ?? -1;
        }
        return slots;
    }

    private static Int32 Score(Item item, RulesDB db, String character, Byte threat)
    {
        var score = 0;
        if (item.WeaponID >= 0 && db.Weapons.TryGetValue(item.WeaponID, out var w))
            score += w.Power * 4;
        if (item.ArmorID >= 0 && db.Armors.TryGetValue(item.ArmorID, out var armor))
            score += (armor.PhysicalDefense + armor.MagicalDefense) * 4 + armor.PhysicalEvade + armor.MagicalEvade;
        if (db.GrowthStats.TryGetValue(item.BonusID, out var s))
        {
            if ((s.GuardElement & threat) != 0) score += 500;
            if ((s.AbsorbElement & threat) != 0) score += 500;
            if ((s.HalfElement & threat) != 0) score += 250;
            if ((s.WeakElement & threat) != 0) score -= 500;
            score += s.Strength + s.Magic + s.Willpower + s.Dexterity;
        }

        var learnable = db.Learnables.GetValueOrDefault(character, [])
            .Select(a => a.Ability.ID).ToHashSet();
        score += 60 * item.TaughtAbilities.Count(a => learnable.Contains(a.ID));
        
        return score;
    }
}

