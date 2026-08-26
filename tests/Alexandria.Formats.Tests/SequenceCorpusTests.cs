using Alexandria.Formats.Scripts;

namespace Alexandria.Formats.Tests;

/// <summary>The sequence layer is deliberately implemented only where it changes battle state
/// (debt #3). These tests pin the three facts that decision rests on, so a new dump or a modded
/// scene that breaks any of them fails here rather than silently changing behaviour.</summary>
public class SequenceCorpusTests
{
    /// <summary>Read and parsed ONCE. Three tests over 3,525 files is 10,575 reads otherwise,
    /// which took the Formats suite from 455 ms to 5 s.</summary>
    private static readonly (String Name, AnimationSequenceScript Script)[] Corpus =
    [
        .. Directory.EnumerateFiles(FF9Data.Scenes, "*.seq", SearchOption.AllDirectories)
            .Select(f => (Path.GetFileNameWithoutExtension(f), AnimationSequenceScript.Read(f)))
    ];

    /// <summary>Every opcode Memoria's exporter emits. Exactly one of these — ToggleStandAnimation —
    /// alters state a headless engine can observe; EffectPoint marks WHEN damage lands, and the
    /// rest are animation, camera, sound, movement and mesh.</summary>
    private static readonly HashSet<String> Known =
    [
        "ActivateReflect", "ChangeCharacterProperty", "ChangeSize", "Channel", "EffectPoint",
        "EndThread", "LoadMonsterSFX", "LoadSFX", "Message", "MoveToPosition", "MoveToTarget",
        "PlayAnimation", "PlayCamera", "PlayMonsterSFX", "PlaySFX", "PlaySound",
        "PlayTextureAnimation", "SetupReflect", "ShowMesh", "ShowShadow", "StartThread",
        "ToggleStandAnimation", "Turn", "Wait", "WaitAnimation", "WaitMonsterSFXDone",
        "WaitMonsterSFXLoaded", "WaitMove", "WaitReflect", "WaitSFXDone", "WaitSFXLoaded",
        "WaitSize", "WaitTurn"
    ];

    [Fact]
    public void The_corpus_uses_no_opcode_we_have_not_accounted_for()
    {
        Assert.Equal(3525, Corpus.Length);

        var seen = Corpus
            .SelectMany(c => c.Script.Instructions)
            .Select(i => i.Opcode)
            .ToHashSet();

        Assert.Empty(seen.Except(Known));
        Assert.Empty(Known.Except(seen));   // and nothing in the list has gone away
    }

    [Fact]
    public void Nothing_that_changes_state_is_hidden_inside_a_thread()
    {
        // THIS is what makes flattening safe. Our reader drops the thread structure, so a
        // ToggleStandAnimation nested in one would be read as if it ran in the main flow.
        var nested = Corpus
            .SelectMany(c => c.Script.Instructions
                .Where(i => i.InThread && i.Opcode is "ToggleStandAnimation" or "EffectPoint")
                .Select(i => $"{c.Name}: {i.Opcode}"))
            .ToList();

        Assert.Empty(nested);
    }

    [Fact]
    public void Only_one_attack_in_the_game_lands_its_effect_twice()
    {
        // Damage is applied synchronously in Resolve, which is identical to vanilla for a
        // sequence with one effect point or none. Tantarian's Close Book is the sole exception,
        // and its two points bracket the ToggleStandAnimation that flips his form.
        var multi = Corpus
            .Where(c => c.Script.Instructions.Count(i => i.Opcode == "EffectPoint") > 1)
            .Select(c => c.Name)
            .ToList();

        Assert.Equal(["04 Close Book", "08 Close Book"], multi.Order());
    }
}
