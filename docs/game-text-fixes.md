# Game text fixes

Every message in the game's English text that the ISO patch's `text-fixes` step changes, stock on the first line and
patched on the second (`/` line break, `¶` page break). The step edits each bank in place (`IsoPatch/MesTextFixes.cs`:
whole-word substitutions plus the two spacing rules); the mod's own dialogue is not part of this list.

173 messages in 51 banks. Regenerate with `tools/analysis/gen_game_text_fixes.py <stock iso> <patched iso>`.

## `dun\message\ww_mes\dunmsd00_1.mes`

- **#3002** Skelton Soldier  
  → Skeleton Soldier
- **#3003** Skelton Soldier  
  → Skeleton Soldier

## `dun\message\ww_mes\Steve02_1.mes`

- **#4127** Earth Digger is called / Earth Digger because he digs / earth. Your welcome.  
  → Earth Digger is called / Earth Digger because he digs / earth. You're welcome.
- **#4204** Come on! That the best / you can do little Timmie.  
  → Come on! That's the best / you can do little Timmie.
- **#4205** There must've been alot of / these hats at that garage / sale.  
  → There must've been a lot of / these hats at that garage / sale.

## `dun\message\ww_mes\Steve05_1.mes`

- **#4217** It seems like Helga  has  / apples too.  
  → It seems like Helga has  / apples too.
- **#4403** He's got alot of children.  
  → He's got a lot of children.
- **#4661** Anyways, I wonder  / how's Tukkie is doing.  
  → Anyways, I wonder  / how Tukkie is doing.

## `dun\message\ww_mes\Steve06_1.mes`

- **#4619** Who says a  black cat crossing  / our path is bad luck! / Who says that?  
  → Who says a black cat crossing  / our path is bad luck! / Who says that?
- **#4677** I  wonder [Xiao],  / how old you are?  
  → I wonder [Xiao],  / how old you are?
- **#4695** [Xiao], you're too  friendly. / It makes me jealous.  
  → [Xiao], you're too friendly. / It makes me jealous.
- **#4699** Billy used to be my brother-in / -arms. We fought alot. / But now he's...  
  → Billy used to be my brother-in / -arms. We fought a lot. / But now he's...

## `dun\message\ww_mes\Steve07_1_1.mes`

- **#4403** He's got alot of children.  
  → He's got a lot of children.

## `dun\message\ww_mes\Steve07_2_1.mes`

- **#4217** It seems like Helga  has  / apples too.  
  → It seems like Helga has  / apples too.

## `dun\message\ww_mes\Steve07_3_1.mes`

- **#7135** Even feathers have  magical gems...  
  → Even feathers have magical gems...
- **#7165** Stabilizers are down! / I'll try going to  / auxilliary power!!  
  → Stabilizers are down! / I'll try going to  / auxiliary power!!

## `dun\message\ww_mes\Steve07_4_1.mes`

- **#7182** There are alot of guys / running around that look / like him.  
  → There are a lot of guys / running around that look / like him.
- **#7189** Darn,  I couldn't get to  / telling you his weak point!  
  → Darn, I couldn't get to  / telling you his weak point!

## `dun\message\ww_mes\Steve07_5_1.mes`

- **#4619** Who says a  black cat crossing  / our path is bad luck! / Who says that?  
  → Who says a black cat crossing  / our path is bad luck! / Who says that?

## `gedit\e01\e01talk_1.mes`

- **#80** Ah, [Toan],  I missed you, man! / Your smile really brightens my day.  
  → Ah, [Toan], I missed you, man! / Your smile really brightens my day.
- **#130** …y or whatever it's  / called  will exchange it for an ite…  
  → …y or whatever it's  / called will exchange it for an ite…
- **#167** Say [Toan], you're going / the Cave alot lately, huh. / I heard. ¶ Can I come with you the next time?  
  → Say [Toan], you're going / the Cave a lot lately, huh. / I heard. ¶ Can I come with you the next time?
- **#181** …? / Carl's always getting in  trouble and / annoying me. …  
  → …? / Carl's always getting in trouble and / annoying me. …
- **#250** … Garayan"? / It's rare fish,  a legendary fish. ¶ Its bea…  
  → … Garayan"? / It's rare fish, a legendary fish. ¶ Its bea…

## `gedit\e01\e01_1.mes`

- **#200** …ful / this Genie is, but it  was powerful  / enough to co…  
  → …ful / this Genie is, but it was powerful  / enough to co…
