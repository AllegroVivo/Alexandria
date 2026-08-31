using Alexandria.Battle;
using Alexandria.Formats;
using Alexandria.Formats.Scripts;

namespace Alexandria.Battle.Tests;

public class EbVmOracleTests
{
    // ---- piece 3: the world as the test provides it ----

    private sealed class BattleHost(Random rng, Int32 playersMask, Int32 selfMask, Int32 selfMp) : IEbHost
    {
        public Int32? AttackId;
        public Int32 Target;

        public Int32 Random8() => rng.Rand8();
        public Int32 Sysvar(Int32 id) => id == 0 ? Random8() : throw new NotSupportedException($"sysvar {id}");

        public Int32 SysList(Int32 slot) => slot switch
        {
            0 => Target,
            1 => selfMask,
            2 => playersMask,
            _ => throw new NotSupportedException($"SysList[{slot}]")
        };

        public void SetSysList(Int32 slot, Int32 value)
        {
            if (slot == 0) Target = value;
        }

        public Int32 ReadStat(Int32 mask, CharacterStat stat) => stat switch
        {
            CharacterStat.MP => selfMp,
            _ => throw new NotSupportedException($"stat {stat}")
        };

        public Int32 RandomBit(Int32 mask)
        {
            var bits = new List<Int32>();
            for (var i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0)
                    bits.Add(i);
            if (bits.Count == 0)
                return 0;
            return 1 << bits[(bits.Count * Random8()) >> 8];
        }

        public void Attack(Int32 attackId) => AttackId = attackId;
        public void BattleDialog(Int32 textId) => throw new NotSupportedException();
        public void WriteStat(Int32 mask, CharacterStat stat, Int32 value) => throw new NotSupportedException($"stat {stat}");
        public void BattleSet(Int32 verb, Int32 value) => throw new NotSupportedException();
        public void SetTextVariable(Int32 scriptId, Int32 value) => throw new NotSupportedException();
        public void AttackSpecial(Int32 attackId) => throw new NotSupportedException();
        public void Spawn(Int32 entry, Int32 uid) => throw new NotSupportedException();
        public void FieldOp(Int32 opcode, IReadOnlyList<Int32> args) => throw new NotSupportedException();
        public Int32 ReadObjectStat(Int32 uid, CharacterStat stat)
            => throw new NotSupportedException($"object {uid} property {stat} — teach me");
    }

    // ---- local builders (mirrors of the ones in EnemyBrainTests) ----

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

    private static EbVM TypeCVm(BattleHost host)
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript("EVT_BATTLE_AC_E028F")));
        return new EbVM(EbScript.Read(reader).Entries[1], host);
    }

    private static EbEntry BandersnatchEntry()
    {
        using var reader = new BinaryReader(File.OpenRead(FF9Data.SceneScript("EVT_BATTLE_AC_E028F")));
        return EbScript.Read(reader).Entries[2];
    }

    [Fact]
    public void The_vm_and_the_hand_port_pick_the_same_attack()
    {
        // dice: 3 shuffle rolls, 1 draw, 1 target — identical streams for both minds
        Int32[] rolls = [0, 0, 0, 200, 0];

        var brainDice = new RiggedDice(rolls);
        var brain = new ShuffledDeckBrain(brainDice,
            [0, 1, 2, 3], [0, 12, 12, 12],
            [CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer]);
        var (expectedAttack, _) = brain.Decide(Mage(447), [Hero()], brainDice);

        var host = new BattleHost(new RiggedDice(rolls), playersMask: 0b01, selfMask: 0b10, selfMp: 447);
        var vm = TypeCVm(host);
        vm.RunFunction(0); // init: consumes the 3 shuffle rolls
        vm.RunFunction(5); // atb: draw, target, act

        Assert.Equal(expectedAttack, host.AttackId); // both say Thundara (3)
        Assert.Equal(0b01, host.Target); // SV_Target = the lone player
    }

    [Fact]
    public void Both_minds_degrade_to_strike_when_broke()
    {
        // MP 5: the selection loop walks the queue to the free card in both worlds
        // dice: 3 shuffle, 1 draw, then one target roll per attempted card (4)
        Int32[] rolls = [0, 0, 0, 200, 0, 0, 0, 0];

        var brainDice = new RiggedDice(rolls);
        var brain = new ShuffledDeckBrain(brainDice,
            [0, 1, 2, 3], [0, 12, 12, 12],
            [CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer, CardTarget.RandomPlayer]);
        var (expectedAttack, _) = brain.Decide(Mage(5), [Hero()], brainDice);

        var host = new BattleHost(new RiggedDice(rolls), playersMask: 0b01, selfMask: 0b10, selfMp: 5);
        var vm = TypeCVm(host);
        vm.RunFunction(0);
        vm.RunFunction(5);

        Assert.Equal(expectedAttack, host.AttackId); // both say Strike (0)
    }

    [Fact]
    public void The_scripted_wolf_never_licks_a_sleeping_player()
    {
        // 3 shuffle rolls put Tongue at the head, 200 draws it, and the one target
        // roll happens over a mask the _E filters have already purged of sleepers
        Int32[] rolls = [0, 0, 0, 200, 0];
        var sleeper = Hero();
        sleeper.Statuses = BattleStatus.Sleep;
        var awake = Hero();
        var wolf = Mage(447);

        var brainDice = new RiggedDice(rolls);
        var (expectedAttack, expectedTarget) = EnemyBrains.Bandersnatch(brainDice)
            .Decide(wolf, [sleeper, awake], brainDice);

        var vmDice = new RiggedDice(rolls);
        var host = new EngineHost([sleeper, awake], [wolf], vmDice);
        var scripted = new ScriptedBrain(BandersnatchEntry(), host, selfLine: 4);
        var (attack, target) = scripted.Decide(wolf, [sleeper, awake], vmDice);

        Assert.Equal(expectedAttack, attack); // Tongue (6)
        Assert.Same(expectedTarget, target);
        Assert.Same(awake, target); // and never the sleeper
    }

    [Fact]
    public void Both_minds_walk_to_thundara_when_everyone_sleeps()
    {
        // Tongue's mask comes up empty; OperatorSelect spends NO roll on it, so the
        // fifth roll lands on Thundara's plain RandomBit — which happily targets
        // sleepers — in both worlds
        Int32[] rolls = [0, 0, 0, 200, 0];
        var sleeper1 = Hero();
        sleeper1.Statuses = BattleStatus.Sleep;
        var sleeper2 = Hero();
        sleeper2.Statuses = BattleStatus.Sleep;
        var wolf = Mage(447);

        var brainDice = new RiggedDice(rolls);
        var (expectedAttack, expectedTarget) = EnemyBrains.Bandersnatch(brainDice)
            .Decide(wolf, [sleeper1, sleeper2], brainDice);

        var vmDice = new RiggedDice(rolls);
        var host = new EngineHost([sleeper1, sleeper2], [wolf], vmDice);
        var scripted = new ScriptedBrain(BandersnatchEntry(), host, selfLine: 4);
        var (attack, target) = scripted.Decide(wolf, [sleeper1, sleeper2], vmDice);

        Assert.Equal(expectedAttack, attack); // Thundara (5)
        Assert.Same(expectedTarget, target);
        Assert.Same(sleeper1, target);
    }
}
