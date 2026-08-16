using System.Security.Cryptography;

namespace Alexandria.Battle;

public enum CardTarget
{
    RandomPlayer,
    RandomAwakePlayer,
    Self
}

/// <summary>
/// The vanilla shuffled-rotation AI shared by Type C, the Bandersnatch, and (in
/// grander form) Ozma - hand-ported from disassembled .eb atb functions. A deck of
/// cards is Fisher-Yates shuffled at battle start; each turn the draw jitters the
/// head card to a Random8-biased queue position, then the first affordable card
/// with a valid target acts. Self-target cards consume no target roll (faithful:
/// the script assigns SV_Target directly). Divergence: vanilla's RandomBit masks
/// include dead players (revalidated later by the engine); we roll among valid
/// candidates directly, using OperatorSelect's count×roll>>8 formula.
/// </summary>
public sealed class ShuffledDeckBrain : IEnemyBrain
{
    private readonly Int32[] _attackIDs;
    private readonly Int32[] _mpCosts;
    private readonly CardTarget[] _targets;
    private readonly Int32[] _queue;

    public ShuffledDeckBrain(Random rng, Int32[] attackIDs, Int32[] mpCosts, CardTarget[] targets)
    {
        _attackIDs = attackIDs;
        _mpCosts = mpCosts;
        _targets = targets;
        _queue = new Int32[attackIDs.Length];

        var deck = new List<Int32>(Enumerable.Range(0, _queue.Length));
        for (var slot = _queue.Length - 1; slot >= 1; slot--)
        {
            var pick = rng.Rand8() % (slot + 1);
            _queue[slot] = deck[pick];
            deck.RemoveAt(pick);
        }
        _queue[0] = deck[0];
    }

    public (Int32 AttackID, BattleUnit Target) Decide(BattleUnit self,
        IReadOnlyList<BattleUnit> players, Random rng)
    {
        var deckSize = _queue.Length;

        var roll = rng.Rand8();
        var mask = 128;
        var pos = deckSize - 1;
        while (pos >= 0 && roll <= mask)
        {
            mask >>= 1;
            pos--;
        }
        if (pos == -1)
            pos = deckSize - 1;

        var card = _queue[0];
        if (pos == deckSize - 1)
        {
            Array.Copy(_queue, 1, _queue, 0, deckSize - 1);
            _queue[deckSize - 1] = card;
        }
        else if (pos != 0)
        {
            Array.Copy(_queue, 1, _queue, 0, pos);
            _queue[pos] = card;
        }

        var tries = deckSize;
        while (true)
        {
            BattleUnit? target;
            if (_targets[card] == CardTarget.Self)
                target = self;
            else
            {
                var candidates = players
                    .Where(p => p.IsAlive
                                && (_targets[card] != CardTarget.RandomAwakePlayer || !p.Has(BattleStatus.Sleep)))
                    .ToList();
                target = candidates.Count > 0
                    ? candidates[(candidates.Count * rng.Rand8()) >> 8]
                    : null;
            }

            if ((target is not null && _mpCosts[card] <= self.CurrentMP) || tries <= 0)
                return (_attackIDs[card], target ?? self);

            card = _queue[0];
            var head = _queue[0];
            Array.Copy(_queue, 1, _queue, 0, deckSize - 1);
            _queue[deckSize - 1] = head;
            tries--;
        }
    }
}

