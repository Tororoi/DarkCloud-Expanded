namespace Dark_Cloud_Improved_Version
{
    /// <summary>The town water redraw's runtime words (IsoPatcher.PatchWaterRedraw / PatchDrawWaterCompaction; CanalTide), in the
    /// last free span under the ELF cave segment.</summary>
    internal static class WaterRedraw
    {
        /// <summary>Town water "submerged tint" redraw — see <c>IsoPatcher.PatchWaterRedraw</c> /
        /// <c>PatchDrawWaterCompaction</c>. The redraw CODE lives baked inside MainDraw/DrawWater's own
        /// ELF footprint (the draw is MOVED to after the character, not duplicated); these two words are
        /// plain runtime DATA the baked code reads/writes, on a runtime-data page (docs/code-caves.md).
        /// +0x00 <c>WaterRedrawPendingFlag</c> — MainDraw's payload-start STUB sets it when the GameMode
        /// gate matches (instead of drawing there); the hook-site cave checks and unconditionally clears
        /// it (every frame, whether set or not, so no stale state survives into a non-matching frame)
        /// to decide whether to `jal` the relocated draw payload.
        /// +0x04 <c>DrawWaterHelperRaScratch</c> — the DrawWater vtable-call-bracket helper's own $ra
        /// stash, needed because it makes two nested calls and neither survives in a register (no free
        /// callee-saved slot in DrawWater's frame, and its sp-relative locals rule out the helper opening
        /// a second stack frame).</summary>
        internal const uint WaterRedrawPendingFlag     = 0x01FAE600;
        internal const uint DrawWaterHelperRaScratch   = 0x01FAE604;

        /// <summary>Low-tide mizu-reorder mailbox — see <c>IsoPatcher.PatchWaterRedraw</c>'s MIZU_STUB and
        /// <c>CanalTide</c>. The baked stub (hosted in the compacted GameMode gate) reads these at the
        /// post-character hook: if FramePtr is nonzero it ReloadTexture(TexGroup)s and MGDraw()s that frame
        /// — drawing the (scene-pass-hidden) water mesh AFTER the player so its own semi-transparent
        /// texture blends over the submerged body. C# owns both words: TexGroup is written BEFORE FramePtr
        /// (the pointer is the stub's gate), and FramePtr is zeroed on map change / frame loss / non-low
        /// tide. Zero at boot = feature off until armed.</summary>
        internal const long MizuRedrawFramePtr      = 0x21FAE608;   // MMU (C# writes)
        internal const uint MizuRedrawFramePtrGuest = 0x01FAE608;   // guest (baked into the stub)
        internal const long MizuRedrawTexGroup      = 0x21FAE60C;
        internal const uint MizuRedrawTexGroupGuest = 0x01FAE60C;
        /// <summary>Water-redraw payload dynamic-return slot (waterOrderGate.bin: COND/SHIM write it,
        /// RET_THUNK `jr`s through it). EE-side only — C# never touches it.</summary>
        internal const uint WaterRedrawReturnSlotGuest = 0x01FAE610;
    }
}
