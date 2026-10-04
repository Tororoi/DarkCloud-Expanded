namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Divine Beast cat's runtime block (guest 0x01FB4000.., the spare span past the ELF cave band; BobberPtr uses its
    /// first four bytes): the words the cat caves and the mod exchange, all at CatBase + offset.</summary>
    internal static class CatBlock
    {
        /// <summary>Guest 0x01FB4000: the spare span past the ELF cave band (BobberPtr uses its first four bytes; the cat's
        /// words run from +0x94 to +0x2A8). The page ends at +0x300. Runtime data, kept off the segment's pages
        /// (page isolation, docs/code-caves.md).</summary>
        internal const long CatBase = 0x21FB4000;
        /// <summary>Divine Beast cat ↔ the native pellet catcher/follower (ElfCave.CatPelletFollow; DivineBeastTitle.cs).
        /// The cat copy sits resident and hidden in chara slot 1. At the charge threshold the mod writes the growth
        /// reciprocal and the head rest offset (cat space × cat scale), zeroes frames/slot, and sets state 3 (waiting);
        /// the cave then binds the next NEW pellet on its birth frame (slot+1, state 1, opacity 128) and owns slot 1's
        /// position and scale every frame until that pellet ends (slot 0, state 2 → the mod fades and re-hides).</summary>
        internal const long CatPelletSlot  = CatBase + 0x94;   // int, bound pellet slot + 1; 0 = none (page boots zero-filled)
        internal const long CatState       = CatBase + 0x98;   // int: 0 idle, 1 following, 2 pellet ended, 3 waiting for a new pellet
        internal const long CatGrowFrames  = CatBase + 0x9C;   // int, frames since bound (cave increments)
        internal const long CatGrowInv     = CatBase + 0xA0;   // float, 1 / growth frames
        internal const long CatHeadX       = CatBase + 0xA4;   // float ×3: head rest offset in CAT space (x, height, z)
        internal const long CatHeadH       = CatBase + 0xA8;
        internal const long CatHeadZ       = CatBase + 0xAC;
        internal const long CatSeenMask    = CatBase + 0xB0;   // int, active pellet-slot bits last frame (cave)
        // States 4 (falling) / 5 (landed) / 6 (running): at full size the cave breaks the cat away from the pellet
        // (expiring it), falls it with the pellet's forward speed, snaps it to CatFloorH on the landing frame and,
        // once the mod sets state 6, runs it at ½ the pellet speed toward CatTargetPtr (or straight).
        internal const long CatVx          = CatBase + 0xB4;   // float ×3: fall velocity, captured at the breakaway (cave)
        internal const long CatVh          = CatBase + 0xB8;
        internal const long CatVz          = CatBase + 0xBC;
        internal const long CatGravity     = CatBase + 0xC0;   // float, units/frame² (mod)
        internal const long CatFloorH      = CatBase + 0xC4;   // float, landing height (mod)
        internal const long CatRunSpeed    = CatBase + 0xC8;   // float, ½·|pellet horizontal speed| (cave)
        internal const long CatTargetPtr   = CatBase + 0xCC;   // uint, guest address of the target's position vector, 0 = none (mod)
        internal const long CatDirX        = CatBase + 0xD0;   // float ×2: unit run direction (cave; the mod faces the cat along it)
        internal const long CatDirZ        = CatBase + 0xD4;
        internal const long CatGrowN       = CatBase + 0xD8;   // int, growth frames (mod)
        // State 5 (landing): the land clip plays straight through; the cave reads the copy's live motion frame (slot 1
        // +0xC20 → MOTION_TYPE +0x10) and keeps the fall's forward momentum until the paws-touch frame, then runs the
        // moment the clip reaches its end (or wraps).
        internal const long CatLandStopFrame = CatBase + 0xDC; // float, clip frame where the paws touch — momentum stops (mod)
        internal const long CatLandEndFrame  = CatBase + 0xE0; // float, clip end frame — straight into the run (mod)
        internal const long CatPrevFrame     = CatBase + 0xE4; // float, motion frame seen last time (cave; wrap detection)
        internal const long CatLandLead      = CatBase + 0xE8; // float, frames before the predicted touchdown at which the land clip starts (mod)
        internal const long CatMoveKey       = CatBase + 0xEC; // int, motion key played while moving after the landing (mod: the brisk walk)
        internal const long CatMoveFrac      = CatBase + 0xF0; // float, ground speed after the landing as a fraction of the pellet's speed (mod)
        internal const long CatProbeUp       = CatBase + 0xF4; // float, floor probe reach above the cat (mod)
        internal const long CatProbeDown     = CatBase + 0xF8; // float, floor probe reach below the cat (mod)
        internal const long CatProbeFront    = CatBase + 0xFC; // float, extra floor cast this far AHEAD of the root along its direction (mod)
        internal const long CatProbeBack     = CatBase + 0x100; // float, … and this far BEHIND; the cat stands on the highest of the three casts
        // Clip rate while moving: the cave writes slot +0xC60 (motion-speed override) = min(base + perSpeed · ground
        // speed, max) — the town's own walk mapping for this rig (EdMoveChara: 0.8·(0.2 + stick) capped 0.85 with
        // ground 1.6·stick → 0.16 + 0.5·ground). Not planted feet; the ratio the designers tuned. A forward wall
        // probe stops the cat (CatBlocked = 1, idle key).
        internal const long CatRateBase      = CatBase + 0x104; // float, clip rate at zero speed (mod: 0.16)
        internal const long CatRatePerSpeed  = CatBase + 0x108; // float, clip rate per unit of ground speed (mod: 0.5)
        internal const long CatRateMax       = CatBase + 0x10C; // float, clip rate cap (mod: 0.85)
        internal const long CatIdleKey       = CatBase + 0x114; // int, stand key (mod)
        internal const long CatBlocked       = CatBase + 0x118; // int, 1 while a wall stops the cat (cave)
        // Pounce + hit: within CatPounceRange of its target the cave plays the take-off, then leaps an arc of
        // CatPounceFrames at the target's live position and lands as usual. While riding, falling or landing it
        // tests its root and a point ahead against every enemy's body spheres (the pellet code's own table);
        // a touch freezes it (state 9) and names the enemy in CatHitSlot for the mod to deal the damage.
        internal const long CatPounceRange   = CatBase + 0x11C; // float (mod)
        internal const long CatPounceFrames  = CatBase + 0x120; // float, leap flight frames (mod)
        internal const long CatTakeoffEnd    = CatBase + 0x124; // float, take-off clip end frame (mod, 204)
        internal const long CatHitRadius     = CatBase + 0x128; // float, the cat's touch radius (mod)
        internal const long CatHitSlot       = CatBase + 0x12C; // int, enemy slot + 1 the cat touched (cave → mod; 0 none)
        // The pounce's clip frames: ready (in place) → take-off, in place until CatRampStart, forward momentum ramping
        // to full by CatRampEnd → leap at full momentum → landing. V = distance ÷ CatPounceTravel (the momentum-frames
        // the clips cover). A target higher than CatFlyThreshold above the floor gets the ballistic arc instead.
        internal const long CatReadyEnd      = CatBase + 0x130; // float, ready clip end frame (mod, 105)
        internal const long CatRampStart     = CatBase + 0x134; // float, take-off frame where the forward ramp starts (mod, 194)
        internal const long CatRampInv       = CatBase + 0x138; // float, 1 / (ramp end − ramp start) (mod)
        internal const long CatRampEnd       = CatBase + 0x13C; // float, take-off frame of full momentum (mod, 198; informational)
        internal const long CatLeapEnd       = CatBase + 0x140; // float, leap clip end frame (mod, 214)
        internal const long CatPounceTravel  = CatBase + 0x144; // float, momentum-frames covered by take-off + leap + landing (mod)
        internal const long CatFlyThreshold  = CatBase + 0x148; // float, target height above the floor that makes it a flying target (mod)
        internal const long CatPounceV       = CatBase + 0x14C; // float, this pounce's full momentum (cave)
        internal const long CatPounceFly     = CatBase + 0x150; // int, 1 = flying-target pounce (cave)
        internal const long CatFloatKey      = CatBase + 0x154; // int, the vertical-leap key held while rising on a flying pounce (mod, 71)
        internal const long CatDbgDist       = CatBase + 0x158; // float, distance compared when the cave decided to pounce (cave, diagnostics)
        internal const long CatDbgRange      = CatBase + 0x15C; // float, the range it compared against (cave, diagnostics)
        internal const long CatReadyStart    = CatBase + 0x160; // float, ready clip start frame (mod, 95) — clip-end tests ignore frames outside [start, end+1]
        internal const long CatTakeoffStart  = CatBase + 0x164; // float, take-off clip start frame (mod, 190)
        internal const long CatLeapStart     = CatBase + 0x168; // float, leap clip start frame (mod, 205)
        internal const long CatPounceMaxDist = CatBase + 0x16C; // float, farthest a pounce may still launch at after the ready (mod, 2× range)
        internal const long CatMoveAbs       = CatBase + 0x194; // float, absolute ground speed after the landing (units/frame); > 0 overrides CatMoveFrac (mod)
        internal const long CatLeapTravel    = CatBase + 0x198; // float, momentum-frames from the leap's start to the paws-touch frame (mod); the leap is re-sized to the live distance ÷ this
        internal const long CatHitSphere     = CatBase + 0x19C; // int, the enemy body sphere the touch test met (cave); the hit entry is planted on it
        internal const long CatSitKey        = CatBase + 0x1A0; // int, the sit clip's key — with no target the cat sits in place (mod)
        internal const long CatPounceGravity = CatBase + 0x1A4; // float, the ground pounce's arc gravity (mod)
        internal const long CatLeapRate      = CatBase + 0x1A8; // float, the leap clip's motion-speed override during a ground pounce (mod)
        internal const long CatPounceGround  = CatBase + 0x1AC; // int, 1 while a ground pounce is airborne (cave): the fall block uses CatPounceGravity
        internal const long CatFloatLaunch   = CatBase + 0x1B0; // float, the float-up frame where the feet leave the ground (mod): the vertical leap is computed there
        internal const long CatFloatStart    = CatBase + 0x1B4; // float, the float-up clip's first frame (mod)
        internal const long CatFloatRate     = CatBase + 0x1B8; // float, the float-up's motion-speed override (mod)
        internal const long CatFallBlend     = CatBase + 0x1BC; // float, MOTION_STATE blend increment for the float-up → fall fade (mod, 1/steps)
        internal const long CatBlendDefault  = CatBase + 0x1C0; // float, the increment put back when the land clip starts (mod, 0.1)
        internal const long CatHitEntry      = CatBase + 0x1C4; // int, the damage entry the cave planted natively, index + 1 (cave → mod)
        internal const long CatHitLatch      = CatBase + 0x1C8; // int, 1 while a planted entry is unresolved (cave sets, mod clears if it never connects)
        internal const long CatHitDamage     = CatBase + 0x1CC; // int, the entry's base damage = pellet damage + attack (mod, at bind)
        internal const long CatHitAttr       = CatBase + 0x1D0; // int, the entry's element attribute bits (mod, at bind)
        internal const long CatKickStrength  = CatBase + 0x1D4; // float (mod)
        internal const long CatKickDecay     = CatBase + 0x1D8; // float (mod)
        internal const long CatHeadNode      = CatBase + 0x1DC; // uint, guest address of the copy's cat_kao frame (mod, at spawn): the contact point is its posed world position
        internal const long CatFallBlendFrames = CatBase + 0x1E0; // float, the float-up → fall fade length in steps (mod): the cave times the switch so the fade ends as the land clip starts
        internal const long CatGlowOn        = CatBase + 0x1E4; // int, 1 while the cat is up (mod): the glow cave draws
        internal const long CatGlowScale     = CatBase + 0x1E8; // float, the torch routine's scale argument (mod; × the fade)
        internal const long CatGlowFlags     = CatBase + 0x1EC; // int, 1 = glow pair, 2 = flickering flame sprite, 3 = both (mod)
        internal const long CatGlowNodeA     = CatBase + 0x1F0; // uint, guest address of the copy's cat_kosibone frame (mod, at spawn)
        internal const long CatGlowNodeB     = CatBase + 0x1F4; // uint, guest address of the copy's cat_sebone2 frame (mod, at spawn)
        internal const long CatGlowReady     = CatBase + 0x1F8; // int, the cave bound its textures (cave; the mod clears it per charge)
        internal const long CatGlowPull      = CatBase + 0x1FC; // float, how far toward the camera the sprite is pulled (mod; the torches use 15)
        internal const long CatGlowObject    = CatBase + 0x200; // the cave's CFireOmni object, 0x40 B
        internal const long CatGlowLift      = CatBase + 0x240; // float, added to the glow's height (mod; negative lowers it)
        internal const long CatAimPos        = CatBase + 0x244; // float3 x,h,z: the point the cat walks to / jumps at — the target's biggest body sphere (mod, per tick); CatTargetPtr points here
        internal const long CatScaleMul      = CatBase + 0x250; // float: the cat's full size — the cave multiplies it into its growth k while the cat rides the pellet (mod writes DivineBeastTitle.CatScale at spawn; 0 = unset → the cave uses 1.0)
        internal const long CatHoldReady     = CatBase + 0x254; // int: 1 = the cave holds the ready crouch on its last frame instead of leaping (mod: the target cannot be hit yet)
        internal const long CatGlowName      = CatBase + 0x258; // char[16], NUL-terminated: the glow disc's texture entry — always "catglowp": every look shares one 8-bit disc and differs only in the palette row (see CatGlowPalRow); the glow cave binds it (mod writes it, then clears CatGlowReady)
        internal const long CatTrackHalf     = CatBase + 0x268; // float: 0 = the flying pounce re-aims until the apex; > 0 = keep re-aiming past the apex until halfway down to the floor (the winged cat, mod)
        internal const long CatApexH         = CatBase + 0x270; // float: the pounce's launch height, then the highest height while rising = the apex (cave)
        internal const long CatCapeCloth     = CatBase + 0x288; // uint: the cape's CCloth (guest) — the ONE cloth ElfCave.CatCapeTint recolours (mod; 0 = none)
        internal const long CatCapeTint      = CatBase + 0x28C; // float3: added to the global ambient for that cloth alone, so the cape carries a colour of its own (mod)
        internal const long CatPaletteTexEntry = CatBase + 0x29C; // uint, the CTexture entry ElfCave.CatPalette last found for catcape (cave; verified by name each frame, so a stale one costs one re-scan)
        internal const long CatGlowPalTexEntry = CatBase + 0x2A0; // uint, the CTexture entry ElfCave.CatGlowPalette last found for catglowp (cave; same by-name verification) — ⚠ the page ends at CatBase + 0x300
        internal const long CatGlowPalRow     = CatBase + 0x2A4; // int, ONE-based palette row the glow cave should paint
                                                         // (0 = derive it from the equipped element, which is what the
                                                         // cape look wants); 7/8/9 = Divine Beast Title / Angel Shooter /
                                                         // Angel Gear
    }
}
