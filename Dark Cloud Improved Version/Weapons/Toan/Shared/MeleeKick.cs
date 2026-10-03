using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The knockback of Toan's melee hits, set as a whole: one strength into every hit's word (the shared 1.2
    /// and the five made data by the ISO patch) and one decay into the three decay words, so every swing, the lunge
    /// and the whirlwind throw an enemy the same ≈ strength² / (2·decay) units (MeleeKickWords). Global ELF words —
    /// they are whoever is swinging's — so a sword holds them only while it is in hand and <see cref="Restore"/>s them
    /// on the way out; the owner word keeps the pnach from re-seeding the five underneath it.</summary>
    internal static class MeleeKick
    {
        private static float _strength = float.NaN, _decay = float.NaN;

        /// <summary>Every hit kicks at <paramref name="strength"/>, fading <paramref name="decay"/> a frame. Written only
        /// when it changes, or when a word is found not to hold it (a rebuild put the vanilla figure back).</summary>
        internal static void Set(float strength, float decay)
        {
            if (strength == _strength && decay == _decay && Memory.ReadFloat(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickHit5) == strength
                && Memory.ReadFloat(MeleeKickWords.Decay35) == decay) return;
            Memory.WriteInt(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickOwner, 1);      // ours before the words: the pnach stops re-seeding
            foreach (int o in new[] { CodeCaves.MeleeKickHit3, CodeCaves.MeleeKickHit4, CodeCaves.MeleeKickHit5, CodeCaves.MeleeKickLunge, CodeCaves.MeleeKickWhirl })
                Memory.WriteFloat(CodeCaves.MeleeKickWords + o, strength);
            Memory.WriteFloat(MeleeKickWords.Strength12, strength);
            Memory.WriteFloat(MeleeKickWords.Decay12, decay);
            Memory.WriteFloat(MeleeKickWords.Decay35, decay);
            Memory.WriteFloat(MeleeKickWords.Decay4, decay);
            _strength = strength; _decay = decay;
        }

        /// <summary>The vanilla figures back in every word, and the five handed back to the pnach's seeding.</summary>
        internal static void Restore()
        {
            if (float.IsNaN(_strength)) return;
            Memory.WriteFloat(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickHit3,  MeleeKickWords.VanillaHit3);
            Memory.WriteFloat(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickHit4,  MeleeKickWords.VanillaHit4);
            Memory.WriteFloat(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickHit5,  MeleeKickWords.VanillaHit5);
            Memory.WriteFloat(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickLunge, MeleeKickWords.VanillaCharge);
            Memory.WriteFloat(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickWhirl, MeleeKickWords.VanillaCharge);
            Memory.WriteFloat(MeleeKickWords.Strength12, MeleeKickWords.VanillaStrength12);
            Memory.WriteFloat(MeleeKickWords.Decay12, MeleeKickWords.VanillaDecay12);
            Memory.WriteFloat(MeleeKickWords.Decay35, MeleeKickWords.VanillaDecay35);
            Memory.WriteFloat(MeleeKickWords.Decay4,  MeleeKickWords.VanillaDecay4);
            Memory.WriteInt(CodeCaves.MeleeKickWords + CodeCaves.MeleeKickOwner, 0);
            _strength = _decay = float.NaN;
        }
    }
}