- **#503** …ere the  / power of Atlamilla resides.  
  → …ere the  / power of Atlamillia resides.
- **#512** It's a guide for using Atlamilla / and other things. I wrote / it for beginners like you.  
  → It's a guide for using Atlamillia / and other things. I wrote / it for beginners like you.

## `gedit\e01\in\i01h10\i01h10_1.mes`

- **#102** There, done! ¶ Looks like it can carry / alot of items, huh?  
  → There, done! ¶ Looks like it can carry / a lot of items, huh?

## `gedit\e01\in\i01h11\i01h11_1.mes`

- **#100** Hey, good to see you. / Everything's back the way it was . ¶ I don't even know how to thank you.  
  → Hey, good to see you. / Everything's back the way it was. ¶ I don't even know how to thank you.

## `gedit\e02\e02talk_1.mes`

- **#48** …ond. ¶ Treant needs water, alot of it.  / So we connecte…  
  → …ond. ¶ Treant needs water, a lot of it.  / So we connecte…
- **#86** … quite  / get into my music  without that room.  
  → … quite  / get into my music without that room.
- **#225** How was it? / Was that seed useful ? ¶ I wonder where that "special place" is, / where that is supposed to grow.  
  → How was it? / Was that seed useful? ¶ I wonder where that "special place" is, / where that is supposed to grow.

## `gedit\e02\in\i02h01\i02h01_1.mes`

- **#103** …/ may not be so easy to find . ¶ Oh well, don't get disco…  
  → …/ may not be so easy to find. ¶ Oh well, don't get disco…

## `gedit\e02\in\i02h03\i02h03_1.mes`

- **#200** …ululu's. ¶ She got mad at me and and  / climbed way up there… · …or. / Oh, and it was really  sweet.  
  → …ululu's. ¶ She got mad at me and  / climbed way up there… · …or. / Oh, and it was really sweet.

## `gedit\e03\e03talk_1.mes`

- **#20** … from out-of-town, huh. / Ok , so I'll introduce myself. …  
  → … from out-of-town, huh. / Ok, so I'll introduce myself. …
- **#22** …uld put me / near the watery . ¶ Water's something we all…  
  → …uld put me / near the watery. ¶ Water's something we all…
- **#127** Have you met Rando, who has a store at  / the port? He doesn't look it but he's  / been around alot longer than me.  
  → Have you met Rando, who has a store at  / the port? He doesn't look it but he's  / been around a lot longer than me.
- **#133** …es. You must / have gotten alot of rare items. ¶ Have yo…  
  → …es. You must / have gotten a lot of rare items. ¶ Have yo…
- **#212** …d  / the mystery of the town ¶ But my fortune telling sa…  
  → …d  / the mystery of the town. ¶ But my fortune telling sa…
- **#220** … / Are you from another town.  ¶ Hmm. You are from Norune…  
  → … / Are you from another town?  ¶ Hmm. You are from Norune…

## `gedit\e03\e03_1.mes`

- **#311** Freshen Up Watery /   Magical Watery /   Fighting Watery  
  → Freshen Up Watery /   Miracle Watery /   Warrior's Watery
- **#313** "Magical Watery"! ¶ Sounds neat!! / I'll go with that.  
  → "Miracle Watery"! ¶ Sounds neat!! / I'll go with that.
- **#314** "Fighting Watery"! ¶ That's cool!! / I'll go with that.  
  → "Warrior's Watery"! ¶ That's cool!! / I'll go with that.
- **#315** Please come visit  / "FreshenUp Watery" again.  
  → Please come visit  / "Freshen Up Watery" again.
- **#316** Please come visit my / "Magical Watery" again.  
  → Please come visit my / "Miracle Watery" again.
- **#317** Please come visit my / "Fighting Watery" again.  
  → Please come visit my / "Warrior's Watery" again.

## `gedit\e03\in\i03h06\i03h06_1.mes`

- **#101** I'll tell you your fortune / for free of charge. / Come visit me anytime.  
  → I'll tell you your fortune / free of charge. / Come visit me anytime.

## `gedit\e03\in\i03h09\i03h09_1.mes`

- **#100** Stew, seems like someone's at the door. ¶ Could it be the boy who restored / my gorgeous mansion ?  
  → Stu, seems like someone's at the door. ¶ Could it be the boy who restored / my gorgeous mansion?

