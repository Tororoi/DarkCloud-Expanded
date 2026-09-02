using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Queens waterfall mist: keeps the emitter table the ISO-baked queensSprayCave reads every frame
    /// (ElfPatches.PatchQueensSprayHook) populated with one entry per waterfall mouth at the live tide level.
    /// Pure data writes; CanalTide writes it at low tide and clears it otherwise.
    /// </summary>
    internal static class QueensCanalMist
    {
        // ── Waterfall mist ───────────────────────────────────────────────────────────────────────────
        // The engine's own EffectWaterSpray (the Matataki waterfall mist, "shibuki"/飛沫 spray texture) is spawned
        // from MainDraw but hardcoded to Matataki (NowEditMap==1). The queensSprayCave (IsoPatcher.PatchQueensSprayHook,
        // hooked at MainDraw 0x17c5a0) reads THIS table every frame and fires an emitter per entry, so we just keep
        // it populated while in Queens. Layout mirrors the cave: word[0]=count, then count × 0x30 entries
        // { pos x,y,z,w @+0x00 ; spread x,y,z,w @+0x10 ; bias bx,by,bz @+0x20 } starting at +0x10.
        //   pos    — waterfall mouth (from gedit/e03/scene.scn); Y = live surface level so the splash tracks the tide.
        //   spread — [x-scatter, size, z-scatter]: particle cloud size (NOT direction/height).
        //   bias   — added to each particle's initial velocity by the spray-bias shim: by<0 lowers the plume (the
        //            vanilla up-velocity is a fixed ~1-1.5, not a spread param); bx/bz aim the mist horizontally
        //            (facing), which spread can't do (its x is symmetric scatter with no net direction).
        // Each waterfall gets EmittersPerFall emitters spread across its X width. World axes: +Z = south, -Z = north
        // (matches the vanilla spray's slight built-in -Z "north" lean); +X = east. Signs/magnitudes are TUNABLE.
        private const long  SprayTableBase = CodeCaves.QueensSprayTable;
        private const float SprayLeanMag = 0.35f;   // horizontal facing strength — kept BELOW the vertical rise so the
                                                    //   mist mostly goes up (was 1.5, which read too horizontal)
        private const float SprayVzDebias = 0.5f;   // EffectWaterSpray bakes a fixed -0.5 vz ("north") into every
                                                    //   particle; add this so the lean below is what's actually seen
        private const float SprayUpBias     = -0.7f;   // obj48: negative → lower plume (÷~3-ish; tune with the surface look)
        private const float SprayUpBiasTaki = -0.5f;   // taki1: taller (≈½ the original height, per request)
        private static readonly float[] Spread = { 5f, 2f, 5f };   // x-scatter, size, z-scatter
        // Each fall's N emitters fan from (Xc,Zc) across ±(SpanX,SpanZ) — obj48s fan along X (their narrow mouth),
        // taki1 fans along Z (its wide western edge). leanZ gets +SprayVzDebias applied on write. Up = per-fall height bias.
        // obj48 Zc = ±27 (the FRONT of the D collision footprint, where the water lands) — NOT the mesh centre ±37,
        // which sits behind the fall's bottom edge.
        private static readonly (float Xc, float Zc, float SpanX, float SpanZ, int N, float LeanX, float LeanZ, float Up)[] Waterfalls =
        {
            (198f,  -27f, 8f, 0f,  1,  0f, +SprayLeanMag, SprayUpBias),   // obj48 @X198, north wall (-Z) → face south (+Z), toward centre
            (198f,   27f, 8f, 0f,  1,  0f, -SprayLeanMag, SprayUpBias),   // obj48 @X198, south wall (+Z) → face north (-Z), toward centre
            (601f,  -27f, 8f, 0f,  1,  0f, +SprayLeanMag, SprayUpBias),
            (601f,   27f, 8f, 0f,  1,  0f, -SprayLeanMag, SprayUpBias),
            (1100f, -27f, 8f, 0f,  1,  0f, +SprayLeanMag, SprayUpBias),
            (1100f,  27f, 8f, 0f,  1,  0f, -SprayLeanMag, SprayUpBias),
            // taki1: western edge (X≈1262 where the fall meets the canal), fanned along its full Z width (-48..52), facing WEST, taller
            (1262f,   2f, 0f, 50f, 10, -SprayLeanMag, 0f, SprayUpBiasTaki),
        };

        internal static void WriteSprayTable(float waterY)
        {
            int idx = 0;
            foreach (var w in Waterfalls)
            {
                for (int k = 0; k < w.N && idx < CodeCaves.QueensSprayMaxEmitters; k++, idx++)
                {
                    float t = w.N == 1 ? 0f : k / (float)(w.N - 1) * 2f - 1f;  // -1..+1 across the fall
                    float x = w.Xc + t * w.SpanX;
                    float z = w.Zc + t * w.SpanZ;
                    long e = SprayTableBase + 0x10 + idx * CodeCaves.QueensSprayEntryStride;
                    Memory.WriteFloat(e + 0x00, x);         Memory.WriteFloat(e + 0x04, waterY);
                    Memory.WriteFloat(e + 0x08, z);         Memory.WriteFloat(e + 0x0C, 1f);
                    Memory.WriteFloat(e + 0x10, Spread[0]); Memory.WriteFloat(e + 0x14, Spread[1]);
                    Memory.WriteFloat(e + 0x18, Spread[2]); Memory.WriteFloat(e + 0x1C, 1f);
                    Memory.WriteFloat(e + 0x20, w.LeanX);              Memory.WriteFloat(e + 0x24, w.Up);
                    Memory.WriteFloat(e + 0x28, w.LeanZ + SprayVzDebias); Memory.WriteFloat(e + 0x2C, 0f);
                }
            }
            Memory.WriteInt(SprayTableBase, idx);   // count LAST — the cave reads this each frame; never expose a partial table
        }

        internal static void ClearSprayTable() => Memory.WriteInt(SprayTableBase, 0);
    }
}
