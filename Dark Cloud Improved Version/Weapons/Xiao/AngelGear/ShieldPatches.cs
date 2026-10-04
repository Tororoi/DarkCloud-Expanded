using System;
using static Dark_Cloud_Improved_Version.AngelGear;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The shield's two cold EE patches and the data words they read: MoveCheck2's enemy-vs-player block distance
    /// becomes the Mailbox word ShieldBlockAddend (<see cref="ArmBlockPatch"/>; 6.0 vanilla, the ring's radius while the
    /// shield is up) and checkCollision's player-position load becomes a pointer read of Mailbox.ShotHitTarget
    /// (<see cref="ArmShotPatch"/>), which <see cref="RedirectShots"/> aims at the copy's pouch while the shield is solid
    /// so enemy shots collide with it natively. Applied ONCE while cold (menu/town — writing hot EE code crashes the
    /// recompiler). One of the <see cref="AngelGear"/> classes, which share their members through using static.</summary>
    internal static class ShieldPatches
    {
        private const string Tag = "[AngelGear/ShieldPatches] ";

        // PHYSICAL BLOCK (a collision circle, not a per-frame clamp). CMonstorUnit::MoveCheck2
        // (0x1DCDD0, called from Step for every enemy) zeroes an enemy's scripted movement (dir +0x1E430 /
        // speed +0x1E450) when its next position is within (its move radius +0x1E414 + 6.0) of the player
        // and it is heading toward her — the ONLY enemy-vs-player body block in the engine (MoveChecMonster
        // is enemy-vs-enemy; MoveCheck is the player-vs-enemy side). The 6.0 is a per-site immediate:
        //   0x1DCFD0  lui  $v1,0x40c0     →  lui  $v1,HI(Mailbox.ShieldBlockAddend)
        //   0x1DCFD4  mtc1 $v1,$f1        →  lwc1 $f1,LO(Mailbox.ShieldBlockAddend)($v1)
        // Two words, applied ONCE while cold (menu/town — writing hot EE code crashes the recompiler);
        // from then on the block distance is a DATA word: 6.0 vanilla, RingRadius while the shield is up.
        private const long   BlockPatchAddr = 0x201DCFD0;
        private static readonly uint[] BlockPristine = { 0x3C0340C0u, 0x44830800u };
        private static readonly uint[] BlockPatched  = { 0x3C030000u | (uint)((Mailbox.ShieldBlockAddend - 0x20000000) >> 16),
                                                          0xC4610000u | (uint)((Mailbox.ShieldBlockAddend - 0x20000000) & 0xFFFF) };
        internal const float VanillaBlockAddend = 6f;
        // ENGINE-SIDE CATCH (no per-tick collision checks). checkCollision (0x1AB740) is every
        // shot's hit-the-player test; its player-position load (`lui $v0,0x1ea; addiu $a1,$v0,0x1d30` @0x1AB828,
        // $v0 dead after) becomes a POINTER read of Mailbox.ShotHitTarget. Solid shield → pointer = the copy's
        // shot-target node (pouch lowered by her 14-unit body lift, engine-refreshed every draw) → shots hit the POUCH natively at frame
        // rate; claimed (latched) shots plant nothing there and simply end — their death near the pouch is
        // the catch. Shield down → pointer = the player global (vanilla).
        private const long   ShotPatchAddr = 0x201AB828;
        private static readonly uint[] ShotPristine = { 0x3C0201EAu, 0x24451D30u };
        private static readonly uint[] ShotPatched  = { 0x3C050000u | (uint)((Mailbox.ShotHitTarget - 0x20000000) >> 16),
                                                         0x8CA50000u | (uint)((Mailbox.ShotHitTarget - 0x20000000) & 0xFFFF) };

        internal static bool _blockArmed, _shotArmed;
        private static bool _blockWarned, _shotWarned, _shotRedirected;

        /// <summary>Make MoveCheck2's enemy-block addend a data word (see the constants above). Cold only:
        /// called from the main-menu entry and retried from the loop while not on a dungeon floor.
        /// Idempotent; refuses (log once) if the words are neither vanilla nor ours.</summary>
        internal static void ArmBlockPatch()
        {
            if (_blockArmed) return;
            try
            {
                uint w0 = (uint)Memory.ReadInt(BlockPatchAddr), w1 = (uint)Memory.ReadInt(BlockPatchAddr + 4);
                if (w0 == BlockPatched[0] && w1 == BlockPatched[1])
                {
                    Memory.WriteFloat(Mailbox.ShieldBlockAddend, VanillaBlockAddend);   // stale value from a dead session
                    _blockArmed = true;
                    return;
                }
                if (w0 != BlockPristine[0] || w1 != BlockPristine[1])
                {
                    if (!_blockWarned) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"MoveCheck2 @0x{BlockPatchAddr:X8} is not vanilla ({w0:X8} {w1:X8}) — shield block not armed");
                    _blockWarned = true;
                    return;
                }
                Memory.WriteFloat(Mailbox.ShieldBlockAddend, VanillaBlockAddend);   // data first...
                Memory.WriteUInt(BlockPatchAddr,     BlockPatched[0]);                           // ...then lui (a half-applied pair is harmless this way round)
                Memory.WriteUInt(BlockPatchAddr + 4, BlockPatched[1]);
                _blockArmed = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shield block armed: MoveCheck2 addend → data word (6.0 vanilla)");
            }
            catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "block patch failed: " + e.Message); }
        }

        /// <summary>Make checkCollision's player-position load a pointer read (see the constants). Cold only.</summary>
        internal static void ArmShotPatch()
        {
            if (_shotArmed) return;
            try
            {
                uint w0 = (uint)Memory.ReadInt(ShotPatchAddr), w1 = (uint)Memory.ReadInt(ShotPatchAddr + 4);
                if (w0 == ShotPatched[0] && w1 == ShotPatched[1])
                {
                    Memory.WriteUInt(Mailbox.ShotHitTarget, StbExternCmd.PlayerPosGuest);
                    _shotArmed = true;
                    return;
                }
                if (w0 != ShotPristine[0] || w1 != ShotPristine[1])
                {
                    if (!_shotWarned) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                        $"checkCollision @0x{ShotPatchAddr:X8} is not vanilla ({w0:X8} {w1:X8}) — engine-side catch not armed");
                    _shotWarned = true;
                    return;
                }
                Memory.WriteUInt(Mailbox.ShotHitTarget, StbExternCmd.PlayerPosGuest);   // pointer first...
                Memory.WriteUInt(ShotPatchAddr,     ShotPatched[0]);                               // ...then lui $a1
                Memory.WriteUInt(ShotPatchAddr + 4, ShotPatched[1]);                               // ...then lw $a1
                _shotArmed = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "engine-side catch armed: checkCollision reads the shot target through a pointer");
            }
            catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "shot patch failed: " + e.Message); }
        }

        /// <summary>Aim every enemy shot's hit-the-player test at the pouch while the shield is solid, back
        /// at her when it is not. Two writes per transition, nothing per frame.</summary>
        internal static void RedirectShots(bool solid)
        {
            if (!_shotArmed) return;
            uint target = solid ? SlingshotProp.ShotTargetGuest : 0u;
            bool want = target != 0;
            if (want == _shotRedirected) return;
            Memory.WriteUInt(Mailbox.ShotHitTarget, want ? target : StbExternCmd.PlayerPosGuest);
            _shotRedirected = want;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + (want ? $"shots now collide with the pouch (0x{target:X})" : "shots collide with her again"));
        }
    }
}