## `gedit\e04\e04talk_1.mes`

- **#29** … other lives over the Muska desert,  / the Scorpion Warri…  
  → … other lives over the Muska Desert,  / the Scorpion Warri…
- **#30** … great ancient king,  / Bamirumba Hamorumba resting  / in… · …ient king,  / Bamirumba Hamorumba resting  / in the Sun &…  
  → … great ancient king,  / Bamilumba Hamolumba resting  / in… · …ient king,  / Bamilumba Hamolumba resting  / in the Sun &…
- **#80** How do you do. Welcome to Muska Lacka.  / I am Mikara, the baby of the 3 sisters.  / It is a pleasure to meet you .  
  → How do you do. Welcome to Muska Lacka.  / I am Mikara, the baby of the 3 sisters.  / It is a pleasure to meet you.
- **#131** According to some rumors there's a  / walking coffin in the Sun & Moon temple.  / Yeeeek!! That's soooooo scary!  
  → According to some rumors there's a  / walking coffin in the Sun & Moon Temple.  / Yeeeek!! That's soooooo scary!
- **#171** …anyway. / This's my typical  unconfirmed info. / Sorry ab…  
  → …anyway. / This's my typical unconfirmed info. / Sorry ab…
- **#191** …a / is the chief. ¶ Too bad  for Muska that there's  / no…  
  → …a / is the chief. ¶ Too bad for Muska that there's  / no…
- **#209** They say  fish live in the / Oasis. I wonder if it's true. / Hard to believe, really.  
  → They say fish live in the / Oasis. I wonder if it's true. / Hard to believe, really.

## `gedit\e04\e04_1.mes`

- **#113** …I was never  / really a warror. ¶ I'm better at kicking …  
  → …I was never  / really a warrior. ¶ I'm better at kicking …
- **#202** It's the end of my misson here. ¶ I'm sure [Ungaga] will be a great / help on your quest, [Toan]. ¶ So long.  
  → It's the end of my mission here. ¶ I'm sure [Ungaga] will be a great / help on your quest, [Toan]. ¶ So long.
- **#209** Make sure you beat that whimpy Genie!  
  → Make sure you beat that wimpy Genie!

## `gedit\e04\in\i04h04\i04h04_1.mes`

- **#161** I forgot  this. Real strength / doesn…  
  → I forgot this. Real strength / doesn…

## `gedit\e05\e05talk_1.mes`

- **#52** Finally, our Sun Giant has returned .  
  → Finally, our Sun Giant has returned.
- **#90** …wns other / than Yellow Drop on the Moon. ¶ I've never b…  
  → …wns other / than Yellow Drops on the Moon. ¶ I've never b…

## `gedit\e05\e05_1.mes`

- **#304** …etition is held. ¶ Finally,  we're moments away from  / s…  
  → …etition is held. ¶ Finally, we're moments away from  / s…

## `gedit\s01\s01_1.mes`

- **#508** No! that's not true!  
  → No! That's not true!

## `gedit\s01\s01talk_1.mes`

- **#20** What do you want?  / Go home. You're too  / much of a whimp for me.  
  → What do you want?  / Go home. You're too  / much of a wimp for me.
- **#520** …came with me  / quietly left .  ¶ I remember the sadness …  
  → …came with me  / quietly left.  ¶ I remember the sadness …

## `gedit\s03\s03_1.mes`

- **#10912** Well that is what  you have to find out. / Cha…  
  → Well that is what you have to find out. / Cha…

## `gedit\s04\s04talk_1.mes`

- **#81** I can't believe that Mayor Nemu  / is helping humans.  
  → I can't believe that Mayor Nem  / is helping humans.
- **#100** Hey, don't talk to me! / I'll get in trouble with Mr. Nemu.  
  → Hey, don't talk to me! / I'll get in trouble with Mr. Nem.
- **#221** Even if Mr. Nemu did,  / I'll never approve of humans!  ¶ Humans! Hmph!  
  → Even if Mr. Nem did,  / I'll never approve of humans!  ¶ Humans! Hmph!

## `gedit\s04\in\s05\s05_1.mes`

- **#205** …ba Hamolumba went to / his enternal rest. ¶ If anything h…  
  → …ba Hamolumba went to / his eternal rest. ¶ If anything h…

## `gedit\s04\in\s06\s06_1.mes`

- **#113** …to return. ¶ We have a ship  to take us to the moon,  / h…  
  → …to return. ¶ We have a ship to take us to the moon,  / h…

