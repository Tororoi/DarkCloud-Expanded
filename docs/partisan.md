# Partisan — quick combo (`Weapons/Ungaga/Partisan.cs`)

While the Partisan is Ungaga's weapon, his three combo swings play a third faster — Shamshir's factor, 4/3; the charge keeps its pace.

- `UngagaKey_Play` (main 0x243260): the swings are attack states 0x25 / 0x26 / 0x27 and play motions **37 / 38 / 39** (攻撃１
  670–678 step 0.28, 攻撃２ 678–694 0.32, 攻撃３ 695–714 0.32). Each hands over when the motion cursor is in a window at the
  PLAYING entry's own end: one frame for swings 1 and 3, HALF a frame for swing 2 (`end − 0.5 ≤ cursor ≤ end`). A step under the
  window's width always lands, so swing 2's step must stay under 0.5: × 4/3 gives 0.43 (× 2 would skip the hand-over to swing 3).
- The three KEY steps raised × 4/3 (0.37 / 0.43 / 0.43) in Ungaga's live Mot_List (CCharacter +0x344), Shamshir's method and
  gating: each entry checked against its frames before any write, written only in walking mode once the same list has been seen two ticks running,
  put back when the Partisan goes or another character is out. No ISO change.
- Tried first and dropped: a 2× first swing alone; and, for a thrust (none read as one), splicing the cutscene model's motion 8 (e323_2c10a 166–174) over 670–678, a
  single held pose relying on the engine's 10-frame key cross-fade, and baked wind-up → thrust clips from his charge poses
  (previews: `game_data/viewers/model/ungaga_swing_viewer.py`, `ungaga_thrust_viewer.py`).
- Super Steve with a Partisan SynthSphere: Shamshir's sphere effect — Xiao's draw ×1.6 and her shoot at 0.95 (`Shamshir.DriveSphere`, from Super Steve's dispatch).
