using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The player: which character is active, what mode / menu the game is in, and one <see cref="Character"/>
    /// per party member (<see cref="Toan"/> .. <see cref="Osmond"/>). Addresses live in
    /// <see cref="PlayerAddresses"/>; bag access in <see cref="Inventory"/>; bag weapon records in
    /// <see cref="WeaponRecord"/>; the equipped weapon's battle copy in <see cref="Weapon"/>.
    /// </summary>
    internal class Player
    {
        private const string LogTag = "[Player] ";

        public const int ToanId = 0;
        public const int XiaoId = 1;
        public const int GoroId = 2;
        public const int RubyId = 3;
        public const int UngagaId = 4;
        public const int OsmondId = 5;

        internal static readonly Character Toan   = new Character(ToanId);
        internal static readonly Character Xiao   = new Character(XiaoId);
        internal static readonly Character Goro   = new Character(GoroId);
        internal static readonly Character Ruby   = new Character(RubyId);
        internal static readonly Character Ungaga = new Character(UngagaId);
        internal static readonly Character Osmond = new Character(OsmondId);

        /// <summary>
        /// Returns the current character being used inside the dungeon.
        /// </summary>
        /// <returns><br>0 = Toan</br>
        /// <br>1 = Xiao</br>
        /// <br>2 = Goro</br>
        /// <br>3 = Ruby</br>
        /// <br>4 = Ungaga</br>
        /// <br>5 = Osmond</br></returns>
        public static int CurrentCharacterNum()
        {
            int character = Memory.ReadByte(PlayerAddresses.CurrentCharacter);

            switch (character)
            {
                case ToanId: return ToanId;
                case XiaoId: return XiaoId;
                case GoroId: return GoroId;
                case RubyId: return RubyId;
                case UngagaId: return UngagaId;
                case OsmondId: return OsmondId;

                default: return 255;
            }
        }

        /// <summary>
        /// Returns a string with the characters name basing on the input id.
        /// </summary>
        /// <param name="character">
        /// <br>0 = Toan</br>
        /// <br>1 = Xiao</br>
        /// <br>2 = Goro</br>
        /// <br>3 = Ruby</br>
        /// <br>4 = Ungaga</br>
        /// <br>5 = Osmond</br>
        /// </param>
        /// <returns>
        /// <br>0 = "Toan"</br>
        /// <br>1 = "Xiao"</br>
        /// <br>2 = "Goro"</br>
        /// <br>3 = "Ruby"</br>
        /// <br>4 = "Ungaga"</br>
        /// <br>5 = "Osmond"</br></returns>
        public static string GetCharacterName(int character)
        {
            switch (character)
            {
                case ToanId: return "Toan";
                case XiaoId: return "Xiao";
                case GoroId: return "Goro";
                case RubyId: return "Ruby";
                case UngagaId: return "Ungaga";
                case OsmondId: return "Osmond";
                default: return null;
            }
        }

        /// <summary>
        /// Value is 255 when in town AND dungeon select, changes when floor is loaded. This also triggers when entering and leaving the menu in a dungeon.
        /// </summary>
        public static bool InDungeonFloor()
        {
            if (Memory.ReadByte(PlayerAddresses.FloorLoadedFlag) != 255)
                return true;

            else
                return false;
        }

        /// <summary>
        /// Get or set the current amount of Gilda.
        /// </summary>
        public static ushort Gilda
        {
            get
            {
                ushort value = Memory.ReadUShort(PlayerAddresses.Gilda);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + LogTag + "Player has " + value + " Gilda");
                return value;
            }
            set
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + LogTag + "Player's Gilda was set to: " + value);
                Memory.WriteUShort(PlayerAddresses.Gilda, value);
            }
        }

        /// <summary>
        /// Returns true if the player is in first person view while in town
        /// </summary>
        public static bool CheckTownFirstPersonMode()
        {
            if (Memory.ReadUShort(Addresses.townCameraPerspective) == 1)
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Returns true if the player is in first person view while inside a dungeon
        /// </summary>
        public static bool CheckDunFirstPersonMode()
        {
            if (Memory.ReadUShort(PlayerAddresses.DunCameraPerspective) == 10)
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Returns true if the player is interacting with a chest
        /// </summary>
        public static bool CheckDunIsOpeningChest()
        {
            if (Memory.ReadUShort(PlayerAddresses.DunCameraPerspective) == 121 ||   //Big Chest
                Memory.ReadUShort(PlayerAddresses.DunCameraPerspective) == 131)     //Normal Chest
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Returns true if the player is interacting with something while inside a dungeon (Ex: dialogue with exit gate)
        /// </summary>
        public static bool CheckDunIsInteracting()
        {
            if (Memory.ReadUShort(PlayerAddresses.DunCameraPerspective) == 400 ||
                Memory.ReadUShort(PlayerAddresses.DunCameraPerspective) == 401)
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Returns true if the game is paued while in town
        /// </summary>
        public static bool CheckTownIsPaused()
        {
            if (Memory.ReadUShort(Addresses.townMode) == 9)
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// True while the game is paused in a way that should freeze in-dungeon gameplay — the "PAUSE" screen
        /// (<see cref="CheckDunIsPaused"/>) OR the in-dungeon item menu.
        ///
        /// The two freeze DIFFERENT things natively, which is why callers usually want BOTH: the item menu
        /// freezes characters but NOT mod timers, while the PAUSE screen freezes mod timers but NOT the
        /// dungeon's chara loop. Anything the mod drives on a timer (or steps itself) will drift during one of
        /// them unless it checks this rather than CheckDunIsPaused alone.
        /// </summary>
        public static bool CheckDunIsPausedOrMenu()
        {
            if (CheckDunIsPaused()) return true;
            return Memory.ReadByte(Addresses.mode) == 3 && Memory.ReadByte(Addresses.dungeonMode) == 2;   // in-dungeon item menu
        }

        /// <summary>
        /// Returns true if the game is paused while inside a dungeon
        /// </summary>
        public static bool CheckDunIsPaused()
        {
            if (Memory.ReadUShort(PlayerAddresses.DunPausePlayerState) == 1
                && Memory.ReadUShort(PlayerAddresses.DunPauseEnemyState) == 1
                && Memory.ReadUShort(PlayerAddresses.DunPauseTitle) == 1
                && Memory.ReadUShort(PlayerAddresses.DunCameraPerspective) == 155)
            {
                return true;
            }

            else return false;
        }

        /// <summary>
        /// Returns true if the player is in the world map menu
        /// </summary>
        public static bool CheckIsWorldMapMenu()
        {
            if (Memory.ReadUShort(Addresses.townMode) == 8) return true;
            else return false;
        }

        /// <summary>
        /// Returns true if the player is editing georama
        /// </summary>
        public static bool CheckIsGeoramaMode()
        {
            if (Memory.ReadUShort(Addresses.townMode) == 4) return true;
            else return false;
        }

        /// <summary>
        /// Returns true if the player is fishing
        /// </summary>
        public static bool CheckIsFishingMode()
        {
            if (Memory.ReadUShort(Addresses.townMode) == 16) return true;
            else return false;
        }

        /// <summary>
        /// Returns true if the player is not in a menu while inside a dungeon
        /// </summary>
        /// <summary>A load is on: the loading screen's end flag is down.</summary>
        public static bool CheckIsLoading() => Memory.ReadInt(Addresses.nowLoadingEnd) == 0;

        public static bool CheckDunIsWalkingMode()
        {
            if (Memory.ReadUShort(Addresses.dungeonMode) == 1) return true;
            else return false;
        }

        /// <summary>
        /// Returns true if the player is inside the weapons menu
        /// </summary>
        public static bool CheckIsWeaponMenu()
        {
            if (Memory.ReadByte(Addresses.selectedMenu) == 2) return true;
            return false;
        }

        /// <summary>
        /// Returns true if the player is in the customize weapon menu
        /// </summary>
        public static bool CheckIsWeaponCustomizeMenu()
        {
            if (Memory.ReadByte(Addresses.selectedMenu) == 2 &&
                Memory.ReadByte(Addresses.weaponsMode) >= 8 &&
                Memory.ReadByte(Addresses.weaponsMode) <= 11) return true;
            return false;
        }

        /// <summary>True while the ACTIVE character is holding a charge attack (animation state 14). Used by Ruby's
        /// Mobius / charge glow; it reads the shared animation word, not a per-character field.</summary>
        public static bool IsChargingAttack()
        {
            return Memory.ReadByte(PlayerAddresses.AnimationId) == 14;
        }

        /// <summary>
        /// Fires the engine's whole-character ambient "flash" on the ACTIVE unit by writing the same globals
        /// <c>setUnitAmbientAnime</c> (dun 0x1DC1000) sets. The per-frame <c>unitAmbientAnime</c> then drives
        /// the unit's ambient colour = (colour × pulse) + 64 for <paramref name="count"/> repeats before it
        /// self-disables — the same effect the game uses for drink / face-change / Ruby's Mobius charge flash,
        /// so it works for whoever is active. <paramref name="r"/>/<paramref name="g"/>/<paramref name="b"/>
        /// are 0-255; enable is written last so the updater never runs mid-setup.
        /// </summary>
        public static void FlashActiveCharacter(float r, float g, float b, float speed, int count)
        {
            Memory.WriteFloat(CharacterFlash.ColorR, r);
            Memory.WriteFloat(CharacterFlash.ColorG, g);
            Memory.WriteFloat(CharacterFlash.ColorB, b);
            Memory.WriteFloat(CharacterFlash.Speed, speed);
            Memory.WriteInt(CharacterFlash.Count, count);
            Memory.WriteFloat(CharacterFlash.Phase, 0f);   // (re)start the pulse
            Memory.WriteInt(CharacterFlash.Enable, 1);     // enable last
        }

        /// <summary>Fire the game's STOCK charge-complete flash (see <see cref="CharacterFlash.ChargeR"/>) on the
        /// active character. Use this rather than hand-picking an RGB whenever the flash means "a charge finished
        /// building" — it is what Ruby's Mobius peak and Ungaga's guard charge use, so it matches the vanilla tint.</summary>
        public static void FlashChargeComplete()
            => FlashActiveCharacter(CharacterFlash.ChargeR, CharacterFlash.ChargeG, CharacterFlash.ChargeB,
                                    CharacterFlash.ChargeSpeed, CharacterFlash.ChargeCount);

        /// <summary>
        /// The equipped weapon's IN-BATTLE record (<see cref="WeaponHave.BattleWeaponRecord"/>, 0x21EA7590), a
        /// WEAPON_HAVE copy with the same field layout as the bag records (<see cref="WeaponRecord"/> offsets).
        /// The engine rebuilds it from the bag record on equip changes and menu closes, and the swing code latches
        /// damage from it: a stat boost written here lasts until the next rebuild and never reaches the bag record.
        /// </summary>
        internal class Weapon
        {
            private const long id       = WeaponHave.BattleWeaponRecord + WeaponRecord.Id;         // 0x21EA7590
            private const long attack   = WeaponHave.BattleWeaponRecord + WeaponRecord.Attack;     // 0x21EA7594
            private const long magic    = WeaponHave.BattleWeaponRecord + WeaponRecord.Magic;      // 0x21EA759A
            private const long maxWhp   = WeaponHave.BattleWeaponRecord + WeaponRecord.WhpMax;     // 0x21EA759C
            private const long element  = WeaponHave.BattleWeaponRecord + WeaponRecord.ElementHud; // 0x21EA75A6: selected element (0-4)
            private const long special2 = WeaponHave.BattleWeaponRecord + WeaponRecord.Special2;   // 0x21EA767F

            /// <summary>The weapon the ACTIVE character holds: the id of their equipped BAG slot's record. The battle record
            /// at <see cref="id"/> is a copy taken at equip time, and a BUILD-UP rewrites the bag record in place without
            /// an equip — the copy then still names the old weapon, and every ability keyed on it stayed the old
            /// weapon's until a re-equip. The copy is the answer only when the bag cannot be (no valid slot, an empty
            /// record, or the character monster-transformed, when the bag still lists a weapon they are not holding).</summary>
            public static ushort GetCurrentWeaponId()
            {
                int ch = Player.CurrentCharacterNum();
                int slot = ch >= 0 && ch < 6 ? Memory.ReadByte(DngStatusData.EquippedSlotAddr(ch)) : DngStatusData.MaxWeaponSlots;
                if (slot < DngStatusData.MaxWeaponSlots
                    && Memory.ReadInt(DngStatusData.Base + DngStatusData.TransformStateOffset) != DngStatusData.TransformedMonster)
                {
                    ushort held = Memory.ReadUShort(DngStatusData.WeaponRecord(ch, slot));
                    if (held != 0) return held;
                }
                return Memory.ReadUShort(id);
            }

            /// <summary>The equipped weapon's effective (post-attachment) Attack.</summary>
            public static ushort GetCurrentWeaponAttack()
            {
                return Memory.ReadUShort(attack);
            }

            /// <summary>The equipped weapon's effective Magic.</summary>
            public static ushort GetCurrentWeaponMagic()
            {
                return Memory.ReadUShort(magic);
            }

            /// <summary>The equipped weapon's max WHP.</summary>
            public static ushort GetCurrentWeaponMaxWhp()
            {
                return Memory.ReadUShort(maxWhp);
            }

            /// <summary>The equipped weapon's selected element index: 0 Fire, 1 Ice, 2 Thunder, 3 Wind, 4 Holy, 5 none.</summary>
            public static byte GetCurrentWeaponElement()
            {
                return Memory.ReadByte(element);
            }

            /// <summary>The equipped weapon's second ability bitfield: 02 Durable, 04 Drain, 08 Heal, 16 Critical, 32 Abs Up.</summary>
            public static byte GetCurrentWeaponSpecial2()
            {
                return Memory.ReadByte(special2);
            }
        }
    }
}