## `gedit\s11\s11_1.mes`

- **#101** Welcome to Yellow Drop!  
  → Welcome to Yellow Drops!

## `gedit\s13\s13talk_1.mes`

- **#120** …articular tradition for ages .  ¶ But the Boss completely…  
  → …articular tradition for ages.  ¶ But the Boss completely…
- **#220** Have we met before? / Couldn't have, I guess .  
  → Have we met before? / Couldn't have, I guess.

## `gedit\s15\s15_1.mes`

- **#11** All at once, the cavaliers advance. / I witness an fearsome scene unfold  / before me.  Is this war..?  
  → All at once, the cavaliers advance. / I witness a fearsome scene unfold  / before me.  Is this war..?
- **#32** Sadness, anger and hatred at losing  / you. This endless blackness is the  / energy that feeds him .  
  → Sadness, anger and hatred at losing  / you. This endless blackness is the  / energy that feeds him.

## `gedit\s28\s28_1.mes`

- **#412** My heart, filled with greif, began / to change. Something was born  / inside me.  
  → My heart, filled with grief, began / to change. Something was born  / inside me.

## `gedit\s32\s32_1.mes`

- **#253** I made an oath to / fight for Mikala. / But I failed to save Mikala...  
  → I made an oath to / fight for Mikara. / But I failed to save Mikara...
- **#254** Mikala? / [Ungaga], is she / the one you care for?  
  → Mikara? / [Ungaga], is she / the one you care for?
- **#255** Mikala is my fiancee. / I loved her.  
  → Mikara is my fiancee. / I loved her.
- **#256** Mikala is dead?  
  → Mikara is dead?
- **#258** Oh, I see..., so that's why / you are so down. ¶ Well let me tell you.  I assure you, / Mikala is alive.  
  → Oh, I see..., so that's why / you are so down. ¶ Well let me tell you.  I assure you, / Mikara is alive.

## `gedit\s34\s34talk_1.mes`

- **#202** (Huff, huff, huff...) / Sorry... I think  / I'm a bit  late.  
  → (Huff, huff, huff...) / Sorry... I think  / I'm a bit late.

## `gedit\s39\s39_1.mes`

- **#209** It is not too late. / Call out  for Sophia's  / wandering soul!  
  → It is not too late. / Call out for Sophia's  / wandering soul!

## `gedit\s90\s90_1.mes`

- **#608** Okay, let's go back to / Yellow Drop, now.  
  → Okay, let's go back to / Yellow Drops, now.

## `gedit\s98\s98_1.mes`

- **#161** … of Beasts!? / What an embarassment. ¶ Hey kid, sorry ab…  
  → … of Beasts!? / What an embarrassment. ¶ Hey kid, sorry ab…

## `gedit\s99\s99_1.mes`

- **#124** I  need that stone. More than …  
  → I need that stone. More than …
- **#2205** There are also special  floors that  / are called Limited Zones.  
  → There are also special floors that  / are called Limited Zones.
- **#2209** That's it. Got it? / Well, if not  you'll get the  / hang of it as you go along.  
  → That's it. Got it? / Well, if not you'll get the  / hang of it as you go along.

## `gedit\system\editsys.bin`

- **#1005** Dike  
  → Pike
- **#1009** Xena  
  → Gina
- **#1100** AncientBaron  
  → Ancient Baron
- **#1207** Stew  
  → Stu
- **#2406** Strage Guard  
  → Storage Guard
- **#2410** Suger  
  → Sugar
- **#3308** Marnia  
  → Mahnia

## `gedit\system\editsys_1.mes`

- **#1100** AncientBaron  
  → Ancient Baron
- **#2410** Suger  
  → Sugar
- **#3308** Marnia  
  → Mahnia

## `meswin\system14e.bin`

- **#161** Halbert  
  → Halberd
- **#317** Moustache Key  
  → Mustache Key
- **#1203** Kye&Momo's House  
  → Kye & Momo's House
- **#1286** Mr. Moustache  
  → Mr. Mustache
- **#1410** LeaningTower  
  → Leaning Tower
- **#1415** Freshen Up Washery  
  → Freshen Up Watery
- **#1430** Freshen Up Washery  
  → Freshen Up Watery
- **#1431** Miracle Washery  
  → Miracle Watery
- **#1432** Warrior'sWashery  
  → Warrior's Watery
- **#1487** Stew  
  → Stu
