using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Matador "Guard Crush" — hold the shot for <see cref="ChargeSeconds"/> and the pellet released is a charged one: it
    /// carries <see cref="DamageMult"/>× the weapon's damage, passes an enemy's guard window with a hammer-swing kick behind it
    /// (an ordinary pellet carries none), and flies as a projection of
    /// the slingshot itself — a copy of the model, tinted orange, riding the pellet, wrapped in the cat's glow.
    ///
    /// The pellet is the game's, untouched: it flies, collides and plants its damage entry as any pellet does
    /// (step__5CSHOT), its own sprite hidden inside the model. Three things ride on it —
    ///  · the DAMAGE: the pool slot's damage word, scaled once at the bind, and made distinct so the guard bypass
    ///    (ElfCave.CatGuardBypass) can tell the entry apart: a Xiao-owned entry whose base damage equals
    ///    <see cref="Mailbox.PelletCrushDamage"/> passes the window and is stamped with the kick in
    ///    <see cref="Mailbox.PelletKickStrength"/>/<see cref="Mailbox.PelletKickDecay"/>;
    ///  · the MODEL: <see cref="SlingshotProp.SpawnProjectile"/> — the live weapon copied into chara slot 3, world-rooted,
    ///    at its own size, under the ambient add <see cref="Tint"/>. Built ONCE while the Matador is equipped and kept
    ///    resident at opacity 0 (the build is tens of milliseconds over PINE — the cat is kept the same way), so a shot only
    ///    places it on the pellet and shows it; from then on ElfCave.PropPelletFollow (Mailbox.PropFollowSlot/Lift) places it
    ///    every frame and reports the pellet's end, when the copy fades back to hidden;
    ///  · the GLOW: the Divine Beast cat's glow cave (ElfCave.CatGlowDraw) hung on the copy's root — the same `catglowp`
    ///    disc in the Fire element's ramp (the palette cave paints <see cref="FireRow"/>). The cat never coexists with the
    ///    Matador, and the row is handed back to None when the shot ends so the cat repaints on its next spawn.
    /// Nothing is drawn or crushed when the ISO lacks the caves.
    /// </summary>
    internal static class MatadorCharge
    {
        private const string Tag = "[Matador] ";

        private const double ChargeSeconds  = 0.5;    // hold this long → the shot is charged (the game's charge-complete flash marks it)
        private const double ArmSeconds     = 0.5;    // a charged release must produce its pellet within this
        private const float  DamageMult     = 1.5f;   // × the pellet's damage
        private const float  KickStrength   = 2.5f;   // the kick the bypass cave stamps on the charged pellet's entry (an ordinary pellet
        private const float  KickDecay      = 0.1f;   // has none): Goro's hammer swing, as SetKickBack takes them
        private const float  ModelScale     = 1.0f;   // the slingshot copy at the weapon's own size
        private const float  Lift           = 0f;     // the copy's height above the pellet
        private const float  Spin           = 0f;     // yaw per frame: none — it flies as it left
        private static readonly float[] Tint = { 120f, 45f, 0f };   // the copy's ambient add — orange
        private const float  Dim            = 0.6f;   // …over a dimmed base, so the add sets the hue
        private const int    FadeOutTicks   = 6;      // the copy after the pellet ends — also how long the crush/kick word stays up for the entry to be consumed
        // The glow: the cat's disc on the copy's centre node. Scale as the torch routine takes it (the cat uses 0.5 for its
        // torso), flags 2 = the flickering flame sprite.
        private const float  GlowScale      = 0.4f;
        private const int    GlowFlags      = 2;
        private const float  GlowPull       = 5f;
        private const float  GlowLift       = 0f;
        private const int    FireRow        = 1;      // the palette cave's ONE-based rows: 1 Fire … 6 None, 7–9 the weapon looks
        private const int    NoneRow        = 6;      // handed back when the shot ends
        private const string GlowDisc       = "catglowp";

        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static bool     _holding, _charged;
        private static DateTime _holdStart, _armedUntil = DateTime.MinValue;
        private static int      _slot = -1;           // the charged pellet while it flies
        private static int      _fade = -1;           // ticks into the copy's fade-out (−1 = not fading)
        private static bool     _nativeWarned;

        private static bool Native =>
            (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) == DunPatches.CatFollowHookNew
            && (uint)Memory.ReadInt(ElfPatches.GuardBypassHookAddrMmu) == (0x08000000u | (CodeCaves.ElfCave.CatGuardBypass >> 2));

        /// <summary>Drive every tick (16 ms) while the Matador is equipped; <paramref name="active"/> false HOLDS everything
        /// as it stands (pause, menu, chest, conversation — the pellet and the prop's slot stand still natively).
        /// <see cref="Stop"/> ends it when the weapon goes.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            if (!Native)
            {
                if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the pellet-follow / guard-bypass caves are not in this ISO — no charged shot (re-patch the ISO)"); }
                return;
            }

            // The hold: draw (0xB) / nocked hold (0xC) states; charged once it has lasted ChargeSeconds.
            int shotState = Memory.ReadInt(PlayerAction.ChargeActionState);
            bool holding = shotState == PlayerAction.XiaoShotDraw || shotState == PlayerAction.XiaoShotHold;
            if (holding)
            {
                if (!_holding) { _holding = true; _charged = false; _holdStart = GameClock.Now; }
                if (!_charged && (GameClock.Now - _holdStart).TotalSeconds >= ChargeSeconds) { _charged = true; Player.FlashChargeComplete(); }
            }
            else if (_holding)
            {
                _holding = false;
                if (_charged) _armedUntil = GameClock.Now.AddSeconds(ArmSeconds);   // the next new pellet is the charged one
                _charged = false;
            }

            // The copy: resident and hidden between shots (its Maintain also notices a weapon change and despawns; the
            // next tick rebuilds).
            if (!SlingshotProp.Active && !SlingshotProp.SpawnProjectile(ModelScale, 0f, Tint, Dim)) return;
            if (_slot < 0) SlingshotProp.Maintain(0f);

            // The pool: bind the first NEW pellet while armed; watch the bound one.
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i])
                {
                    _seen[i] = true;
                    if (GameClock.Now < _armedUntil)                                   // a new charged pellet takes over from one still out or fading
                    {
                        _armedUntil = DateTime.MinValue;
                        if (_slot >= 0) End();
                        Bind(pool, i);
                    }
                }
                else if (!live) _seen[i] = false;
            }
            if (_slot >= 0)
            {
                bool ended = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, _slot)) == 0 || Memory.ReadInt(CodeCaves.Mailbox.PropFollowEnded) != 0;
                // The pellet dies the frame it lands, but the enemy consumes its damage entry in its own CheckDmg — often the
                // next frame — so PelletCrushDamage stays up through the fade (End clears it), or the bypass and the kick miss.
                if (ended && _fade < 0) { _fade = 0; Memory.WriteInt(CodeCaves.Mailbox.CatGlowOn, 0); Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "charged pellet ended — the copy fades"); }
                if (_fade < 0) SlingshotProp.Maintain(1f);
                else if (++_fade <= FadeOutTicks) SlingshotProp.Maintain(1f - _fade / (float)FadeOutTicks);
                else End();                                                        // back to resident and hidden
            }
        }

        /// <summary>Hang the cat's glow on the copy's centre node: both anchors that node (the cave draws at their midpoint),
        /// the palette cave asked for the Fire row, the disc named, the bind cleared, then On LAST.</summary>
        private static void ShowGlow(uint node)
        {
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowPalRow, FireRow);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowScale, GlowScale);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowFlags, GlowFlags);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowPull, GlowPull);
            Memory.WriteFloat(CodeCaves.Mailbox.CatGlowLift, GlowLift);
            Memory.WriteUInt (CodeCaves.Mailbox.CatGlowNodeA, node);
            Memory.WriteUInt (CodeCaves.Mailbox.CatGlowNodeB, node);
            byte[] nm = new byte[16]; System.Text.Encoding.ASCII.GetBytes(GlowDisc).CopyTo(nm, 0);
            Memory.WriteBytesBatch(CodeCaves.Mailbox.CatGlowName, nm);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowReady, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.CatGlowOn, 1);
        }

        /// <summary>The weapon or the floor went: tear down whatever is out, the resident copy included.</summary>
        internal static void Stop()
        {
            if (_slot >= 0) End();
            if (SlingshotProp.Active) SlingshotProp.Despawn();
            _holding = false; _armedUntil = DateTime.MinValue;
        }

        private static void Bind(long pool, int slot)
        {
            long dmgA = PlayerShotPool.DamageAddr(pool, slot);
            int damage = Memory.ReadInt(dmgA), charged = (int)(damage * DamageMult);
            if (charged == damage) charged = damage + 1;                           // distinct from every ordinary pellet's
            long va = PlayerShotPool.VelAddr(pool, slot), pa = PlayerShotPool.PosAddr(pool, slot);
            float vx = Memory.ReadFloat(va), vz = Memory.ReadFloat(va + 8);
            float yaw = (float)Math.Atan2(vx, vz);
            if (!SlingshotProp.Active)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no slingshot copy — the charged pellet flies plain");
                return;
            }
            SlingshotProp.PlaceProjectile(Memory.ReadFloat(pa), Memory.ReadFloat(pa + 4) + Lift, Memory.ReadFloat(pa + 8), yaw);   // shown here, before the cave's first placement
            Memory.WriteInt  (dmgA, charged);
            Memory.WriteFloat(CodeCaves.Mailbox.PelletKickStrength, KickStrength);
            Memory.WriteFloat(CodeCaves.Mailbox.PelletKickDecay, KickDecay);
            Memory.WriteInt  (CodeCaves.Mailbox.PelletCrushDamage, charged);
            Memory.WriteFloat(CodeCaves.Mailbox.PropFollowLift, Lift);
            Memory.WriteFloat(CodeCaves.Mailbox.PropFollowSpin, Spin);
            Memory.WriteInt  (CodeCaves.Mailbox.PropFollowEnded, 0);
            Memory.WriteInt  (CodeCaves.Mailbox.PropFollowSlot, slot + 1);        // LAST: the cave places the copy from this frame
            _slot = slot; _fade = -1;
            SlingshotProp.Maintain(1f);
            ShowGlow(SlingshotProp.CentreGuest != 0 ? SlingshotProp.CentreGuest : SlingshotProp.RootGuest);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"charged shot: pellet slot {slot}, damage {damage} → {charged}, yaw {yaw:F2}");
        }

        private static void End()
        {
            Memory.WriteInt(CodeCaves.Mailbox.PropFollowSlot, 0);
            Memory.WriteInt(CodeCaves.Mailbox.PelletCrushDamage, 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowOn, 0);
            Memory.WriteInt(CodeCaves.Mailbox.CatGlowPalRow, NoneRow);          // the cave repaints the disc for whoever uses it next
            if (SlingshotProp.Active) SlingshotProp.Maintain(0f);                // hidden, resident for the next shot
            _slot = -1; _fade = -1;
        }
    }
}
