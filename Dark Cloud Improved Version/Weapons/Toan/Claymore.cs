using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Claymore — "Greatsword": in Toan's hand the blade is <see cref="BladeScale"/>× its size, reaching as much
    /// farther, and every hit throws its enemy as the Baselard's does (MeleeKick, the engine's own knockback words). Super
    /// Steve carrying its SynthSphere inherits the throw alone (her pellets kick as the Baselard sphere's do, through
    /// Baselard.DriveSphere), not the size.
    ///
    /// The model's root frame ("w31", whose bind is the identity) is scaled <see cref="BladeScale"/>×, so the visible blade
    /// and the "dcol1" hit bone under it grow together: the swing spheres ride at S·Z from the hand instead of Z
    /// (Z = <see cref="Dcol1Z"/>, S the scale). That alone would carry them clean over anything closer than about
    /// S·Z − r − R (r the swing radius, R the enemy's body sphere), so while the blade is out every enemy whose body edge is
    /// within the scaled length gets (S − 1)·Z added to its body sphere radii — which puts its near edge exactly where the
    /// stock blade's was (Z − r − R; the hit sphere moved out by (S − 1)·Z, the target grew by the same) — and has them back
    /// the moment it is farther, so nothing past the visible blade is ever hit. The charge attacks read the same bone, so
    /// the lunge and the whirlwind (which sweeps dcol1 around Toan at S·Z with its 12-unit sphere, a hole the bonus
    /// closes) follow.</summary>
    internal static class Claymore
    {
        internal const float BladeScale = 1.8f;
        private const string ModelCode = "c01w31";
        /// <summary>The dcol1 bone's Z in c01w31 (combat and menu models alike): the stock hit distance from the hand.</summary>
        private const float Dcol1Z = 10.384f;
        private const float Bonus  = Dcol1Z * (BladeScale - 1f);   // added to a close enemy's body radii: how far the hit point moved out
        private const float Gate   = Dcol1Z * BladeScale;          // body-edge distance from Toan within which the bonus applies
        private const int   TickMs = 100;
        private const float RadiusSane = 200f;              // a body radius above this is not a real sphere

        private static readonly float[,] _stock = new float[EnemyAddresses.FloorSlots.Count, BodyCollision.MaxBodyParts];   // 0 = not captured
        private static bool _anyInflated;

        public static void GreatswordEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Claymore] greatsword: blade {BladeScale:F1}×, hit point {Dcol1Z:F1} → {Dcol1Z * BladeScale:F1}; body spheres +{Bonus:F1} within {Gate:F1}; kick {Baselard.KickStrength:F2} fading {Baselard.KickDecay:F2}");
            bool warnedNoBlade = false, loggedBlade = false;
            while (Player.Weapon.GetCurrentWeaponId() == Items.claymore && Player.InDungeonFloor())
            {
                if (Player.CurrentCharacterNum() == Player.ToanId)
                {
                    int blade = ScaleBlade();
                    if (blade < 0 && !warnedNoBlade) { warnedNoBlade = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[Claymore] the model's root frame does not read as the stock bind; the blade is left as it is"); }
                    if (blade > 0 && !loggedBlade) { loggedBlade = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Claymore] blade scaled {BladeScale:F1}×"); }
                    MeleeKick.Set(Baselard.KickStrength, Baselard.KickDecay);   // re-asserted: a word found vanilla is written again
                    MaintainHitboxes();
                }
                else { MeleeKick.Restore(); RestoreHitboxes(); }                // an ally out: the game's own kick and spheres
                Thread.Sleep(TickMs);
            }
            MeleeKick.Restore();
            RestoreHitboxes();
            // The blade is not put back: the weapon model is rebuilt from its pack for whatever is equipped next.
        }

        /// <summary>The root frame's local 3×3 set to BladeScale × identity. Idempotent: identity → scaled; already scaled →
        /// untouched (so a restart, a floor change or a rebuilt model can never compound it). Returns 1 when scaled or
        /// holding, 0 when the model is not located yet, −1 when the frame holds neither (left alone).</summary>
        private static int ScaleBlade()
        {
            uint name = Weapons.ResolveBladeFrame(ModelCode);
            if (name == 0) return 0;
            long frame = Weapons.LocateModelFrame(name, null);
            if (frame == 0) return 0;
            long m = frame + WeaponModel.Vu1LocalMatrixDiag0;
            bool identity = true, scaled = true;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    float v = Memory.ReadFloat(m + r * 0x10 + c * 4);
                    float want = r == c ? 1f : 0f;
                    if (Math.Abs(v - want) > 0.001f) identity = false;
                    if (Math.Abs(v - want * BladeScale) > 0.001f) scaled = false;
                }
            if (scaled) return 1;
            if (!identity) return -1;
            for (int r = 0; r < 3; r++) Memory.WriteFloat(m + r * 0x10 + r * 4, BladeScale);
            return 1;
        }

        /// <summary>Every live enemy: its body sphere radii at stock + Bonus while its body edge is within Gate of Toan, at stock
        /// otherwise. The stock figure is captured on first sight; a radius found holding neither stock nor stock + Bonus was
        /// written by something else since (a miniboss scale-up) and is taken as the new stock.</summary>
        private static void MaintainHitboxes()
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) <= 0)
                {
                    for (int p = 0; p < BodyCollision.MaxBodyParts; p++) _stock[s, p] = 0f;   // gone: the next occupant is captured fresh
                    continue;
                }
                long b = BodyCollision.SlotBase(s), up = EnemyAddresses.CharObjects.PosAddr(s);
                float dx = Memory.ReadFloat(up) - px, dy = Memory.ReadFloat(up + 8) - py;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                // The largest live sphere at stock size stands in for the body edge: the unit's own position, less that radius.
                float largest = 0f;
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                {
                    if (Memory.ReadInt(b + BodyCollision.ActiveArray + p * BodyCollision.BodyPartStride) == 0) continue;
                    float r = _stock[s, p] > 0f ? _stock[s, p] : Memory.ReadFloat(BodyCollision.RadiusAddr(s, p));
                    if (r > 0f && r < RadiusSane) largest = Math.Max(largest, r);
                }
                bool close = dist - largest <= Gate;
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                {
                    long addr = BodyCollision.RadiusAddr(s, p);
                    float r = Memory.ReadFloat(addr);
                    if (r <= 0f || r >= RadiusSane) continue;
                    float stock = _stock[s, p];
                    if (stock <= 0f || (Math.Abs(r - stock) > 0.01f && Math.Abs(r - (stock + Bonus)) > 0.01f))
                        stock = _stock[s, p] = r;                                            // first sight, or rewritten under us
                    float want = close ? stock + Bonus : stock;
                    if (Math.Abs(r - want) > 0.01f) { Memory.WriteFloat(addr, want); if (close) _anyInflated = true; }
                }
            }
        }

        /// <summary>Every captured radius back at stock.</summary>
        private static void RestoreHitboxes()
        {
            if (!_anyInflated) return;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                for (int p = 0; p < BodyCollision.MaxBodyParts; p++)
                {
                    float stock = _stock[s, p];
                    if (stock <= 0f) continue;
                    long addr = BodyCollision.RadiusAddr(s, p);
                    if (Math.Abs(Memory.ReadFloat(addr) - (stock + Bonus)) <= 0.01f) Memory.WriteFloat(addr, stock);
                    _stock[s, p] = 0f;
                }
            _anyInflated = false;
        }
    }
}