- **#1601** Jibubu's Hose  
  → Jibubu's House
- **#1603** 3sisters' House  
  → 3 Sisters' House

## `meswin\systeme.bin`

- **#161** Halbert  
  → Halberd
- **#317** Moustache Key  
  → Mustache Key
- **#1203** Kye&Momo's House  
  → Kye & Momo's House
- **#1286** Mr. Moustache  
  → Mr. Mustache
- **#1410** LeaningTower  
  → Leaning Tower
- **#1415** Freshen Up Washery  
  → Freshen Up Watery
- **#1430** Freshen Up Washery  
  → Freshen Up Watery
- **#1431** Miracle Washery  
  → Miracle Watery
- **#1432** Warrior'sWashery  
  → Warrior's Watery
- **#1487** Stew  
  → Stu
- **#1601** Jibubu's Hose  
  → Jibubu's House
- **#1603** 3sisters' House  
  → 3 Sisters' House

## `meswin\system_ae.bin`

- **#72** Bag is full.Can't / carry any more.  
  → Bag is full. Can't / carry any more.

## `meswin\sysarticle_1.mes`

- **#72** Bag is full.Can't / carry any more.  
  → Bag is full. Can't / carry any more.

## `meswin\system14_1.mes`

- **#317** Moustache Key  
  → Mustache Key
- **#1203** Kye&Momo's House  
  → Kye & Momo's House
- **#1286** Mr. Moustache  
  → Mr. Mustache
- **#1410** LeaningTower  
  → Leaning Tower
- **#1415** Freshen Up Washery  
  → Freshen Up Watery
- **#1430** Freshen Up Washery  
  → Freshen Up Watery
- **#1431** Miracle Washery  
  → Miracle Watery
- **#1432** Warrior'sWashery  
  → Warrior's Watery
- **#1487** Stew  
  → Stu
- **#1601** Jibubu's Hose  
  → Jibubu's House
- **#1603** 3sisters' House  
  → 3 Sisters' House

## `meswin\system_1.mes`

- **#317** Moustache Key  
  → Mustache Key
- **#1203** Kye&Momo's House  
  → Kye & Momo's House
- **#1286** Mr. Moustache  
  → Mr. Mustache
- **#1410** LeaningTower  
  → Leaning Tower
- **#1415** Freshen Up Washery  
  → Freshen Up Watery
- **#1430** Freshen Up Washery  
  → Freshen Up Watery
- **#1431** Miracle Washery  
  → Miracle Watery
- **#1432** Warrior'sWashery  
  → Warrior's Watery
- **#1487** Stew  
  → Stu
- **#1601** Jibubu's Hose  
  → Jibubu's House
- **#1603** 3sisters' House  
  → 3 Sisters' House

## Unused copies

The USA executable never loads these JP-era banks; the step patches them for completeness.

### `meswin\_systeme.bin`

- **#161** Halbert  
  → Halberd
- **#317** Moustache Key  
  → Mustache Key
- **#1085** Dike  
  → Pike
- **#1089** Xena  
  → Gina
- **#1203** Kye&Momo's House  
  → Kye & Momo's House
- **#1258** AncientBaron  
  → Ancient Baron
- **#1280** AncientBaron  
  → Ancient Baron
- **#1286** Mr.Moustache  
  → Mr. Mustache
- **#1410** LeaningTower  
  → Leaning Tower
- **#1431** Miracle Washery  
  → Miracle Watery
- **#1487** Stew  
  → Stu
- **#1601** Jibubu's Hose  
  → Jibubu's House
- **#1603** 3sisters' House  
  → 3 Sisters' House

### `meswin\_system14e.bin`

- **#161** Halbert  
  → Halberd
- **#317** Moustache Key  
  → Mustache Key
- **#1085** Dike  
  → Pike
- **#1089** Xena  
  → Gina
- **#1203** Kye&Momo's House  
  → Kye & Momo's House
- **#1258** AncientBaron  
  → Ancient Baron
- **#1280** AncientBaron  
  → Ancient Baron
- **#1286** Mr.Moustache  
  → Mr. Mustache
- **#1410** LeaningTower  
  → Leaning Tower
- **#1431** Miracle Washery  
  → Miracle Watery
- **#1487** Stew  
  → Stu
- **#1601** Jibubu's Hose  
  → Jibubu's House
- **#1603** 3sisters' House  
  → 3 Sisters' House
