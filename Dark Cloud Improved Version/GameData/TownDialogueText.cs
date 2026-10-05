namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The authored town dialogue: what each ally hears from every talkable NPC in Norune, Matataki, Queens,
    /// Muska Lacka, the Sun &amp; Moon Temple outside, Yellow Drops, Brownboo and Dark Heaven Castle, the
    /// "It's finished!" answers, and Pickle's greeting. Pure data; <see cref="Dialogues"/> picks and writes it.
    /// <see cref="Initialize"/> (re)fills every slot and runs at each game-loop start, which also undoes the one
    /// in-place edit the writer makes (Dark Heaven's line lands in slot 0 of whichever array is current).
    ///
    /// Text markup: Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond; Ȟ = heart symbol;
    /// ^ = next line, ¤ = next dialogue bubble; 40 symbols max per line, more than that can clip dialogue.
    /// </summary>
    internal static class TownDialogueText
    {
        /// <summary>Talk-menu option lines; [0] Norune, [1] Matataki (SetDialogue keeps the current town's as its option text).</summary>
        internal static readonly string[] SideQuestDialogueOption = new string[15];
        /// <summary>Pickle's (Brownboo) collection-quest greeting and its progress template.</summary>
        internal static string BrownbooPickle;
        internal static string BrownbooPickleData;

        // Per town x ally: first-talk lines and their second-talk lines (<name>2), indexed by the town's NPC order
        // (Dialogues.<town>Characters). Length 15 everywhere: SetDialogue indexes them with the villager slot /
        // NPC id, so the unfilled tail must stay in place.
        internal static readonly string[] NoruneXiao = new string[15];
        internal static readonly string[] NoruneXiao2 = new string[15];
        internal static readonly string[] NoruneGoro = new string[15];
        internal static readonly string[] NoruneGoro2 = new string[15];
        internal static readonly string[] NoruneRuby = new string[15];
        internal static readonly string[] NoruneRuby2 = new string[15];
        internal static readonly string[] NoruneUngaga = new string[15];
        internal static readonly string[] NoruneUngaga2 = new string[15];
        internal static readonly string[] NoruneOsmond = new string[15];
        internal static readonly string[] NoruneOsmond2 = new string[15];
        internal static readonly string[] MatatakiXiao = new string[15];
        internal static readonly string[] MatatakiXiao2 = new string[15];
        internal static readonly string[] MatatakiGoro = new string[15];
        internal static readonly string[] MatatakiGoro2 = new string[15];
        internal static readonly string[] MatatakiRuby = new string[15];
        internal static readonly string[] MatatakiRuby2 = new string[15];
        internal static readonly string[] MatatakiUngaga = new string[15];
        internal static readonly string[] MatatakiUngaga2 = new string[15];
        internal static readonly string[] MatatakiOsmond = new string[15];
        internal static readonly string[] MatatakiOsmond2 = new string[15];
        internal static readonly string[] QueensXiao = new string[15];
        internal static readonly string[] QueensXiao2 = new string[15];
        internal static readonly string[] QueensGoro = new string[15];
        internal static readonly string[] QueensGoro2 = new string[15];
        internal static readonly string[] QueensRuby = new string[15];
        internal static readonly string[] QueensRuby2 = new string[15];
        internal static readonly string[] QueensUngaga = new string[15];
        internal static readonly string[] QueensUngaga2 = new string[15];
        internal static readonly string[] QueensOsmond = new string[15];
        internal static readonly string[] QueensOsmond2 = new string[15];
        internal static readonly string[] MuskarackaXiao = new string[15];
        internal static readonly string[] MuskarackaXiao2 = new string[15];
        internal static readonly string[] MuskarackaGoro = new string[15];
        internal static readonly string[] MuskarackaGoro2 = new string[15];
        internal static readonly string[] MuskarackaRuby = new string[15];
        internal static readonly string[] MuskarackaRuby2 = new string[15];
        internal static readonly string[] MuskarackaUngaga = new string[15];
        internal static readonly string[] MuskarackaUngaga2 = new string[15];
        internal static readonly string[] MuskarackaOsmond = new string[15];
        internal static readonly string[] MuskarackaOsmond2 = new string[15];
        internal static readonly string[] SunmoonXiao = new string[15];
        internal static readonly string[] SunmoonXiao2 = new string[15];
        internal static readonly string[] SunmoonGoro = new string[15];
        internal static readonly string[] SunmoonGoro2 = new string[15];
        internal static readonly string[] SunmoonRuby = new string[15];
        internal static readonly string[] SunmoonRuby2 = new string[15];
        internal static readonly string[] SunmoonUngaga = new string[15];
        internal static readonly string[] SunmoonUngaga2 = new string[15];
        internal static readonly string[] SunmoonOsmond = new string[15];
        internal static readonly string[] SunmoonOsmond2 = new string[15];
        internal static readonly string[] YellowdropsXiao = new string[15];
        internal static readonly string[] YellowdropsXiao2 = new string[15];
        internal static readonly string[] YellowdropsGoro = new string[15];
        internal static readonly string[] YellowdropsGoro2 = new string[15];
        internal static readonly string[] YellowdropsRuby = new string[15];
        internal static readonly string[] YellowdropsRuby2 = new string[15];
        internal static readonly string[] YellowdropsUngaga = new string[15];
        internal static readonly string[] YellowdropsUngaga2 = new string[15];
        internal static readonly string[] YellowdropsOsmond = new string[15];
        internal static readonly string[] YellowdropsOsmond2 = new string[15];
        internal static readonly string[] BrownbooXiao = new string[15];
        internal static readonly string[] BrownbooXiao2 = new string[15];
        internal static readonly string[] BrownbooGoro = new string[15];
        internal static readonly string[] BrownbooGoro2 = new string[15];
        internal static readonly string[] BrownbooRuby = new string[15];
        internal static readonly string[] BrownbooRuby2 = new string[15];
        internal static readonly string[] BrownbooUngaga = new string[15];
        internal static readonly string[] BrownbooUngaga2 = new string[15];
        internal static readonly string[] BrownbooOsmond = new string[15];
        internal static readonly string[] BrownbooOsmond2 = new string[15];
        // Dark Heaven Castle has a single NPC line per ally.
        internal static string DarkheavenXiao;
        internal static string DarkheavenXiao2;
        internal static string DarkheavenGoro;
        internal static string DarkheavenGoro2;
        internal static string DarkheavenRuby;
        internal static string DarkheavenRuby2;
        internal static string DarkheavenUngaga;
        internal static string DarkheavenUngaga2;
        internal static string DarkheavenOsmond;
        internal static string DarkheavenOsmond2;

        // "It's finished!" talk-option answers, per town NPC.
        internal static readonly string[] NoruneFinishedDialogue = new string[15];
        internal static readonly string[] MatatakiFinishedDialogue = new string[15];
        internal static readonly string[] QueensFinishedDialogue = new string[15];
        internal static readonly string[] MuskaFinishedDialogue = new string[15];

        //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
        // Ȟ = heart symbol
        // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue

        /// <summary>
        /// Applies the new dialogues for all NPCs that allies can talk to
        /// </summary>
        internal static void Initialize()
        {
            SideQuestDialogueOption[0] = "Hello.^  How should I rebuild Norune?^  It´s finished!^  Do you have any sidequests?";
            SideQuestDialogueOption[1] = "Hello.^  How should I rebuild Matataki Village?^  It´s finished!^  Do you have any sidequests?";

            BrownbooPickle = "Hey there, wanderer! Would you like^to know your collection progress?^Whenever you talk to me, I´ll check^your obtained items and weapons.¤You need to be either carrying them^or have them in your storage.¤Can you make it to the 100% collection?^It won´t be easy, but if you commit^to it, you can achieve anything!^Good luck!";
            BrownbooPickleData = "You have collected:^X / Y obtainable items^X / Y obtainable weapons";

            //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag, mayor
            NoruneXiao[0] = "El Gato, how are you doing little buddy?^Need any food or water?";
            NoruneXiao[1] = "In all my travels around Terra, I have^never seen a cat like you.¤Anyone with eyes can see that there is^something special about you Ӿ!";
            NoruneXiao[2] = "Kitty, were you stuck in a bubble too?";
            NoruneXiao[3] = "You´re a cute little cat! Oh no, are you^a stray? You should come live with us!";
            NoruneXiao[4] = "Look at you, you are so cute^unlike those muscle freaks!";
            NoruneXiao[5] = "I remember when Paige was a little girl,^her mother made her a plush cat^that looked just like you!¤Kids sure do grow up fast, people may^come and go but the memories we make^with out loved ones are eternal.¤Pretty poetic for a fisherman huh, hahaha!";
            NoruneXiao[6] = "Sometimes it´s hard to be an older brother.^Macho and I may butt heads but^deep down we care about each other.^Do you have any siblings kitty?";
            NoruneXiao[7] = "No way, I can´t believe Ť let´s^you go on adventures with him.^You are so lucky!";
            NoruneXiao[8] = "Hey there, I´ve been seeing you^hang out with Ť! Are you^his little sidekick now? Ȟ";
            NoruneXiao[9] = "I appreciate how you are helping Ť^on his journey, I still wonder where^he got that change potion.¤It´s hard for one person to try change^the world but if we all work together^as allies, anything is possible. Don´t^worry, I won´t tell anyone your secret Ȟ";
            NoruneXiao[10] = "Are you helping Ť with^fixing other people´s houses?¤Since you´re helping people can^you help me, I have a bit of a mouse^problem. I could pay you in food!";
            NoruneXiao[11] = "They say animals have sharper senses^then that of humans, can you feel^the magic within this village?¤I sense something different about you^Ӿ, perhaps you´re no ordinary cat!";
            NoruneXiao[12] = "Hmm I wonder where you came from,^I haven´t seen any other cats in this village.";

            NoruneXiao2[0] = "I see you following Ť everywhere,^I bet you taught him everything he knows!¤Next time I go training in the Cave, I^gotta bring you with me to watch my back!";
            NoruneXiao2[1] = "If anyone understands the troubles^that come with being a wanderer it´s me.¤If you ever need a place^to stay, there is always a^spot for you in my buggy Ӿ.";
            NoruneXiao2[2] = "Let´s have a race kitty, I bet^I can run faster than you!";
            NoruneXiao2[3] = "Gina would love to have you as a playmate,^the poor girl gets lonely now that^Ť is too busy to play with her.";
            NoruneXiao2[4] = "If you are ever hungry you should go^to Uncle Pike, he always has spare fish!";
            NoruneXiao2[5] = "Have you ever heard of the legendary^fish Mardan Garayan, knowing my^luck you probably ate one before!";
            NoruneXiao2[6] = "I remember there was once a luchador who^wore a red lion mask who stood for justice^and good, he was as quick as thunder.¤He fought for many decades before^he was finally defeated by evil.^They say he fought valiantly to^the bitter end that even those evil¤doers respected his resolve. I´m sure^there are many who are following^the footsteps of that hero.";
            NoruneXiao2[7] = "It´s no fair, I always wanted a pet cat^but my sister doesn´t let me have one^after I had that accident with my^pet fish Uncle Pike gave me...";
            NoruneXiao2[8] = "Ť may act like a tough guy but^he doesn´t always use his head.^Sometimes I really worry about him.¤It´s not like I can go with him^you know. You can though, I see^you following him around everywhere.¤Can you do me a favor and make^sure he stays out of trouble?^Thanks Ӿ, I appreciate it.";
            NoruneXiao2[9] = "Is Ť taking proper care of you?^That boy has a good heart but sometimes^he works way too hard, he gets^that habit from his father.";
            NoruneXiao2[10] = "What´s your favorite food kitty, I bet^having cat food everyday gets old.";
            NoruneXiao2[11] = "Oh-hohoho have you come to live^with me, I guess this old Hag^could use a cat by her side!";
            NoruneXiao2[12] = "If the village ever has a rat problem^would you be willing to take a break^from following Ť around to^help the rest of the village?";

            //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag, mayor
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            NoruneGoro[0] = "Hey bro, carrying that heavy mallet^around seems like one heck of^a workout, I should try it sometime.";
            NoruneGoro[1] = "Ah yes, Matataki Village,^that´s not too far from here.¤I avoid doing business in that area^since there are huge hornets not too far^from Matataki.";
            NoruneGoro[2] = "Wow, you´re from Matataki Village,^my Daddy works there!";
            NoruneGoro[3] = "Oh so you are from Matataki Village?^My husband works as a traveling^merchant there!";
            NoruneGoro[4] = "My goodness you look just like Claude,^were you two separated at birth?";
            NoruneGoro[5] = "Look at you, a hunter!^You´re talking to a true blue fisherman^so I guess I´m a bit of a hunter myself.";
            NoruneGoro[6] = "So you´re a hunter from Matataki?^Do you think you´re so tough?^Let me tell you something, I´m strong.¤I bet you that I´m stronger than your^best hunter at Matataki mwahaha!";
            NoruneGoro[7] = "Hmm, I wonder what Alnet would say^if I brought home a mallet like that.";
            NoruneGoro[8] = "I heard you are from Matataki Village,^I´ve never been there but I know Auntie^Laura´s husband has traveled there¤in the past for work.^I wonder if you know him?";
            NoruneGoro[9] = "Hello there, you´re another one of^Toan´s friends! Do you ever get tired^from always carrying that mallet around?";
            NoruneGoro[10] = "Everyone in the village says I look^just like you, you should give me^your bearskin hood¤and we can play a prank on all of Norune!";
            NoruneGoro[11] = "I´ve heard that Matataki Village has^magical fairies called Laughapockle,^these seven fairies can be quite^mischievous!";
            NoruneGoro[12] = "I´ve heard you can find a monster named^King Prickly around Matataki Village,¤I´ve been having an issue with Pricklies^coming in my home so I can´t imagine^a King Prickly!";

            NoruneGoro2[0] = "You´re all pudgy just like Claude^but they say you´re a powerful hunter,^we should go down to the^cave and train together.";
            NoruneGoro2[1] = "I´ve heard of a very frugal shopkeeper^by the name of Mr.Mustache in Matataki,^I hope you find my prices more fair.";
            NoruneGoro2[2] = "I think carrying that big heavy hammer^around made you short haha!";
            NoruneGoro2[3] = "Haha, finally there´s someone in the^village who is the same height as my^little Gina!";
            NoruneGoro2[4] = "You look a little taller than Carl,^is everyone from Matataki Village^so short?";
            NoruneGoro2[5] = "Oh, you´ve come from Matataki Village?^I wonder what kind of fish you can^find over there.";
            NoruneGoro2[6] = "Norune Village is too quiet sometimes.^Our villagers don´t put much value in^exercise or body strength, they just^live care free.¤I think the other villagers could learn^something from you hunters of Matataki.";
            NoruneGoro2[7] = "Matataki Village sounds like a weird^place, you´re telling me everyone wears^animal skins there?";
            NoruneGoro2[8] = "When I first saw you I thought^you were Claude, another villager who^lives not too far from here.¤It´s almost like you could be twins,^perhaps distant relatives!";
            NoruneGoro2[9] = "I know you like wearing animal skins^but please don´t do anything to our^Llama, we need it for milk and cheese.";
            NoruneGoro2[10] = "I wonder what kind of food can be found^in Matataki Village, can you bring some^back for me?";
            NoruneGoro2[11] = "Animals are essential to our^world´s spirits. I cannot condone your^village´s hunting practices¤but I guess to each is they´re own.";
            NoruneGoro2[12] = "Dran is a magical beast that defends^our village of Norune, tell the beast^hunters of Matataki that he is a gentle,¤well-mannered monster and not to^be hunted.";


            //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag, mayor
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            NoruneRuby[0] = "Who would have guessed that a genie^would be helping Ť on his^adventure!";
            NoruneRuby[1] = "Ah yes, Queens, that´s a name I have^not heard in a while. I have not visited^that port town in years!";
            NoruneRuby[2] = "Your purple hair is pretty!";
            NoruneRuby[3] = "All of the kids in the village are^talking about you, it wouldn´t hurt to^show them a magic trick or two!";
            NoruneRuby[4] = "I know you are traveling with Ť^but you better not get any ideas sister,^Paige has a crush on him so you better^back off!";
            NoruneRuby[5] = "I´ve heard about you Ʀ,^they say that you are trying to be^the best genie in the world!¤Do you know of any spells to make the^legendary Mardan Garayan appear?";
            NoruneRuby[6] = "The other villagers say that you are a^genie. I hope you haven´t come back to^put us into those weird bubbles again.";
            NoruneRuby[7] = "I heard Uncle Pike saying that he wants^to go on a fishing trip to Queens.^I hope he takes me!";
            NoruneRuby[8] = "Wow I love your clothes!^Before the Dark Genie attacked our^village I was celebrating the¤Star festival, it was really scary!";
            NoruneRuby[9] = "Hello, you met Ť in Queens?^It´s nice that you are lending your^magical abilities to Ť and^the rest of his allies!";
            NoruneRuby[10] = "It´s cool that you are a genie, can you^do spells? Can you make 100 plates of^premium chicken appear out of thin^air through magic?";
            NoruneRuby[11] = "Oh-ho-ho-ho I may look like an old^fossil but there was once a time where^I was just as beautiful as^you Ʀ.";
            NoruneRuby[12] = "The other villagers told me you are^from Queens, I remember hearing a story^about how your Queen ran away to be^with her love.¤Who is leading your city now?";

            NoruneRuby2[0] = "I heard that you prefer to use magic^instead of your fists.¤Now I don´t know about that but we^should train together sometime!";
            NoruneRuby2[1] = "I remember Queens was a bustling port^town, has it expanded since?";
            NoruneRuby2[2] = "Woah! You can you use magic!?^You have to teach Gina!";
            NoruneRuby2[3] = "Gina says that when she grows up she^wants purple hair just like you!^It looks like you are her role model^now.";
            NoruneRuby2[4] = "Paige and I practice dancing every week,^you should join us one day!";
            NoruneRuby2[5] = "No way, so your from Queens!?^That port town is every fisherman´s^dream!";
            NoruneRuby2[6] = "I could tell that you are from Queens^just by looking at you.¤I have family living there that^specialize in law enforcement.";
            NoruneRuby2[7] = "I heard Queens is surrounded by water,^I almost drowned once when Uncle Pike^let me borrow his fishing rod.";
            NoruneRuby2[8] = "I remember hearing an old story from^Queens about its ruler La Saia falling^in love with a commoner.¤They say they ran away to be together^forever, how romantic Ȟ"; /*<-- Insert <3 to replace the '!' */
            NoruneRuby2[9] = "You said you want to be the best genie,^I wonder if there are more genies^out there.";
            NoruneRuby2[10] = "Is it true that the food in Queens is^the best in the world?";
            NoruneRuby2[11] = "I heard that you are on a quest to be^the best genie in the world, I know it^may be a daunting task but come to me^if you need any advice.";
            NoruneRuby2[12] = "Dran is an all-powerful beast that^helps protect Norune. I hope he^will help Ť and you all^on your adventure.";


            //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag, mayor
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            NoruneUngaga[0] = "Woah bro, you look strong! I´d love to^spar with you and see what you can do!";
            NoruneUngaga[1] = "The villagers of Muska Lacka must be^resourceful people if they are^living in a barren desert.";
            NoruneUngaga[2] = "Yay it´s Mr. Ų, I wish I^can be as tall as you one day!";
            NoruneUngaga[3] = "Watch where you point that staff,^you might poke your eye out!";
            NoruneUngaga[4] = "Your clothes look really interesting,^is that how all warriors^from Muska Lacka dress like?";
            NoruneUngaga[5] = "It must be difficult to live in^such a harsh terrain, is it possible^to fish in Muska Lacka?";
            NoruneUngaga[6] = "It´s nice to see that someone from^so far away is helping Ť^on his quest. You are a good man.";
            NoruneUngaga[7] = "Look at you, you´re almost as^tall as those muscle brothers!¤Wow, what is your secret?";
            NoruneUngaga[8] = "I bet with that staff you could^be an excellent shepherd, we have^plenty of llamas here in Norune!";
            NoruneUngaga[9] = "Thank you for helping my son on his^journey, there is a saying that it^takes a village to raise a child.¤He will learn a lot from^all of his new allies!";
            NoruneUngaga[10] = "What, your favorite food is^scorpion jerky?! Say it isn´t so!";
            NoruneUngaga[11] = "Oh, a sand warrior from the far^off village of Muska Lacka has come^to visit?¤The Sun and Moon Temple is a source^of magic power for your village much^like Dran´s Windmill is for Norune.";
            NoruneUngaga[12] = "I am glad strong warriors like you^are helping Ť on his quest,^we will defeat the genie!";

            NoruneUngaga2[0] = "I´m glad someone strong like you^is helping Ť on his journey,^he needs all the muscle he can get!";
            NoruneUngaga2[1] = "I heard in Muska Lacka that the^sand warriors are at war with the^scorpion tribe, is that true?";
            NoruneUngaga2[2] = "I have been picking up all^of the sticks I find to^make a staff like you!";
            NoruneUngaga2[3] = "Gina has been picking up twigs^and sticks and saying that she^is the great warrior Ų¤Children are so impressionable!";
            NoruneUngaga2[4] = "You are much more polite then^those muscle brothers!";
            NoruneUngaga2[5] = "Every fisherman´s dream is the Mardan^Garayan, if you see one let me know.";
            NoruneUngaga2[6] = "While you are on your quest, make^sure to try and take a break too, life^is all about the work rest balance.";
            NoruneUngaga2[7] = "I hope Alnet would let me use a staff^one day, maybe I can defend^Norune just like Ť!";
            NoruneUngaga2[8] = "All of the villagers complain about^the heat in our village, I can´t^imagine living in a desert!";
            NoruneUngaga2[9] = "Has Ť been taking care of^his health, the last thing^I need is him getting sick!";
            NoruneUngaga2[10] = "Is it true that Muska Lacka is a big^desert? How do you grow food?";
            NoruneUngaga2[11] = "Looking at your eyes I can tell^that you are a strong person who^has conquered many challenges.¤May the Dark Genie be one more^challenge for you to overcome.";
            NoruneUngaga2[12] = "We all believe in you, the village of^Norune thanks you all for your help.";


            //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag, mayor
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            NoruneOsmond[0] = "Woah I hear that you are powerful^even though you are small!";
            NoruneOsmond[1] = "The dream of every merchant is to^sell their products across the world,^imagine selling items on the moon!";
            NoruneOsmond[2] = "Yay Bunny Boy is back! Can you tell^me more stories about the Moon?";
            NoruneOsmond[3] = "I´ve never seen someone who looks^like you before, you kind of^look like a bunny.";
            NoruneOsmond[4] = "Aren´t you a small little guy,^I wonder how you can float like that!";
            NoruneOsmond[5] = "I heard from other villagers that^you are from the moon, is it true^that there is a moon sea?";
            NoruneOsmond[6] = "I´m happy that even the moon knows^about Norune village, let them know^that we are the strongest on Terra.";
            NoruneOsmond[7] = "Is it true that you are from the Moon?^I heard from the other villagers^but I didn´t believe it!";
            NoruneOsmond[8] = "Wow I heard you are from the moon,^is that true?";
            NoruneOsmond[9] = "Thank you for helping Ť on^his journey, you have come from so^far away! It just goes to show that^if we are near or far, hard times¤bring people together. If we all^work together as allies,^anything is possible.";
            NoruneOsmond[10] = "You are telling me that you are^from the Moon? Is it true the Moon^is made from cheese?";
            NoruneOsmond[11] = "I know that the Moon People are well^versed in magic, yet you use a fire arm?^You confuse me young one.";
            NoruneOsmond[12] = "Hmm I wonder where you came from,^I haven´t seen any other bunnies^in this village.";

            NoruneOsmond2[0] = "If you take Ť to the moon^make sure he doesn´t get lost, he^has a bright future ahead of him!";
            NoruneOsmond2[1] = "If you are trying to get some items,^Gaffer´s Buggy is the best shop^in all of Terra!";
            NoruneOsmond2[2] = "How can you fly, is it magic?^Teach Gina how to fly too!";
            NoruneOsmond2[3] = "You are the same height as my Gina^but you are much older, I guess^you didn´t eat your vegetables!";
            NoruneOsmond2[4] = "If Ť goes to the Moon with^you remind him to get a souvenir for^Paige, that would be so romantic!";
            NoruneOsmond2[5] = "Imagine fishing on the moon hahaha!";
            NoruneOsmond2[6] = "If you ever need any carrots^you should talk to Gaffer.";
            NoruneOsmond2[7] = "It´s no fair, Ť gets to go^to the Moon while I´m stuck here^in boring Norune! ";
            NoruneOsmond2[8] = "I´m a little confused on where there^would be bunnies on the moon,^strange isn´t it!";
            NoruneOsmond2[9] = "Please make sure Ť behaves^himself when he is on the Moon,^he is a nice boy but he could^also be mischievous!¤He is growing up fast but he^will always be my boy.";
            NoruneOsmond2[10] = "Ť should take me to the Moon^one day, I bet I could live^like a king on the Moon!";
            NoruneOsmond2[11] = "There was something I always wanted^to ask a Moon Person, perhaps^you may have the answer.¤I heard whispers of a legendary tower^that floats in the sky by the name of^the Demon Shaft, who created that^and for what purpose?";
            NoruneOsmond2[12] = "What, you are from the Moon?! I see,^perhaps Dran knows something about the^people of the Moon. Norune Village^hopes that you feel welcome and at home!";



            //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MatatakiXiao[0] = "Hello there, you must be a^traveler since we don´t see many^cats around here.";
            MatatakiXiao[1] = "Ohohoh aren´t you a cute one!";
            MatatakiXiao[2] = "Oh my, what is a small cat like you^doing here?¤You better be careful in this village,^we hunt ferocious beasts but you seem^like a gentle kitty.";
            MatatakiXiao[3] = "Hey there don´t mind my scarf I´m a^friendly guy, I swear!";
            MatatakiXiao[4] = "Here kitty, I have a proposition for^you! If you could keep an eye on Momo^for me I´ll cook for you my specialty^manly cooking kitty cat miracle dish!";
            MatatakiXiao[5] = "If it isn´t a little itty bitty kitty^cat... Your cute appearance can´t^fool me, for I know your secret!¤You are actually the legendary white^tiger coming back to hunt us one by^one!";
            MatatakiXiao[6] = "Haha, seeing you brings back many^memories of my youth! After a long^fought battle, I conquered this lion^that I´m wearing but I´m sure you´re¤more tough than any king of the jungle^little one!";
            MatatakiXiao[7] = "Interesting, it´s rare seeing a cat^like you here. The last time we had a^feline come into the village was when¤the legendary white tiger came to^challenge Fudoh.¤I don´t believe in wearing any beast^skins, you can feel at ease when you´re^with me.";
            MatatakiXiao[8] = ".......Cute.......";
            MatatakiXiao[9] = "Oh wow, I can´t believe that there´s a^cat in the village, wait until Kululu^hears about this!";
            MatatakiXiao[10] = "Oh hello there kitty, I´m very happy to^see you I don´t always have visitors.^It´s very nice to meet you Ӿ!";
            MatatakiXiao[11] = "Hooo! You better not get any ideas cat,^I´m not going to be an easy meal!";

            MatatakiXiao2[0] = "Be careful around this village, the^hunters here are vigilant. I´ll be sure^to dismantle any traps around our house^for now, you are welcome here.";
            MatatakiXiao2[1] = "Poor little kitty do you have a home?^You shouldn´t be living in the forest.¤It could be dangerous here, if you need^anything feel free to come back, our^house is hard to miss!";
            MatatakiXiao2[2] = "Wow that´s such a pretty little bell,^I´d love to have that as a necklace!";
            MatatakiXiao2[3] = "It must be a bizarre feeling being in a^village like this... The villagers of^Matataki take pride in wearing the^skins and fur of the beasts they¤defeated in battle, each one was a^challenge we overcame. For us, these^skins and what they represent are a way^of life.";
            MatatakiXiao2[4] = "I wonder if cats like you can even^digest my manly cooking,¤hmm maybe you shouldn´t have it after^all!";
            MatatakiXiao2[5] = "Wait, perhaps I can use your power!¤Join me white tiger and together^we can rule the world.";
            MatatakiXiao2[6] = "Say I remember hearing that small cats^like you are common in our neighboring^village of Norune just north of our^Matataki.¤I wonder what brings you to these parts?";
            MatatakiXiao2[7] = "The story of Fudoh and his heroism is^so inspiring. Sometimes I get upset^that he is not here with us.¤I know that although he may not be here,^he will always be in our hearts.¤I´ve never been much of a hunter but^Fudoh continues to inspire me to be a^stronger person and to overcome my^challenges and make Fudoh proud.¤Perhaps one day, we will both be strong^little kitty.";
            MatatakiXiao2[8] = "......Here Kitty Kitty......";
            MatatakiXiao2[9] = "Beasts rarely come to the village^because they know that strong hunters^live here, like me!";
            MatatakiXiao2[10] = "Sometimes I wish I could leave this^house and see the outside world, only^sometimes though.¤Ӿ it would be nice if you could^visit from time to time and tell me^all about your adventures with your^friends, I would appreciate that.¤We could enjoy some fish candy^and share stories!!";
            MatatakiXiao2[11] = "If your hungry for some fish, I´m sure^I can give you a great deal!";


            //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MatatakiGoro[0] = "Why if it isn´t young Ʊ I´m^happy to see that you´ve come to join^the other villagers.";
            MatatakiGoro[1] = "My oh my, if it isn´t little Ʊ!^You´ve grown up but you still have those^chubby cheeks, come here and let your¤granny Annie give you a pinch for old^times´ sake!";
            MatatakiGoro[2] = "Hey there, who would have thought the^son of the legendary hunter would be so^handsome! Ȟ"; //Suppose to end with heart <3
            MatatakiGoro[3] = "It´s a surprise seeing you come outside^Ʊ we were worried about you.¤We wanted to reach out but at the same^time we knew that you needed your space.¤Know that you will always have a place^in this village, we are all family.";
            MatatakiGoro[4] = "Grrrr, I have reason to believe that^Momo has her eyes on you! What does she^see in you that I don´t have.¤I bet you don´t even know how to cook!";
            MatatakiGoro[5] = "You, you´re the boy who lives in the^tree house! Oh I heard a lot about you^yes!¤The Spirits said you would come, they^said you would save us all they did!";
            MatatakiGoro[6] = "Do my eyes deceive me?! What a surprise^but a welcome one to be sure! You´ve^become a man since I last saw you^Ʊ.";
            MatatakiGoro[7] = "Hello there Ʊ, you´ve grown^since I last saw you. I understand^how difficult things may be for you.¤Remember to hold your head up high and^always take pride in the life that was^given you.¤What happened to Fudoh was a tragedy but^that suffering shouldn´t define you,^you´ll always be your father´s son and^you already made him proud.¤I can´t wait to start writing songs^about the Legendary Hunter Ʊ,^Son of Fudoh.";
            MatatakiGoro[8] = "......You grew......";
            MatatakiGoro[9] = "Woaah, you´re that weird kid who never^comes out of his house!¤I remember when we were younger we would^play in the forest all the time!¤Haha do you remember when we would chase^after those Laughapockle? Good Times!";
            MatatakiGoro[10] = "The Spirits told me all about you^Ʊ¤I know the loneliness that comes with^being all alone.¤I´m happy that you found your purpose^and overcame that challenge.";
            MatatakiGoro[11] = "Look whooo decided to come out of^hiding! It´s about time, buy something^will ya!";

            MatatakiGoro2[0] = "I remember seeing your father Fudoh^teaching you how to hunt when you were^young, you were a natural.¤You definitely take after your old man^but you are a little chubby haha!";
            MatatakiGoro2[1] = "No matter how much you´ve grown you´ll^always be little Ʊ to me!";
            MatatakiGoro2[2] = "Once you and your friends are done^saving the world, we should go shopping^sometime!";
            MatatakiGoro2[3] = "I could tell that you´ve been training,^you´ve grown!¤Be careful when you are in the forest,^I heard there is a monster named King^Prickly that jumps out at you when you^least expect it!";
            MatatakiGoro2[4] = "Regardless of how I feel, men must put^their feelings aside. Best of luck on^your journey my friend.¤When you defeat that Dark Genie, I´ll^make a manly cooking feast in your^honor!";
            MatatakiGoro2[5] = "Ʊ I had a great respect for^your father Fudoh but he did leave one^thing undone...¤That man still owes me five Gilda, how^uncivilized leaving this world without^paying me!¤Feel free to pay your fathers debt at^your convenience son.";
            MatatakiGoro2[6] = "Grow strong Ʊ and rid this^world of the evil that plagues it.¤My time in this world is limited, this^village will require a new chief...¤Grow strong and make Fudoh proud, he will^always watch over you. May you watch^over us Legendary Hunter Ʊ.";
            MatatakiGoro2[7] = "Stay strong Ʊ all of^Matataki is cheering you on! One day you^will surpass even your father Fudoh,^always believe in yourself and never¤give up.^Let your might trample the eternal^forest beasts in your way brave hunter.";
            MatatakiGoro2[8] = "......I miss Fudoh, thank you for saving^us Ʊ......";
            MatatakiGoro2[9] = "Say, can I borrow that mallet you got^there?¤It would be handy for squashing those^pesky Earth Diggers!";
            MatatakiGoro2[10] = "Remember, if you ever need somewhere to^store your mallet collection come to me.";
            MatatakiGoro2[11] = "I wonder how much you would sell that^bear fur, it would make a nice rug.";


            //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MatatakiRuby[0] = "Greetings young lady and welcome to the^village of Matataki!";
            MatatakiRuby[1] = "Oh hello there young lady, I´ve seen you^helping Ʊ and Ť on^their quest!";
            MatatakiRuby[2] = "I love your outfit, it´s so glamorous!^I get tired of wearing the same beast^skins.¤Maybe we should go shopping together and^you could help me put together a cute^outfit!";
            MatatakiRuby[3] = "I don´t think I´ve ever seen you here,^it seems like Ť is gathering^allies from all over Terra!¤We are a proud village of hunters,^if you need any assistance with anything^let us know.";
            MatatakiRuby[4] = "I heard from some of the other villagers^that you´re from the port town of Queens,^how did you travel all the way over^here!?";
            MatatakiRuby[5] = "Ah if it isn´t my lovely granddaughter^Momo. My goodness, what did you do to^your hair!";
            MatatakiRuby[6] = "Hello there young lady, on behalf of all^of the Matataki villagers I wanted to^thank you for your assistance on^Ť and Ʊ´s quest.¤I guess not all magic is bad after all.";
            MatatakiRuby[7] = "You must be the friendly genie everyone^is talking about.¤This village is home to some of the best^hunters on Terra. To be honest, I have^never been one for fighting but magic^sounds interesting.";
            MatatakiRuby[8] = "...I can feel your magical energy...";
            MatatakiRuby[9] = "I never saw someone with purple hair^before!¤You say you´re from Queens, does^everyone from Queens have purple hair^and dress weird?";
            MatatakiRuby[10] = "I can feel the magical power emanating^from you, you must be one powerful^genie! I´m happy you´re on our side.";
            MatatakiRuby[11] = "Remember to buy more items to help you^on your quest. Genie or not you´ll need^all the help you can get!";

            MatatakiRuby2[0] = "Seeing you reminds me of Annie when she^was your age, oh she was the most^beautiful lady in the village.¤I remember getting into arguments with^Kye and Baron over her.¤In the end it was me who won her over^with my dashing good looks and smart wit^haha!";
            MatatakiRuby2[1] = "The other villagers say that you´re trying^to prove that you´re Terra´s greatest^genie.¤I think that is very admirable that^you´re also helping us along your way,^it´s like hitting two Fli Fli´s with one^stone!";
            MatatakiRuby2[2] = "Matataki´s way of life focuses on^hunting and comradery, although Queens^does sounds exciting!¤I´d love to visit a port town with lots^of shops!";
            MatatakiRuby2[3] = "The word in the village is that you´re a^powerful genie, I guess that explains^your odd clothes!";
            MatatakiRuby2[4] = "If you ever feel even the slightest bit^hungry feel free to enjoy my manly^cooking.¤Wait do genies get hungry like humans?";
            MatatakiRuby2[5] = "Momo, I know I may be a handful, I know^I may be silly but I wanted thank you^for taking care of me.";
            MatatakiRuby2[6] = "The magic and wonder of going on an^adventure, oh to be young again!";
            MatatakiRuby2[7] = "If you don´t mind me asking, do you know^of any spells to enhance one´s musical^ability?";
            MatatakiRuby2[8] = "......I like your hair......";
            MatatakiRuby2[9] = "Using magic sounds like it would make^hunting a breeze!¤Do you think you could teach me one day?";
            MatatakiRuby2[10] = "I´ve been trying to practice magic at^home to the best of my ability.¤I was able to turn my candy into Gilda!¤I made sure to transform it back because^Gilda doesn´t taste as good as delicious^candy.";
            MatatakiRuby2[11] = "I know you may be the best genie in the^world but that doesn´t change anything,^you have to pay like everyone else!";


            //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MatatakiUngaga[0] = "As the years go by, I find myself^thinking about my youth. The problems^we faced and the challenges we overcame^together.¤Annie was always with me through thick^and thin, I´m very thankful for that.¤Family and loved ones are important,^never forget that young man.";
            MatatakiUngaga[1] = "These days Ro has been very^introspective, this situation with the^Dark Genie must have really got to him. I^wish life can return to how it once was.¤At my age I´ve seen a lot of life, but^I´ve seen a lot of death as well,^Fudoh is proof of that.¤These are feelings that I wish no one^else has to go through. I know that^things will get better in time,^thank you for the help.";
            MatatakiUngaga[2] = "I never seen you in this village,^what brings you here?¤Oh you´re assisting Ť and^Ʊ on their quest,^that´s amazing!";
            MatatakiUngaga[3] = "Welcome to Matataki Village, I heard^the other villagers talking about a tall^warrior who is aiding Ť^on his quest.¤I can tell you are strong,^they need all the help they can get.";
            MatatakiUngaga[4] = "Woah brother, you look tough!";
            MatatakiUngaga[5] = "When I was walking by the Waterfall^my friend Mardan Garayan told me that^he was going to move to Muska Lacka,^I wonder where that is?";
            MatatakiUngaga[6] = "If it isn´t another one of Ť^and Ʊ´s traveling companions!^Make yourself at home fellow hunter.¤Perhaps you could teach me how^to use that fighting stick!";
            MatatakiUngaga[7] = "I heard from some of the other villagers^that you are from the far off desert^village of Muska Lacka, a village^renowned for their powerful warriors.¤I remember reading that the desert is a^harsh climate full of competing tribes.^If only we can all live in harmony...";
            MatatakiUngaga[8] = "...You´re so tall...";
            MatatakiUngaga[9] = "You´re huge giant man,^how did you get so tall!";
            MatatakiUngaga[10] = "Hello tall man, you are almost as tall as me!^Keep on growing tall man and maybe one^day we could both be giants!";
            MatatakiUngaga[11] = "Such exotic clothes, how much gilda^would you like for your hat sir?";

            MatatakiUngaga2[0] = "Defeat the evil Ų, there will^always be brighter days ahead,^that alone is worth fighting for.";
            MatatakiUngaga2[1] = "Times are uncertain but that won´t^stop me from being positive,¤maybe when this is all over we´ll^travel north to visit our^neighbors in Norune Village!";
            MatatakiUngaga2[2] = "I don´t think I´ve ever seen anyone^fight with a large staff like that,^let alone be able to control^the wind either!";
            MatatakiUngaga2[3] = "I wonder what kind of beasts the^warriors of Muska Lacka hunt?";
            MatatakiUngaga2[4] = "I wonder what kind of culinary^delights can be found in your village^of Muska Lacka?¤I´m proud of my manly cooking but there^is always room to improve and^implement new styles!";
            MatatakiUngaga2[5] = "I always wondered, is it Muska Lacka^or Muska Racka? It´s hard to believe^a village like that would have^such a confusing name!";
            MatatakiUngaga2[6] = "I wanted to extend my^gratitude Ų. Although Matataki^and Muska Lacka are two villages very^far away from one another, we´re still¤helping each other. Both villages^take pride in their strength and the^warriors that call these two areas home.¤Know that if we all come together,^we can defeat this great evil.";
            MatatakiUngaga2[7] = "Brave warrior of the sand tribe,^seeing you has reminded me of a fairy^tale I heard when I was younger.¤It told the tale of someone who lived^in a harsh environment much like^us hunters. They overcame the many^challenges life threw their way and¤even went on to help the^underprivileged as well as the weak.¤This was a story of an ordinary man^growing old, he was just a common^man with a big heart. That in itself^is a rarity in our harsh reality.";
            MatatakiUngaga2[8] = "...Nice hat...";
            MatatakiUngaga2[9] = "Seriously that fighting stick is^almost as tall as I am!";
            MatatakiUngaga2[10] = "Do you like eating candy, wait what^is scorpion jerky? I don´t want to be a^meanie but that sounds gross, have a^lollipop instead!¤Didn´t you almost die from a^scorpion sting anyways?";
            MatatakiUngaga2[11] = "Hooo, that fighting stick would make for an^excellent stick for me to perch on,^care to part with it?";


            //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MatatakiOsmond[0] = "Woah watch out my friend, this^house is rigged with traps!";
            MatatakiOsmond[1] = "Ohhh seeing you brings back a flood of^memories, my parents would tell me^bedtime stories about bunnies^who lived on the moon.";
            MatatakiOsmond[2] = "Oh you´re so cute!";
            MatatakiOsmond[3] = "Oh giant bunny, welcome to our humble^village of Matataki. We hereby swear^to not harm a hair on your fuzzy head!";
            MatatakiOsmond[4] = "You look strong for such a little guy!";
            MatatakiOsmond[5] = "I know it may be hard to believe but^every night my moon bunny friends^come by and take me to the moon!!¤Come take me away bunnie boys old Kye^is ready: lucky, cookie, zucchini!";
            MatatakiOsmond[6] = "It brings me great pride to know that^the noble moon people are assisting us.¤The Dark Genie will regret the day^he awoke from his dark urn!";
            MatatakiOsmond[7] = "I heard that there was a bunny helping^Ť and his friends. As a village^that hunts beasts this may sound^preposterous but thank you my friend.";
            MatatakiOsmond[8] = "...Interesting...";
            MatatakiOsmond[9] = "What´s up with your weird clothes,^how come you don´t^wear any animal skins?";
            MatatakiOsmond[10] = "Wowie, I heard you came from the moon,^do you think one day you^could take me with you?¤Imagine the space adventures of^Couscous and Ō, that would^make for a great story!";
            MatatakiOsmond[11] = "Hmm, what kind of currency^do you use on the Moon?";

            MatatakiOsmond2[0] = "I wonder why the Dark Genie wants to^destroy Terra? Little is known about^Flagg Gilgister. Either it really makes^you think about their motivations...";
            MatatakiOsmond2[1] = "It brings me pride knowing that^Ʊ and his companions reached^the moon, that boy is a lot^like his father.";
            MatatakiOsmond2[2] = "Hey big bunny, do you want a carrot?";
            MatatakiOsmond2[3] = "You must be some sort of^divine beast, I´ve never seen^a bunny as large as you!";
            MatatakiOsmond2[4] = "When we were children we would always^hear stories about the moon being made^of cheese or bunnies living on the moon.¤I´m happy that one of those things^turned out to be real!";
            MatatakiOsmond2[5] = "Ahhhh stay away from me, I caught^the rare moon disease^known as space prebbles!";
            MatatakiOsmond2[6] = "Throughout all my years, I never^thought I would encounter a moon^person, I am honored my friend.";
            MatatakiOsmond2[7] = "To a beast, this village may seem^menacing, but to me, it´s home.¤I never was one for hunting since my^body cannot handle it but I have^a great respect for you fighters.";
            MatatakiOsmond2[8] = "...Big bunny!...";
            MatatakiOsmond2[9] = "Wait, your ears... They´re^kind of like rabbit ears!^You aren´t a rabbit are you?!";
            MatatakiOsmond2[10] = "Is the Moon really made^of delicious cheese?";
            MatatakiOsmond2[11] = "I hear the Moon Sea is home to many^dangerous monsters, it seems like a bad^investment to set up a business there!";



            //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            QueensXiao[0] = "Ahh! I´m glad that you´re here.^My name is King and I´m the mayor^of this humble seaside town.¤We could use some pest control,^these rats are becoming more common.";
            QueensXiao[1] = "Hey there, stay out of trouble now!";
            QueensXiao[2] = "I noticed that you have your eye on our^world famous flapping fish, would you^like to try some?";
            QueensXiao[3] = "The importance of having a^Watery goes a long way back.¤The water from Queens is considered^to be some of the best, most premium^water in all of the Earth.¤Many merchant families made their name^by selling our desirable water to^other villages and towns.¤I hope my humble store^can continue that legacy.";
            QueensXiao[4] = "Aren´t you the cutest little cat I´ve^ever seen!¤Have you come to Queens in search of^fish?";
            QueensXiao[5] = "Stray cats like you used to be found all^over Queens as the ocean smell would^brings cats from around the world.¤Not many animals can be seen after the^Genie attacked..."; //CANNOT REACH THE NPC
            QueensXiao[6] = "Sheriff Wilder is always giving us^trouble but we´re just trying to make^Queens a better place for everyone.¤I mean maybe we´re also looking out for^ourselves but a man´s gotta stay ahead^right!";
            QueensXiao[7] = "The King Mimic carries many treasures,^defeating one is a challenge worthy only^for the strongest warrior.";
            QueensXiao[8] = "This is a holy place and a refuge for^all.¤These recent events only underscore that^we are stronger together.^The Dark Genie will not defeat our^indelible spirit!";
            QueensXiao[9] = "I love cats but you better stay out of^King´s property!";
            QueensXiao[10] = "Hey there little buddy, let me know if^you see any trouble okay?";
            QueensXiao[11] = "Oh have you come to get your fortune^told?¤Sorry, I don´t do pets, ohohohoho!¤I hear the villagers in Matataki share a^bond with animals, perhaps they can^help.";
            QueensXiao[12] = "Beat it cat, I have allergies!¤Wait, having you around might be good^for business!";

            QueensXiao2[0] = "This town is so ungrateful, you work^tirelessly and do your best for the^people and what do you get for it?¤Here´s some advice, it´s best if^you look out for yourself.";
            QueensXiao2[1] = "(Whew) Taking care of Queens is a big^job, I don´t know how the Sheriff does^it!";
            QueensXiao2[2] = "I heard from some of the other merchants^that there are many large warrior fish^by the name of Gyon who inhabit the^Shipwreck.¤They carry large spears, take caution if^you try to gobble up that fish!";
            QueensXiao2[3] = "To think that the queen and a commoner^once fell in love right here in Queens!¤How romantic!";
            QueensXiao2[4] = "When Stu was young he used to take care^of all the stray cats.¤There was King Speed and Plugal but Hot^Sauce was his favorite.¤Oh, children grow up so fast...";
            QueensXiao2[5] = "Dark Genie or not, It´s important that^we take care of our physical health."; //CANNOT REACH THE NPC
            QueensXiao2[6] = "I heard that the Sheriff has been^suspicious of Joker for a while now.¤We´re all relieved as it gives us a^break from dealing with him and his^dim-witted partner.";
            QueensXiao2[7] = "I hear everyone was brought back by the^power of Ť´s Atlamillia, a^powerful gem indeed...¤It makes one think about what would^happen if a gem like that would fall^into the wrong hands...";
            QueensXiao2[8] = "As long as this cathedral stands, the^story of La Saia will never leave our^hearts.";
            QueensXiao2[9] = "I´m not a fan of people but animals are^a different story.";
            QueensXiao2[10] = "Would you like to join Sam and I with^fighting crime and keeping Queens safe?";
            QueensXiao2[11] = "Hmm, there´s something special about^you, are you sure your just a cat?";
            QueensXiao2[12] = "You better be careful, there´re weapons^and bombs all over the place.¤Last thing we need is an accident, let^alone one involving a cat!";


            //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            QueensGoro[0] = "Here he comes, the hammer boy that is^the talk of the town.¤Hey can you do me a favor?¤If you do defeat the Dark Genie can you^tell everyone it´s because I told you^to, it would help with my re-election^campaign.";
            QueensGoro[1] = "Woah you look strong Ʊ¤Do you think if I wore an animal skin^the other villagers would take me more^seriously when it comes to stopping^crime?";
            QueensGoro[2] = "Hey I know you!¤You´re the hunter that carries around^that huge Frozen Tuna!¤That must be exhausting for such a small^guy.¤You Matataki Hunters are something else!";
            QueensGoro[3] = "Oh you look pretty heavy, are you sure^it´s not all water weight?";
            QueensGoro[4] = "Oh look at those chubby cheeks, you look^just like Stu when he was a baby!";
            QueensGoro[5] = "Oh you say you are from the distant^village of Matataki?¤I hear the forests around the village^are home to many lethal monsters which^are poisonous such as the hornets or^Cannibal plants."; //CANNOT TALK TO THE NPC
            QueensGoro[6] = "Hey there bear skin, King is the person^who runs this town and don´t you forget^it!";
            QueensGoro[7] = "Everyone is talking about that chubby^kid from Matataki and how he uses a^Frozen Tuna to pummel his enemies.¤Here´s a tip: if you can find a sundew^it will lead you to many precious gems.";
            QueensGoro[8] = "A band of warriors consisting of people^from all over the Earth, what an^inspiring sight.¤Strong people are those who are pure of^heart and put the needs of others before^themselves.¤Thank you for everything Ʊ.";
            QueensGoro[9] = "You must be strong if you defeated a^bear, why don´t you join us?";
            QueensGoro[10] = "I see you´re wearing a bear skin on your^head, did you defeat that bear in^combat?¤You must be a very strong warrior indeed!¤Seeing you reminds me of family that I^have in a far off village who also^value physical strength hahaha!";
            QueensGoro[11] = "What brings you here, oh you wish to^have your fortune told? Which one first,^the bear or the boy?";
            QueensGoro[12] = "You look like the kind of guy who would^really benefit from purchasing my^Big Bucks Hammer.¤You´d get so much money that you could^even ditch those old animal skins and^finally make yourself look presentable.¤Everyone in Queens will call you^Big Money Ʊ, all you have to do^is grab that hammer.";

            QueensGoro2[0] = "Woah someone as plump as you must be^living the good life!";
            QueensGoro2[1] = "It must be nice to be able to pick up^that huge hammer, Sheriff Wilder says^that he´s going to help me train.¤Maybe one day I´ll be as big as he is!";
            QueensGoro2[2] = "Hunters and fisherman really are not too^different from each other.¤It´s no Frozen Tuna but you´re always^welcome to my flapping fish!";
            QueensGoro2[3] = "I heard that the village of Matataki has^a pond in the shape of a peanut.¤That´s pretty interesting!";
            QueensGoro2[4] = "It´s good to see you again young man.^Thank you for helping fix our seaside^town, we´re very thankful.¤Maybe as a reward I can sew you some^clothes that don´t look so raggedy.";
            QueensGoro2[5] = "I know you are a hunter but don´t get^ahead of yourself.¤Always remember to pack antidotes and^mighty healing when going on your^adventures."; //CANNOT TALK TO THE NPC
            QueensGoro2[6] = "Hahaha I know this may sound weird but^you look like Auntie Medu!";
            QueensGoro2[7] = "The Shipwreck is not only home to the^treasures, but also many lethal monsters,^each one more challenging than the next.¤Now it´s as if all of the treasure^hunters of Queens are profiting of their^Queen´s misfortunate demise.¤A tragedy to be sure but it´s not like^she needs those treasures anymore.";
            QueensGoro2[8] = "When meeting travellers who come to^Queens I always love telling them about^the story of La Saia.¤I feel like it´s one that many could^relate to as we all have someone that^left us whom we miss...";
            QueensGoro2[9] = "Everyone says that you´re from Matataki^village but I never heard of that place.¤For all I know maybe you made that name^up.";
            QueensGoro2[10] = "Anyone who could defeat a bear is worthy^enough to call themselves a warrior!";
            QueensGoro2[11] = "I sense a lot of regret in your heart^young man.¤As if you lost something or someone^without properly saying goodbye.^However, it´s as if there is a spiritual^presence that´s watching you.¤I feel peace and a sense of pride^emanating from this presence.";
            QueensGoro2[12] = "I wonder, out of all the weapons, why^did you choose to use a mallet?¤Oh, you use axes too?¤I hear only the most accomplished^Matataki hunters can use an axe^effectively.¤Whoever taught the ways of a hunter must^be proud.";


            //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            QueensRuby[0] = "Does this Dark Genie guy think he´s^tougher than me?¤He´s got another thing coming.¤Now I don´t want anything to do with you^Ʀ but having one of my old^associates defeat the Dark Genie would^help my chances at re-election.";
            QueensRuby[1] = "I´m glad that you´ve turned a new leaf^Ʀ.";
            QueensRuby[2] = "I´m glad you are using your magical^powers for good!¤There´s nothing better than finding^your purpose.";
            QueensRuby[3] = "I always appreciated you Ʀ,^well at least when you weren´t getting^into mischief.¤Thank you for helping Ť and^his group with fighting off the^Dark Genie.";
            QueensRuby[4] = "Back in my day proper ladies wouldn´t^dress like that.";
            QueensRuby[5] = "I´m curious about your hair color^Ʀ, was it always like this?¤It´s a rare color indeed, perhaps it´s^connected to your magic!"; //CANNOT TALK TO THE NPC
            QueensRuby[6] = "Hey there Ruby, how´s it going?¤The last time you got Jake and I in^trouble we couldn´t come into work with^King for a while!¤I´m going to be honest, it was nice^having that vacation!";
            QueensRuby[7] = "The Mask of Prajna is one of the undead^doomed to haunt the once hallowed halls^of La Saia´s Shipwreck.";
            QueensRuby[8] = "It would only make sense that a native^of Queens would try to free La Saia from^her curse.¤What happened between her and her lover^was tragic."; //THIS IS SPOILERS BEFORE THE BOSS FIGHT
            QueensRuby[9] = "Don´t think that just because you´re^helping everyone that King owes you any^favors.";
            QueensRuby[10] = "I´m very proud of how far you´ve come^Ʀ.¤I remember when you would cause all^sorts of issues for the people of Queens^with King and his hoodlums.¤Continue to make us proud Ʀ.";
            QueensRuby[11] = "(Humph) Everyone in Queens is talking^about the magical powers of the great^genie Ʀ as well as her good^looks.¤How come no one compliments me,^the great fortune teller Yaya, for my^beauty?";
            QueensRuby[12] = "Hey you´re not here to take more^merchandise are you?¤Come on Ʀ I can´t keep giving^you discounts like this.¤One of these days you´ll send me to the^poor house!";

            QueensRuby2[0] = "Remember that one time when Stu tried^teasing me by calling me King Ploogal?¤I made sure to punch him right in the^face.";
            QueensRuby2[1] = "That King is always up to no good!";
            QueensRuby2[2] = "The fish were barely biting, it doesn´t^help that the sea is so blue that you^can´t see them either!¤Say, you´re trying to prove that you´re^the most powerful genie, do you know of^any spells that can help?"; //POTENTIAL EASTER EGG
            QueensRuby2[3] = "Even a genie like you needs to stay^hydrated, feel free to come by if you^ever need something healthy to drink!";
            QueensRuby2[4] = "I wish my son would get married and give^me a grandchild.¤Would you like to meet my son, he´s such^a nice boy!";
            QueensRuby2[5] = "As a doctor I often have to administer^mighty healing to people who fall ill.¤King´s tough bodyguards Stu and Jake^cried when I had to do them haha!"; //CANNOT TALK TO THE NPC
            QueensRuby2[6] = "Do you want to help us procure some rare^gems from Joker again?";
            QueensRuby2[7] = "I´ve been collecting gems for quite some^time but I´ve never seen a Sun Stone.¤If only I had one in my collection.¤The legend goes that the Sun Stone only^shows itself to those who are pure of^heart.¤Heh like that´s going to happen.";
            QueensRuby2[8] = "Seeing merchants from all over come by^and pillage La Saia´s resting place,^the Shipwreck is a disgrace.¤I never thought people could be so^heartless.";
            QueensRuby2[9] = "Don´t talk with me, I don´t want^anything to do with you after the^mischief you made last time.¤King wouldn´t speak to us for weeks^even!";
            QueensRuby2[10] = "Sam and I are going to keep a close eye^on King and Joker.¤It seems like more and more people want^to work for that bad King.";
            QueensRuby2[11] = "One must wonder, once the people of^Earth defeat the Dark Genie, will they^turn their attention to you next?¤Humans fear what they don´t^understand...¤Perhaps that´s why the Moon People chose^to seclude themselves in Brownboo^Village.";
            QueensRuby2[12] = "You should encourage Ʊ to use^that Big Bucks Hammer to make some^extra Gilda!";


            //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            QueensUngaga[0] = "It´s comforting to know that Gilda can^solve problems in society.¤How much do you think it would cost to^get the Dark Genie to leave and never^show his ugly face here again?¤Hey, everyone has a price.";
            QueensUngaga[1] = "I hope that one day I´ll be as tall as^you Ų.";
            QueensUngaga[2] = "Interesting, I heard you´re from the^desert village of Muska Lacka.¤I know that there´s some rare fish that^can be found in the desert by using^potato cakes as bait.";
            QueensUngaga[3] = "It hasn´t been easy running a business^since King was elected as mayor.¤I feel like he slowly wants to buy every^business in Queens.¤I´d love to give him a piece of my mind!";
            QueensUngaga[4] = "The good thing about living in Queens is^we get people from all around the world^coming to visit!";
            QueensUngaga[5] = "I had the luxury of traveling all^around the Earth but not once have I^been to Muska Lacka."; //CANNOT TALK TO THE NPC
            QueensUngaga[6] = "You´re dressed funny, you must be from^out of town.";
            QueensUngaga[7] = "I don´t think you or any of your friends^are going to be able to defeat the^Dark Genie.¤The power of hate is too strong, it lies^in the heart of everyone.¤Something tells me that Genie draws from^that hate to fuel itself.";
            QueensUngaga[8] = "The story of La Saia underscores the^importance of upholding the promises you^make with loved ones.";
            QueensUngaga[9] = "You think you´re so tough?¤We should see which one of us is^stronger.";
            QueensUngaga[10] = "Ahh, a brave warrior from the Muska Lacka^Desert.¤Thank you for all the help young man,^the people of Queens appreciate your^service.¤We´ll make that Dark Genie pay for what^he did!";
            QueensUngaga[11] = "You, I sense that you hold a great^responsibility, is there someone who you^wish to protect?";
            QueensUngaga[12] = "I don´t believe in using a staff, a good^weapon should be sharp and to the^point.¤I hear the shipwreck is home to many^ferocious monsters and precious gems.¤I wouldn´t want to go down there even^if there´re rare gems!";

            QueensUngaga2[0] = "I should take you out on a spin in my^new car as a way of saying thanks for^freeing me.";
            QueensUngaga2[1] = "My primary goal in life is to keep^people safe, who knows maybe one day^I´ll be the Sheriff!";
            QueensUngaga2[2] = "That´s a cool looking turban you´ve got^there, Rando has one just like it!¤I guess it goes to show that even^Muska Lacka and Queens share some^similarities!";
            QueensUngaga2[3] = "Being trapped in Atla wasn´t so bad,^at the very least I was able to have^some alone time!";
            QueensUngaga2[4] = "Say, now that I think about it Rando^hasn´t aged a day!¤I wonder what kind of moisturizer he is^using and if he´d be willing to share^some with me ohhohoho!";
            QueensUngaga2[5] = "Be safe when you enter the Shipwreck,^there are many strange monsters which^call La Saia´s underwater grave home."; //CANNOT TALK TO THE NPC
            QueensUngaga2[6] = "I heard that there was some merchants^who went down to the Shipwreck and^didn´t return.¤I know King is the Mayor but why is this^his problem?";
            QueensUngaga2[7] = "The Moon Orb is said to give eternal^life to the one who owns it.¤Wouldn´t you want to live forever?";
            QueensUngaga2[8] = "This church was built hundreds of years^ago, since it has become a landmark^bringing many lovers together in unity.";
            QueensUngaga2[9] = "Sheriff Wilder is always running around^and ruining our plans.¤One of these days his assistant Sam is^going to disappear.¤Hehehehe.";
            QueensUngaga2[10] = "Haha you have admirers, Sam was talking^about how he wants to be as tall as you^one day!";
            QueensUngaga2[11] = "I also can see that you have a fear of^scorpions, perhaps you were hurt by one?¤That makes no sense as I saw you eating^scorpion jerky the other day!";
            QueensUngaga2[12] = "Come to think of it, I used to know a^guy from Muska Lacka, we used to trade^merchandise every so often.¤His name was Brooke, he was one tough^guy!";


            //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            QueensOsmond[0] = "Everyone says that you´re from the Moon,^I find it hard to believe.¤I bet your just a short person in a^bunny suit trying to find their claim to^fame by defeating the Genie.";
            QueensOsmond[1] = "Your heli-pack is so cool can I try it^one day?";
            QueensOsmond[2] = "Wow, it´s the first time I´ve seen^someone from the Moon!¤Did you fly down to the Earth with your^heli-pack?";
            QueensOsmond[3] = "What a surprise, is outer space filled^with cute little bunnies just like you?";
            QueensOsmond[4] = "I´m surprised that I had the Moon Orb^all along!¤If Joker had known he would have^certainly given me trouble.";
            QueensOsmond[5] = "Well I can´t guarantee that I can help^you medically if you get hurt, I could^still sell you many useful items."; //CANNOT TALK TO THE NPC
            QueensOsmond[6] = "Hey you better be careful where you fly^bunny boy, this is our town.";
            QueensOsmond[7] = "People give me a lot of trouble, maybe^I should move away to the Moon with you.";
            QueensOsmond[8] = "It makes me happy to know^that Ť has your experience and^leadership, together we´ll defeat that^genie.";
            QueensOsmond[9] = "Haha you´re a bunny, why are you flying?¤You should be hopping around everywhere^not flying!";
            QueensOsmond[10] = "I still have a hard time believing that^the Dark Genie´s evil has even^influenced the Moon and its inhabitants.¤His power really is astounding...";
            QueensOsmond[11] = "You, I don´t know what it is but you´re^different from your allies.¤I sense that fate has a much greater^role for you.¤I see many Chronicles unfolding with a^blinding White light.¤What could this all mean?";
            QueensOsmond[12] = "I can´t believe it, you use guns on^the Moon?!¤I have so many questions!¤Can I get one please?";

            QueensOsmond2[0] = "That Suzy is going around and telling^people that I want to buy up all the^shops in town.¤Now why would I want to buy a puny shop^like hers?¤I oughta buy it just to shut it down.";
            QueensOsmond2[1] = "Everyone in town is very grateful for^all the work that you and your allies^have been doing!";
            QueensOsmond2[2] = "Space Gyon is a fish monster that can^be found in the Moon Sea.¤I wonder how Gyon managed to get to^outer space...¤Perhaps it was through the help of the^Dark Genie.";
            QueensOsmond2[3] = "I´m proud to say that I have the best^watery in Queens but now you tell me^that there´s a Moon Sea?¤I wonder what Moon water tastes like?";
            QueensOsmond2[4] = "Stu is still embarrassed about this^when he was young he would play pretend^hero!¤He would call himself King Speed but^then one day he found a strange feather^that made him faster then everyone.";
            QueensOsmond2[5] = "The Genie caught us all by surprise but^now we´re ready."; //CANNOT TALK TO THE NPC
            QueensOsmond2[6] = "What brings you down here anyways?^The Dark Genie isn´t the business of the^Moon People, we can solve our own^problems.";
            QueensOsmond2[7] = "Are there any rare gems to be found on^the Moon?";
            QueensOsmond2[8] = "Through the flurry of changes one must^ensure that they stay true to^themselves.¤I can tell that you´re a good^person Ō and that goes for the^rest of your allies.";
            QueensOsmond2[9] = "I´ve never seen a bunny in Queens^before, what are you doing here?";
            QueensOsmond2[10] = "I do what I can to keep our sea side^town safe but if you ever need anything^Ō let me know.";
            QueensOsmond2[11] = "You´re a strong leader, I can tell you^have many who look up to you even if^you´re a cute little bunny.";
            QueensOsmond2[12] = "How did you get that heli-pack?¤I have to admit the first time that I^saw you flying with that thing it blew^my mind!";



            //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MuskarackaXiao[0] = "Filthy beasts like you can learn^a thing or two from my beauty!¤You probably think you´re so cute but^you´ll never be anything when^compared to the great Jibubu!";
            MuskarackaXiao[1] = "What´s a small cat like you doing in^this desert?¤You must be fierce and vigilant to^survive in this harsh climate.¤You may look cute but looking at your^eyes I can see the warrior within...";
            MuskarackaXiao[2] = "I heard Enga used to be an accomplished^swordsman when he was a young man.¤I never was able to master the sword^like Enga but I´m the only warrior in^the village who can use a Slingshot!";
            MuskarackaXiao[3] = "You´re a small cat aren´t you, I know a^thing or two about being the smallest...¤Living out in the desert can be^dangerous, the other day my friend was^stung by a scorpion and almost died!¤If you need anything at all or even a^house to take shelter, feel free to find^me.";
            MuskarackaXiao[4] = "A stray cat like you has no business^being in or around my home!¤There´s plenty of room in the wide open^desert for you to roam around in.";
            MuskarackaXiao[5] = "Gosuke and Toto would always pretend to^be warriors and have adventures^together!¤Maybe you could join them and be an^adventurer too!¤You´re a small cat now I´m sure you´ll^change the world one day! Tehehe Ȟ"; // <3 AT THE END
            MuskarackaXiao[6] = "The Sun and Moon Temple is a sacred^place as it´s not only the home of the^Moon Ship but also the final resting¤place of our noble king who passed away^generations ago.¤Since his passing his kingdom broke up^into many warring tribes.¤Since then blood has been split and^people have been killed, perhaps it is¤too late to unify...¤It´s up to our generation to unify after^years of conflict.";
            MuskarackaXiao[7] = "No one should ever under estimate the^killer instinct of an animal.¤You may be small but you´re a worthy^hunter in your own right.";
            MuskarackaXiao[8] = "Hey cat, how about you bust me out of^here!";
            MuskarackaXiao[9] = "Wow, a cat in our village!^Would you like to play with me?";
            MuskarackaXiao[10] = "You, you´re no ordinary cat...¤What are you?";

            MuskarackaXiao2[0] = "Everyone thinks that Ų is^the most handsome looking man^in the village!¤Keep this between you and me but^everyone here has low standards!¤Here I am talking to^a wild animal, shoo!";
            MuskarackaXiao2[1] = "I always see you following the boy in^the green hat. I once knew a man with^sparkling eyes just like him, full of^hope and strength.¤He was a traveling adventurer, when he^made his way here he defeated everyone^in the village!¤They say he was looking for something or^perhaps someplace...¤He was determined so I´m sure he found^what he was looking for.";
            MuskarackaXiao2[2] = "It´s been my responsibility to take care^of the Moon Signet for quite some time^now.¤I feel a sense of pride whenever I think^about the village trusting me with this^heirloom.¤The Moon Signet used to be inseparable^from the Moon people here on Terra but^in recent years they´ve become lazy.";
            MuskarackaXiao2[3] = "Mikara doesn´t like animals, she says^she´s allergic but I think she´s just^scared.";
            MuskarackaXiao2[4] = "Devia was talking about you the other^day, she said she wanted to take you in.¤Don´t get any ideas little cat, there´s^no room in the 3 Sisters´ House.";
            MuskarackaXiao2[5] = "Were you also stuck inside of a weird^bubble?¤I wonder how that all happened anyways,^one minute we´re minding our own^business and the next we´re gone...¤I guess everything can change in the^blink of an eye huh Ӿ.";
            MuskarackaXiao2[6] = "Cats like you shouldn´t step foot into^the Temple, it´s a tomb festering with^some of the most ferocious monsters only^seen in your nightmares.¤No place for a little critter like you^haha!";
            MuskarackaXiao2[7] = "I wish it were possible for all the^desert tribes to live in peace.¤If the return of the Dark Genie has^taught us anything it´s that we should^stand together.";
            MuskarackaXiao2[8] = "These people, they put me in this prison^like an animal.¤If I was given another chance I would^be a changed man, honest!";
            MuskarackaXiao2[9] = "Gosuke will always be my best friend,^I see you running after Ť all^the time, I guess he would be your best^friend haha!";
            MuskarackaXiao2[10] = "I feel magic...¤Are you like Gosuke?¤No, you are different...";


            //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MuskarackaGoro[0] = "You are so out of shape, have you ever^thought of laying off the grass cakes?";
            MuskarackaGoro[1] = "Ah so your from the village of Matataki,^I can tell by your distinct warrior^garb.¤The Moon People used to maintain the^shrine but they´ve gotten lazy over^time, now they relax in the woods not^too far from Matataki.¤Make sure your people don´t hunt them^by accident!";
            MuskarackaGoro[2] = "The Sun and Moon Temple is a sacred^place, no one outside of the village has^ever stepped foot inside of it.¤It´s a shame that it has been riddled^with monsters, please help Ť^and Ų rid us of this^infestation.";
            MuskarackaGoro[3] = "It´s hard to believe that we were almost^destroyed had not been for Ť^and his friends rescuing us.¤We are all in your debt.";
            MuskarackaGoro[4] = "Why are you wearing that, you must^be hot!";
            MuskarackaGoro[5] = "You´re so heroic, seeing you fight side^by side with Ų makes me inspired^to become a warrior myself!";
            MuskarackaGoro[6] = "Bonka used to be the strongest person in^the villages´ history, that all changed^when that traveler came.¤I still remember that fated encounter^to this day, he wielded a beautiful sky^blue blade engraved in gold!¤Much to everyone´s surprise, in just one^swing he defeated Bonka.¤He said he came from a faraway village^in search of a powerful weapon.¤I can´t remember what he was in search^in particular but he seemed desperate^to find it.¤Come to think of it, he looked a lot^like that boy leading you all...";
            MuskarackaGoro[7] = "Ah, a hunter from Matataki!¤Matataki Village is renowned for their^hunting prowess and bravery in battle.";
            MuskarackaGoro[8] = "Chubby hunters like you better watch out^when walking near me, they didn´t put me^in this cage for show you know!¤You better watch your back forest^hunter!";
            MuskarackaGoro[9] = "You´re a small guy huh, yet you´re still^a capable warrior!¤That´s so inspiring, I want to be just^like the great warrior Ʊ when^I´m older!";
            MuskarackaGoro[10] = "You are small like Toto yet you fight...¤What are you fighting for little one?";

            MuskarackaGoro2[0] = "I heard Devia say that you looked cute,^how and why!¤The women of the village should be^coming to me and not some out of shape^Opar like you!";
            MuskarackaGoro2[1] = "You seem like quite the accomplished^warrior, you remind me of myself when^I was young.¤I used to be the best warrior in all the^village, I still am to some degree.¤This all changed when I was bested in a^duel with a traveling adventurer by the^name of Aga.¤He was determined to find a weapon that^was capable of defeating the darkness^which had begun to linger in our world.¤We were both young and I must admit I^was impressed.¤I have not seen him since and I fear he^may have succumbed to the darkness^himself...";
            MuskarackaGoro2[2] = "To access the Moon Ship you need both^the Sun and the Moon Signet.¤The Sun Signet has always been in the^care of our village chief but the Moon^Signet was recently given to us by the^Moon People.¤Sure it makes it more convenient but I^can´t imagine someone walking away from^their duty!";
            MuskarackaGoro2[3] = "I can´t fathom leaving your home behind^and going on an adventure across the^world.¤For a small guy you are quite brave...";
            MuskarackaGoro2[4] = "They say that you are a warrior, as if!¤I bet even that wimp Jibubu could defeat^you with one hand tied behind his back!";
            MuskarackaGoro2[5] = "I bet your mallet is so strong that you^can even crack the shell of a crabby^hermit!";
            MuskarackaGoro2[6] = "If that mallet of yours ever becomes^worn out be sure to visit Brooke to get^some repair powder.¤He is one of the bravest warriors in our^village, his eye is proof of that.";
            MuskarackaGoro2[7] = "Always take care brave hunter.^To defeat the evil plaguing our land one^must first overcome the evil within and^extinguish any self-doubts.";
            MuskarackaGoro2[8] = "One thing you and the hunters from^Muska Lacka have in common is that you´re^both prideful, that pride will be your^downfall.¤It´s that same pride that helped fuel^the conflict between our desert tribes.¤Even if you found some way to defeat the^Dark Genie you´ll never defeat the evil^within the human heart!";
            MuskarackaGoro2[9] = "They say Gosuke appeared in our village^soon after a powerful warrior departed.¤Whenever I ask the adults about who this^traveller was they´re always very^secretive about his identity.¤I´d love to meet him and ask how he made^Gosuke, maybe he could make more!";
            MuskarackaGoro2[10] = "The animal you´re wearing, it is dead...¤Why did you harm it?";


            //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MuskarackaRuby[0] = "Oh my, finally someone with beautiful^sensibilities such as myself!¤What kind of foundation do you use for^your skin?";
            MuskarackaRuby[1] = "Now aren´t you the pretty one, it´s not^every day that you meet another woman^warrior.¤I´ve been the chief of this village for^quite some time, initially my ability to^lead was called into question due to my^gender.¤However these disagreements were always^settled in a duel and I always was the^victor.¤Let no one dictate what you can do,^always take pride in who you are.";
            MuskarackaRuby[2] = "Oh you came from a port town called^Queens? That´s funny the most water we^have is our oasis.";
            MuskarackaRuby[3] = "You can use magic right?¤I wonder if you can make Toto another^playmate like Gosuke, he often gets^lonely...";
            MuskarackaRuby[4] = "I never met someone who could use magic^but old Enga says that long ago the Moon^people once sealed the Dark Genie by^using magic.";
            MuskarackaRuby[5] = "Wow I love your outfit, it´s so cute! Ȟ"; // <3 AT THE END
            MuskarackaRuby[6] = "You and the Dark Genie share many^similarities but I sense no hostility^from you.";
            MuskarackaRuby[7] = "I´ve never had the experience of facing^a magic user in battle and I hope I^never have the chance. You are a^formidable fighter indeed.";
            MuskarackaRuby[8] = "Purple hair?¤Jeez they must really be desperate for^help if they let a purple hair clown^like you join!¤Oops did I say clown, I meant to say ´Genie´";
            MuskarackaRuby[9] = "No way you can do magic, they say Gosuke^came from magic!";
            MuskarackaRuby[10] = "Maaaaaaaaagic...";

            MuskarackaRuby2[0] = "I heard that if you use the saliva of a^blue dragon it can promote hair growth!¤I mean there´s only one way to test that^theory, time to find some blue dragons!";
            MuskarackaRuby2[1] = "They say that you see the same eyes in^different people and that time passes^faster as you age.¤That boy who arrived in the village,^the one with the green hat, I´m sure he^is the son of Aga.¤He was in search for a weapon in^preparation which can defeat a coming^great evil.¤Perhaps he himself became lost to evil^and the Fairies saw fit to send his own^son to finish the journey his father^began...¤Aga...";
            MuskarackaRuby2[2] = "The Moon Signet represents our^connection to the moons, I´m not sure^why the Moon people gave it up but I´m^very proud to defend this signet.";
            MuskarackaRuby2[3] = "The other day I saw Ӿ sitting^by the river waiting for Ť to^catch a fish, it was such a precious^moment!";
            MuskarackaRuby2[4] = "Now I heard all about your skills in^magic but if only you could find someway^to use your magic to change the^weather!";
            MuskarackaRuby2[5] = "I heard that you want to be the most^power genie in the world, I bet you can^do it!";
            MuskarackaRuby2[6] = "Watching that young Toto run and play^reminds me of the good old days of my^youth!";
            MuskarackaRuby2[7] = "I once heard a story of an old merchant^who wore a sombrero and owned a number^of buggies.¤They say that man has no eyes to speak^of, what manor or horror is he?¤Perhaps it is just make believe...";
            MuskarackaRuby2[8] = "Being in this prison is making me bored,^hey genie can you do any magic tricks!¤Maybe you could make a moon bunny come^out of your green friends hat haha!";
            MuskarackaRuby2[9] = "I hope I can go on an adventure like^Ų and Ť when^I´m older!";
            MuskarackaRuby2[10] = "Thank you for helping, magic girl...";


            //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MuskarackaUngaga[0] = "Everyone seems to prefer you over me and^now your going on an adventure to save^the world? The Spirits always seems to^deal you a better hand.¤I know we haven´t always got along but^I´ll be sure to defend the village in^your absence...¤But, if I get so much as one scratch on^my perfect face that´s it for me!";
            MuskarackaUngaga[1] = "That friend of yours, the one with the^green hat... Be sure to keep him close^and learn from him.¤Ų I´m an old warrior but not^one without regrets.¤Long ago, there was a day when that^boy´s father and I met each other in^battle.¤His strength was unmatched and I was^quickly bested. He treated everyone,^with love and kindness, even on the^battlefield. He was a good man.¤Part of me wishes I could have joined^him on his journey but I had to fulfill^my responsibilities here to guide our^village as chief.¤(Sigh) As people grow older, they tend^to wonder about what could have been.¤Life is a journey Ų, be sure to^live without regrets.";
            MuskarackaUngaga[2] = "Hey Ų, make sure to take some^precaution when going into the Sun and^Moon Temple.¤I hear there´s plenty of poisonous^scorpions there, last thing we need is^you being stung again haha!¤Sorry, I´m just teasing!";
            MuskarackaUngaga[3] = "I always worry about you Ų, it´s^like you are always putting yourself in^harm´s way for the sake of the village^but now with the threat of the¤Dark Genie, you´re doing it for all of^the world.¤Only you and your allies can save us.";
            MuskarackaUngaga[4] = "Ų you´re a strong warrior, that^little kid with the green hat will need^all the help he can get to defeat the^Dark Genie.¤Take care and make sure you come back in^one piece, I don´t want you breaking my^sisters heart!";
            MuskarackaUngaga[5] = "Thank you so much for helping everyone^in the village, make sure you give that^Dark Genie a good whack in the head^for me!¤I may be an optimist but perhaps all of^the desert tribes can come together and^start a new way of life!";
            MuskarackaUngaga[6] = "All of these events has reminded me of^an old fable that has been passed down^throughout our Muska Lacka.¤One which tells the story of a young boy^whose village was torn from the earth by^a great evil and suspended from the sky!¤This boy had to travel throughout the^lands to find a way to not only defeat^the evil but also return his home.¤Make believe often mimics reality it^seems haha!";
            MuskarackaUngaga[7] = "You are the pride of our village, now^you´ll step foot into the outside world¤and everyone will know why the warriors^of Muska Lacka are the strongest to walk^on Terra.";
            MuskarackaUngaga[8] = "Heh, if it isn´t the brave Ų^have you come to taunt me once again!";
            MuskarackaUngaga[9] = "Woah, big bro!¤I had so much fun during our training,^I want to be just like you when I´m^older!";
            MuskarackaUngaga[10] = "Ų... friend...";

            MuskarackaUngaga2[0] = "I heard Enga used to be the most^handsome man in the village.I have a^hard time believing that!";
            MuskarackaUngaga2[1] = "You are strong Ų be sure to^stay true to your ideals.¤You are now traveling the world for a^great purpose, to protect those who are^weak and to defeat the great evil that^is afflicting the land.¤That same evil destroyed this village^and will not stop until it leaves only^dark clouds of destruction in its wake.";
            MuskarackaUngaga2[2] = "I heard you spent time with a Moon Bunny^by the name of Theo, that is so cool!^I wish they would come visit more often!";
            MuskarackaUngaga2[3] = "Ų as you explore the world it^would make me happy if you could come^back to visit and tell me all about your^journey.";
            MuskarackaUngaga2[4] = "The other day, Devia was drawing water^from the Oasis at dusk when she saw the^most bizarre fish!¤She described it as being the oddest^shade of purple with long yellow fins^that resembled a regal hair style!¤We´re not sure why it appeared, we made^Potato Cakes perhaps it was fond of the^smell?";
            MuskarackaUngaga2[5] = "After what happened to our village I^think I have a new found motivation to^become a strong warrior like Chief^Bonka.";
            MuskarackaUngaga2[6] = "They say a Moon person helped snap you^out of that depression you fell into.¤They are quite the wise ones those Moon^people!";
            MuskarackaUngaga2[7] = "I´ve watched over you since you were^young, it takes a village to raise a^child!¤I´m very proud of all of the progress^you have made, not only as a warrior but^also as a person.¤Ų our warring tribes are no^strangers to conflict and there will be^times where you will have to put your¤values to the test on the battlefield.^That is how I lost my eye.¤That´s the price we have to pay for^holding true to our ideals.¤It´s important that when faced with^difficulty, you don´t lose sight of who^you are.";
            MuskarackaUngaga2[8] = "I wonder, once you fail and meet your^end what will become of your legacy?¤Will your pitiful tribe still hold you^to high regard, or will be known as the^failure that doomed them?";
            MuskarackaUngaga2[9] = "Don´t tell Chief Bonka or Enga but I was^once exploring the Sun and Moon Temple^the other day and I saw some gold and^silver golems.¤Do you think they´re related to Gosuke?";
            MuskarackaUngaga2[10] = "Don´t be sad Ų...^Gosuke proud of you...";


            //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MuskarackaOsmond[0] = "You cover your face with goggles and a^scarf, why not show your beauty for all^to see?";
            MuskarackaOsmond[1] = "Ah hah, finally a Moon person comes to^our village!¤I´ll have you know that the people of^Muska Lacka have been staying on top of^the defending the Sun and Moon Temple as^well as the Signets.¤We´re not lazy like those bunnies who^chose to live the easy life in the^forest...";
            MuskarackaOsmond[2] = "Ah a Moon person, have you come to visit^the Sun and Moon Temple?¤Take caution as it is infested with^monsters!";
            MuskarackaOsmond[3] = "So if you´re here that must^mean that Ų and his friends^made his way to the moons!";
            MuskarackaOsmond[4] = "Old Enga would tell us that the moons^are home to the Moon tribe but I never^thought they would be bunnies!";
            MuskarackaOsmond[5] = "You may be small and cuddly, but I bet^you´re quite the warrior! Ȟ"; // <3 AT THE END
            MuskarackaOsmond[6] = "Ahhh how are doing young one!¤It has been a lifetime since I´ve been^in the presence of your kind.¤I have passed down the stories and myths^that your tribe has given us to the next^generation of Muska Lacka¤but they often have a hard time^believing that the Moon people once^lived among us!";
            MuskarackaOsmond[7] = "It has been sometime since a moon person^has journeyed to our village.^If you are in need of any supplies let^me know my friend.¤The Moon people and the tribe of^Muska Lacka have always had a close^partnership and we wish to maintain that^connection.";
            MuskarackaOsmond[8] = "What even are you?";
            MuskarackaOsmond[9] = "Wooah, nice you can fly!";
            MuskarackaOsmond[10] = "Moons... Pretty...";

            MuskarackaOsmond2[0] = "Are you a short hair or a long hair^bunny, what is your hair care regimen?";
            MuskarackaOsmond2[1] = "I can tell by your demeanor that you´re^the leader of your moon tribe, care to^have a duel?¤The winner will be both the chief of the^desert tribe as well as the moon^bunnies!";
            MuskarackaOsmond2[2] = "What is that weapon you use, it´s like^a slingshot but mechanical! I know Moon^people are adept in magic but what magic^is this?!";
            MuskarackaOsmond2[3] = "When this is all over I hope that we can^continue to live in unison.¤It seems like people have been fighting^each other for some time now and that^has to change.";
            MuskarackaOsmond2[4] = "Ų is the best warrior the^village has to offer, I hope he will aid^you all on your quest.";
            MuskarackaOsmond2[5] = "Have you been teaching Ų a^thing or two when it comes to fighting?";
            MuskarackaOsmond2[6] = "I often wonder about Gosuke and his^creation.¤How was that traveller Aga able to^create him, did he learn a method from^you Moon people or did he use some other^form of magic to conjure Gosuke?";
            MuskarackaOsmond2[7] = "They say the Moon people here on Terra^have been spending their time relaxing^in the woods, have you visited them yet?";
            MuskarackaOsmond2[8] = "Hahaha you are going to fight the^Dark Genie?!¤Good luck short stuff!";
            MuskarackaOsmond2[9] = "I can´t believe I´m actually talking to^a Moon person! Enga tells me all about^your tribe, I always believed him but^it´s so cool to see one for real!¤Can you take me with you to the Moon^one day?";
            MuskarackaOsmond2[10] = "Gosuke likes you little bunny...";



            //ungaga, theo
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            SunmoonXiao[0] = "It´s nice to see such^a friendly creature.";
            SunmoonXiao[1] = "Oh my, you smell like magic!¤Did you use a Change Potion recently?";

            SunmoonXiao2[0] = "It´s useless, I´m not a strong warrior^like Ť, I let everyone down.¤Mikara...";
            SunmoonXiao2[1] = "I envy you, I can only imagine^what going on a journey^with Ť is like!";


            SunmoonGoro[0] = "A brave warrior like you would^have prevented this tragedy.¤I bet you have always been strong,^your determination is unwavering.";
            SunmoonGoro[1] = "I´m glad I could see a familiar face,^we may be far away from Brownboo or^Matataki but we´re still neighbors,^and neighbors help each other out!";

            SunmoonGoro2[0] = "Life is precious, I failed to^protect the living...¤I am a failure...";
            SunmoonGoro2[1] = "We really need to cheer Ų up,^if he stays like this he´ll never^be the warrior we need!¤It might be difficult^but I´m sure you´re a^barrel full of laughs Ʊ!";


            SunmoonRuby[0] = "You´re a genie, perhaps you´ve come^to finish the work left behind^by the Dark Genie...";
            SunmoonRuby[1] = "I can´t believe Ť and his^allies actually recruited a genie,^wait until I tell everyone!";

            SunmoonRuby2[0] = "I let everyone down...";
            SunmoonRuby2[1] = "We should try to find the time^to do a magic show, after we defeat^the Dark Genie of course!";


            SunmoonUngaga[0] = "Hmm, it seems you used a cheat device^to unlock all characters before^getting Ų, this dialogue^shouldn´t be possible¤but that didn´t stop you haha!^I may as well use this dialogue space^to shout out my talented friends^Word of Wind, MikeZord, Plguee,^Dayuppy and Glitchedd for all of the¤generous contributions they´ve made to^the Dark Cloud Community and for^their diligent work on this fan mod.¤It was a challenge and we worked day^and night to make this mod something^truly special for you all to enjoy.¤One thing I love about this community^is that it´s home to so many^passionate and talented people.¤Even after 20 years you all continue^to be creative, we hope this mod^can help make new memories.^Thank you for all the support.¤Sincerely, Hiddencastle and^the Dark Cloud Compendium.";
            SunmoonUngaga[1] = "Hey you are not supposed to be here!¤Dark Cloud is home to some of the most^interesting cut content, one of my^favorite moments in my time in^the Dark Cloud Community was when¤Word of Wind and MikeZord actually^found the cut playable character Seda!¤Ahhhh we were so happy,^it was 20 years in the making!¤Could you imagine if we got Seda^instead of Osmond, crazy to think!";

            SunmoonUngaga2[0] = "We lost so much during the pandemic^years, countless good people^gone too soon.¤On August 17th 2020, I lost my father^due to cancer and a lot of this^dialogue was inspired by the stories^we would tell each other.¤We´re all missing someone but^it can´t rain all the time.¤This is dedicated to everyone we^lost in the pandemic years,^the young and the old.¤We miss you more than words^can describe, you will all^forever be in our hearts.";
            SunmoonUngaga2[1] = "Oh so you want to know about the^legendary Dark Cloud 3 eh?¤Well if I told you I´d^have to eat you!¤I may look like a cute Moon Bunny^but I´m secretly a Xenomorph!";


            SunmoonOsmond[0] = "Hmm, it seems you used a cheat device^to unlock all characters before^getting Ų, this dialogue^shouldn´t be possible¤but that didn´t stop you haha!^I may as well use this dialogue space^to shout out my talented friends^Word of Wind, MikeZord, Plguee,^Dayuppy and Glitchedd for all of the¤generous contributions they´ve made to^the Dark Cloud Community and for^their diligent work on this fan mod.¤It was a challenge and we worked day^and night to make this mod something^truly special for you all to enjoy.¤One thing I love about this community^is that it´s home to so many^passionate and talented people.¤Even after 20 years you all continue^to be creative, we hope this mod^can help make new memories.^Thank you for all the support.¤Sincerely, Hiddencastle and^the Dark Cloud Compendium.";
            SunmoonOsmond[1] = "Hey you are not supposed to be here!¤Dark Cloud is home to some of the most^interesting cut content, one of my^favorite moments in my time in^the Dark Cloud Community was when¤Word of Wind and MikeZord actually^found the cut playable character Seda!¤Ahhhh we were so happy,^it was 20 years in the making!¤Could you imagine if we got Seda^instead of Osmond, crazy to think!";

            SunmoonOsmond2[0] = "We lost so much during the pandemic^years, countless good people^gone too soon.¤On August 17th 2020, I lost my father^due to cancer and a lot of this^dialogue was inspired by the stories^we would tell each other.¤We´re all missing someone but^it can´t rain all the time.¤This is dedicated to everyone we^lost in the pandemic years,^the young and the old.¤We miss you more than words^can describe, you will all^forever be in our hearts.";
            SunmoonOsmond2[1] = "Oh so you want to know about the^legendary Dark Cloud 3 eh?¤Well if I told you I´d^have to eat you!¤I may look like a cute Moon Bunny^but I´m secretly a Xenomorph!";




            //linda, lumba, salsa, flammi, flada, limbo, jive, tap, aily, cheek
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            YellowdropsXiao[2] = "I heard that some individuals^on the Blue Terra think that the^Moon People resemble bunnies,^how preposterous!";
            YellowdropsXiao[3] = "It seems like the people on Terra have^spent countless years in conflict, if^only we could all get along!";
            YellowdropsXiao[4] = "I could have sworn I saw you transform^into a person, maybe I´m seeing things!";
            YellowdropsXiao[5] = "I´m proud to see you here with the other^humans and the genie, I guess if one^good thing has come from the¤Dark Genie´s arrival it´s that he´s^uniting everyone together. Cats^included!";
            YellowdropsXiao[6] = "Are you impressed with the technology we^have here on Yellow Drops?";
            YellowdropsXiao[7] = "It´s hard to think about how the other^Moon People are living on Terra, have^they lost touch with their culture, can^they still use magic?";
            YellowdropsXiao[9] = "Oh wow, you are very far away from home^little kitty!";
            YellowdropsXiao[10] = "Not to sound rude or anything but cats^don´t belong here, if you wander into^the Moon Factory and cause mischief¤again who knows what Ō^will do!";
            YellowdropsXiao[11] = "I wonder how Ʊ still manages^to carry that large mallet out here,^imagine how heavy it is!";
            YellowdropsXiao[12] = "The Sun Giant will always be the pride^of Yellow Drops!";

            YellowdropsXiao2[2] = "I wonder how it would be to live^just one day on Terra...";
            YellowdropsXiao2[3] = "Salsa said that he saw you transform^into a person haha!¤Oh Salsa, what a comedian!";
            YellowdropsXiao2[4] = "Seeing Ō fly with his heli-pack^is so inspiring, is there anything he^can´t do?";
            YellowdropsXiao2[5] = "We saw the Dark Genie´s attack from up^here, it looked so scary! I´m sorry you^all had to go through that...";
            YellowdropsXiao2[6] = "Perhaps one day Blue Terra could be just^as advanced as we are in technology!";
            YellowdropsXiao2[7] = "I hear that the Moon people on Terra^have become lazy, I guess that comes^when living the good life!";
            YellowdropsXiao2[9] = "I always see you chasing around^Ť and his companions, I wish I^could go on an adventure like you!";
            YellowdropsXiao2[10] = "Promise to always stay by Ť^side, he has a good heart and will make^sure you won´t cause any trouble!¤The last time you wandered into the^Moon Factory you left destruction in^your wake.¤Who would have thought such a small^creature could cause large damage!";
            YellowdropsXiao2[11] = "I admire Ų´s height.^I wish that I can be that tall one day^that way Ō won´t boss^me around!"; // REVIEW THIS
            YellowdropsXiao2[12] = "I wish it were possible for me to take^that Sun Giant out for a spin!";



            //linda, lumba, salsa, flammi, flada, limbo, jive, tap, aily, cheek
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            YellowdropsGoro[2] = "I always enjoyed reading about the^Hunting Tribe of Matataki, your village^is close to Brownboo is it not?";
            YellowdropsGoro[3] = "Minotaur Joe is the #1 combatant to^watch in the coliseum!";
            YellowdropsGoro[4] = "The other day Ō was kicking^around the idea of competing in the^coliseum, that wouldn´t be fair to the^other fighters!";
            YellowdropsGoro[5] = "I saw you and your friend´s come down on^the Moon Ship, that was really cool!";
            YellowdropsGoro[6] = "I always admired how the people of^Matataki maintained their connection^to nature.";
            YellowdropsGoro[7] = "I always admired how Ō is able^to bring everyone together, he´s a^great leader!";
            YellowdropsGoro[9] = "I hope you enjoy your time in^Yellow Drops, we have everything here!";
            YellowdropsGoro[10] = "The other day Jive had the brilliant^idea of going to the Moon Sea, he ended^up being chased out by Crescent Baron!";
            YellowdropsGoro[11] = "I would often look at Blue Terra and^wonder what it would be like if I went^down for a visit.";
            YellowdropsGoro[12] = "The Dark Genie may be powerful but you^got that mallet, never underestimate the^power of a good mallet.";

            YellowdropsGoro2[2] = "I heard legends of a white tiger which^used to stalk the surrounding forests^of Matataki.¤It would wipe out settlement after^settlement!¤If only there was someone brave enough^to stand up to that beast.";
            YellowdropsGoro2[3] = "When I grow up I want to be just like^Minotaur Joe, he will always be my hero!";
            YellowdropsGoro2[4] = "I know Matataki is a tribe of hunters,^maybe you could train me one day so I^can be the next Minotaur Joe!";
            YellowdropsGoro2[5] = "The Moon Sea wasn´t always so dangerous,^I used to spend my childhood days^exploring every inch of that place!";
            YellowdropsGoro2[6] = "Careful with that mallet, is it really^necessary to carry that with you^everywhere?";
            YellowdropsGoro2[7] = "I heard the others needed a hammer for^the Sun Giant repairs, perhaps they^could borrow yours?";
            YellowdropsGoro2[9] = "Say, those are some weird clothes!^Does everyone from Blue Terra dress^like that?";
            YellowdropsGoro2[10] = "I wonder why the Dark Genie decimated^all of Blue Terra but didn´t do the same^to Yellow Drops?¤He must have been pretty angry at you^folks!";
            YellowdropsGoro2[11] = "I can´t believe that the Moon People on^Terra have forgotten how to use magic,^the easy life made them forget the^fundamentals!";
            YellowdropsGoro2[12] = "Once this Dark Genie business is over I^want to get a heli-pack like Ō!¤Hey don´t look at me like that, you have^to treat yourself!";



            //linda, lumba, salsa, flammi, flada, limbo, jive, tap, aily, cheek
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            YellowdropsRuby[2] = "I heard that you´re a being comprised of^magic, a genie! I never met an actual^genie before!";
            YellowdropsRuby[3] = "Have you heard about Minotaur Joe?^He´ll always be my favorite combatant^to watch in the coliseum!";
            YellowdropsRuby[4] = "One day we´ll remember this ordeal and^laugh! We gotta stay positive!";
            YellowdropsRuby[5] = "Ō is so brilliant, they say he^invented that heli-pack by combining a^milk can, pipes and a belt!¤How did he even do that?";
            YellowdropsRuby[6] = "If I can go anywhere on Terra I´d love^to visit Queens!";
            YellowdropsRuby[7] = "Being in Yellow Drops must be different^from being in Queens, let me know if you^need anything!";
            YellowdropsRuby[9] = "Yellow Drops is always in a state of^nightfall, sometimes it gets scary and^I´d get the others to check under^my bed!";
            YellowdropsRuby[10] = "The others were uncomfortable with the^idea of having a genie in Yellow Drops^but you´re nothing like the Dark Genie.¤Thanks for helping Ť and his^allies!";
            YellowdropsRuby[11] = "I can´t imagine living on Blue Terra!";
            YellowdropsRuby[12] = "When I was young my friends and I would^go to the Moon Sea and try to test our^bravery! We would all chicken out except^for Ō of course!";

            YellowdropsRuby2[2] = "I was wondering, how do you channel^magic through your armbands?";
            YellowdropsRuby2[3] = "When I grow up, I want to be Tag Team^Champions with Minotaur Joe! Our team^could be called the Bunny and Bull^Connection or the Joe and Lum Express!";
            YellowdropsRuby2[4] = "I remember reading in an old textbook^that long ago there once used to be^floating continent. I wonder if that was^the work of the Dark Genie was well!";
            YellowdropsRuby2[5] = "I heard that in some parts of Terra they^don´t use technology at all?¤That can´t be true can it?";
            YellowdropsRuby2[6] = "I heard from the other allies that some^of Terra´s animals have become^dangerous. They must be under the^influence of the Dark Genie!";
            YellowdropsRuby2[7] = "I wonder, you´re a strong magic user^right? I think you should compete in the^coliseum!¤If that blockhead Minotaur Joe could be^a champion so could you!";
            YellowdropsRuby2[9] = "Last time I found a baby Moon Beetle^under my pillow, crazy right!";
            YellowdropsRuby2[10] = "Yellow Drops has always took pride in^how we use magic and technology, maybe^we could learn from each other!";
            YellowdropsRuby2[11] = "Everyone in Yellow Drops was so excited^when you all came, we rarely get^visitors!";
            YellowdropsRuby2[12] = "There was never monsters in the Moon^Sea, the other day I was going out for^exercise and a Crescent Baron tried^whipping me!";


            //linda, lumba, salsa, flammi, flada, limbo, jive, tap, aily, cheek
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            YellowdropsUngaga[2] = "Woah you´re so much taller than me!^Your almost as big as the sun giant!";
            YellowdropsUngaga[3] = "You´re almost as tall as Minotaur Joe^but he still bigger haha!";
            YellowdropsUngaga[4] = "I heard the fighting stick is only used^by the most honorable sand warriors!";
            YellowdropsUngaga[5] = "Thank you for all of your help^Ų! I knew the Moon People could^count on the Desert Tribe to help defeat^Dark Genie!";
            YellowdropsUngaga[6] = "Ʊ seems pretty scary, he´s^wearing an animal pelt! What if he^attacks me, that mallet is huge!";
            YellowdropsUngaga[7] = "I always wanted to go on an adventure,^maybe once this is all done Ō^can plan an expedition across the^Moon Sea!";
            YellowdropsUngaga[9] = "Both you and Ʊ carry your^weapons out, if any monsters appear I^know that you two will defend us!";
            YellowdropsUngaga[10] = "The Moon People and the Desert Tribe of^Blue Terra always had a good^relationship with each other.^Make yourself at home Ų.";
            YellowdropsUngaga[11] = "Make sure not to drink the yellow moon^water, it can give humans a tummy ache!";
            YellowdropsUngaga[12] = "When it´s time for bed I hear odd^notices coming from the direction of the^Moon Sea...¤I have no idea what´s out there but^watch yourself.";

            YellowdropsUngaga2[2] = "I saw Ӿ the cat walking around^with a slingshot in her mouth, what was^she up to?";
            YellowdropsUngaga2[3] = "Minotaur Joe always acts like a crazy^beast in the ring but I heard he´s a^quiet Minotaur when outside of the^coliseum.¤Looks can be deceiving!";
            YellowdropsUngaga2[4] = "Often I wonder about the power of the^Dark Genie, would it be possible for^someone to harness that power and use it^for good?¤I guess it´s not possible, after all the^Genie was locked away to prevent anyone^from misusing that power.";
            YellowdropsUngaga2[5] = "I hope that once this situation is over,^that the Moon People of Yellow Drops can^find some way to keep in contact with^the people of Blue Terra!";
            YellowdropsUngaga2[6] = "Maybe I was over exaggerating, I would^totally beat Ʊ in a duel.¤Just don´t tell him I said that!";
            YellowdropsUngaga2[7] = "I had a question Mr.Ų when you^look down at that little blue marble.^Do you ever get homesick?";
            YellowdropsUngaga2[9] = "I love your head wrap! Can you teach me^how to make one?! I´ll make it Yellow^Drop´s new fashion trend!";
            YellowdropsUngaga2[10] = "I was surprised when Jive told me that^Muska Lacka is home to a legendary^family of fish!";
            YellowdropsUngaga2[11] = "Ӿ once tried to jump into that^yellow water, she looked disappointed^when Ť stopped her!¤It seems like that little cat thinks^that she´s the boss of everyone haha!";
            YellowdropsUngaga2[12] = "Once this is all done I´m sure you´re^excited to be back home! Make sure you^tell everyone about the mighty warrior^Cheek okay?";


            //linda, lumba, salsa, flammi, flada, limbo, jive, tap, aily, cheek
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            YellowdropsOsmond[2] = "You should get Ʀ to teach you^some Genie magic, imagine how strong^you would be!";
            YellowdropsOsmond[3] = "You´re such a strong leader, once you^retire from the Moon Factory you could^start a new career as a coliseum fighter^like Minotaur Joe!";
            YellowdropsOsmond[4] = "Thank you for all the hard work you^do Boss!";
            YellowdropsOsmond[5] = "I saw that Ť has a fishing rod,^it´s a shame we have no fishing spots^here!";
            YellowdropsOsmond[6] = "Ų´s wisdom never ceases to amaze^me!¤We were talking about the Sun and Moon^Temple as well as the Signits.¤The Moon People and the Desert Tribe^share many similarities.";
            YellowdropsOsmond[7] = "You´re so lucky, now you can travel to^Blue Terra! I heard that they think the^Moon is made of cheese!";
            YellowdropsOsmond[9] = "The Sun Giant has such a magnificent^design, truly the pride of Yellow Drops!";
            YellowdropsOsmond[10] = "Don´t tell the other allies I said this^but you´re definitely the star of the^show Boss!";
            YellowdropsOsmond[11] = "Boss I heard that Ӿ was causing^some mischief in the Moon Factory...¤Do you think it´s wise to let an^adventurous cat like her to wander^around unsurprised?";
            YellowdropsOsmond[12] = "I can´t imagine what the Dark Genie is^thinking trying to destroy Terra but we^cannot let this evil go unanswered.";

            YellowdropsOsmond2[2] = "Finding the courage to be a leader is^challenge for most people. Thanks for^everything Boss!";
            YellowdropsOsmond2[3] = "Minotaur Joe is an inspiration to^us all!";
            YellowdropsOsmond2[4] = "I was looking down at Blue Terra with a^telescope and I noticed a large spire,^a shaft like structure?¤Was that always there?";
            YellowdropsOsmond2[5] = "I wish Ť would teach me how^to fish, we could go on a big fishing^trip on Terra one day!";
            YellowdropsOsmond2[6] = "It´s been so long but I wonder how the^Moon People and the Desert Tribe´s^relationship began?";
            YellowdropsOsmond2[7] = "Make sure to get me a souvenir from^Blue Terra, something cool!";
            YellowdropsOsmond2[9] = "I try to avoid going into the Moon Sea^unless we´re going to the coliseum, you^should come with us!";
            YellowdropsOsmond2[10] = "It was not too long ago when we were^children, you were always the trouble^maker haha!";
            YellowdropsOsmond2[11] = "The Moon People and Blue Terra have a^lot of shared history so it´s nice that^we can reconnect!";
            YellowdropsOsmond2[12] = "Who exactly is Ӿ?^Why did Ť go through the^effort of bringing his pet cat all the^here!¤Perhaps there´s more to that cat then^meets the eye...¤Aha, Ӿ is the team´s mascot!";


             //Storage guard, kiwi, mango, suger, natade, mousse
             //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
             // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            BrownbooXiao[6] = "Make sure to watch your step when^walking around Brownboo, I´ve seen many^slip into the water below but^a cat!¤They say there´s a first time for^everything but for your sake I hope^that´s not true!";
            BrownbooXiao[7] = "What are you doing here kitty,^are you lost? I think I saw^Ť walking over there, you^should catch up with him while you can!";
            BrownbooXiao[8] = "I think you would love to live here,^there are all sorts of fun activities^for a cat to partake in here at Brownboo^Village!¤You could look for fish in the streams^or even search for some delicious moon^fruit!";
            BrownbooXiao[10] = "Agh it´s the legendary white tiger, you^destroyed Matataki and now you´ve come^for us!¤Please, spare this village, why not go^to Queens? I hear they have all sorts of^tasty fish!¤Just don´t eat me (sob)";
            BrownbooXiao[11] = "Did you come from the hunter´s village^of Matataki?¤You better be careful, I saw a villager^wearing a cat as a scarf.¤Knowing your luck you´d probably end up^on a dinner plate or in someone´s closet^as a fashion accessory!";
            BrownbooXiao[12] = "Are you Ť´s pet cat? You are so^lucky he seems like such a nice guy!¤I hope he´s feeding you well during this^adventure!";

            BrownbooXiao2[6] = "Sorry to disappoint you but no fish can^be found in our waters, sorry!";
            BrownbooXiao2[7] = "Ohhhhh, I can feel that^magic emanating from you.¤What´s going on, is there something^Ť is not telling us about you?";
            BrownbooXiao2[8] = "Maybe when your done adventuring with^Ť you can come back and play^with me!";
            BrownbooXiao2[10] = "Oh wait, you are definitely not the^white tiger!¤Please don´t tell anyone about this,^I´ll never stop hearing about it from^the others!";
            BrownbooXiao2[11] = "I´m sure that people often discredit you^because you are a small cat but I bet you^are quite the fighter.¤If Ť and the allies ever need^back up I´m sure you´ll be the first one^to answer that call to arms!";
            BrownbooXiao2[12] = "I know it´s hard to believe but you are^helping Ť and the rest of the^allies in your own way, by providing the^best kind of support!¤I bet if you actually saw the Dark Genie^you would claw his eyes out!";


            BrownbooGoro[6] = "You look like a strong hunter, if you^ever need to store anything come to me!";
            BrownbooGoro[7] = "We almost never get any visitors from^Matataki, I´m pleased to meet you!¤Welcome to Brownboo Village, make^yourself at home neighbor!¤Just make sure not to hunt any of our^villagers. Just because we look like^beasts doesn´t mean we should be hunted^like them.";
            BrownbooGoro[8] = "Over the years the Moon People of^Brownboo have forgotten how to use^magic.¤Since ancient times we were the^guardians of Terra but things have been^pretty easygoing!¤The carefree life was a good fit for us.^That is until the Dark Genie returned.^Now we´re rethinking our way of life...";
            BrownbooGoro[10] = "We would often visit Matataki Village at^night when all the hunters were asleep!¤We´d tip toe all throughout the night!¤There was one villager who was pretty^friendly, a rather large fellow named^Cous Cous! We´d often visit their house^to get treats!";
            BrownbooGoro[11] = "There was once a time where Sugar and^Mango went exploring into the Wise Owl^Forest but they were scared off by a^giant silver serpent.¤This beast´s scales were as pale as the^two moons and it´s fangs looked like^something from our worst nightmares.¤Poor Mango couldn´t go to bed for weeks!";
            BrownbooGoro[12] = "Once upon a time, long long ago the^Dark Genie ravaged all of Terra. That is^all except for Brownboo Village.¤This tiny village was the only surviving^bastion from the influence of the Dark^Genie.¤The remaining survivors from all over^Terra journeyed to this village as a^safe haven.¤Many years have since passed and the^Dark Clouds have once again begun to^stir.¤It´s time to make a stand brave Hunter,^like all of those who came before you,^and for the sake of those who will come^after you.¤For the sake of all the life on Terra.";

            BrownbooGoro2[6] = "Keep an eye on that Ӿ she´s^quite the mischievous little cat but I^get an odd vibe from her.¤I can´t quite put my paw on it but^something tells me she is not like other^cats...";
            BrownbooGoro2[7] = "Keep this between us but some of the^other villagers of Brownboo were afraid^of the hunters of Matataki.¤We believed that if you found our quiet^village you would hunt us all down^mercilessly.¤I´m glad that we were´re wrong about^that.";
            BrownbooGoro2[8] = "Unlike most of the other villagers of^Brownboo, I still remember how to use^some magic!¤Yes my friend, you are in the presence^of  the amazingly legendary Mango the^Magician!¤For the first trick I will make your^nose disappear!¤Wait, was your nose always that tiny or^did my magic actually work?";
            BrownbooGoro2[10] = "Mr.Mustache´s shop was our go to spot^for late night snacks!¤I think we ever ran into a laughapockle^there once!";
            BrownbooGoro2[11] = "If you ever outgrow that boring old^wooden mallet can I have it?¤Picture this, the great hunter Natade of^Brownboo Village!¤That mean old genie better look out^because I´d give him a good bonk on the^head!";
            BrownbooGoro2[12] = "The culture of Matataki is fascinating,^it seems like you´re people have always^valued hunting and community.¤It seems like the hunt is what brings^you together as a community, both are^intertwined! That´s so interesting to^think about, the most we do here in¤Brownboo is pick Moon Fruit and share^jokes!";


            //Storage guard, kiwi, mango, suger, natade, mousse
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            BrownbooRuby[6] = "I´m glad that you are indeed a friendly^Genie!¤Unless you are waiting for the right^moment to trick us!¤I really hope that´s not the case...";
            BrownbooRuby[7] = "Genie´s can´t be all bad, I mean here^you are helping out Ť and his^friends.¤You don´t have to do that, but here you^are!¤I guess it´s proof that you can´t always^judge a book by it´s cover, only in this^case don´t judge a Genie haha!";
            BrownbooRuby[8] = "We´ve been living in the village of^Brownboo for generations!¤Initially, the Moon People used to^maintain a good relationship with the^humans of Terra who live out in the far¤away village of Muska Lacka but we^wanted an easier life!";
            BrownbooRuby[10] = "Brownboo has always been a village of^solitude, the Dark Genie didn´t think of^attacking us!¤We´ve been living in secret for many^lifetimes so they probably didn´t know^where we were.¤It was so sad, we all heard the voices^of Terra cry out for help, only to be^silenced in unison...¤I´m glad that Ť answered that^call for help.¤Life on Terra can still be saved.";
            BrownbooRuby[11] = "The village of Matataki hunts down^beasts, a stark contrast to it´s^neighboring village of Norune where^people live in harmony with the beasts¤such as Dran. Meanwhile Queens is a^bustling port town that doesn´t have any^relationship with beasts at all!¤Terra is such an interesting place when^you think about it!";
            BrownbooRuby[12] = "I found a witch parfait the other day^and it was delicious, I can see why they^are your favorite food!";

            BrownbooRuby2[6] = "I hope I didn´t hurt your feelings.¤If Ť and his friends trusts^you, then I trust you. You are always^welcome in Brownboo.";
            BrownbooRuby2[7] = "I wonder how your magical armband works^in combat?¤Perhaps it´s magic older than anyone of^us, magic from a by gone era...¤Just how old are you Ʀ?";
            BrownbooRuby2[8] = "The Moon People of Brownboo haven´t^visited Yellow Drops for countless^years.¤Sometimes I would gaze up at the two^moons and wonder what´s out there.¤Can you be homesick for a place that you^have never been to? A culture you´ve^never really known?¤Who knows, one things for sure when all^this is over we need to pay them a^visit.¤You can join us Ʀ as long as^you take responsibility of that^mischievous Ӿ!";
            BrownbooRuby2[10] = "Thank you Ʀ for fighting^against the Dark Genie.";
            BrownbooRuby2[11] = "Mango was talking about you the other^day, they said that you were trying to^be the best genie in history!¤I think they´d love to be your student^one day, of course once this whole Dark^Genie fiasco is over!";
            BrownbooRuby2[12] = "I´d really appreciate it if you could^bring us some Witch Parfaits from Queens^if you ever get the chance!";


            //Storage guard, kiwi, mango, suger, natade, mousse
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            BrownbooUngaga[6] = "I think if you gave up scorpion jerky,^you would probably be stronger.¤Why not eat Moon Fruit instead?";
            BrownbooUngaga[7] = "I wonder how you got so tall, maybe it´s^all of the scorpion jerky you´ve been^eating!";
            BrownbooUngaga[8] = "We´ve been living in Brownboo for^generations!¤Initially the Moon People used to^maintain a good working relationship^with the humans of Terra in Muska Lacka^but it started being too much of a¤commitment and we wanted an easier life!^It´s not like we made the promise,^rather it was our ancestors.¤Should we be held responsible for the^decisions of our forebears, that doesn´t^seem very fair does it.";
            BrownbooUngaga[10] = "I always have been curious, why did you^choose to use a fighting stick as your^weapon of choice?¤I think the weapon that someone chooses^says a lot about their personality.¤Perhaps you like to keep the enemy at a^distance because you don´t want to get^hurt, but you don´t seem like the overly^defensive type...¤Is there someone you care for, aha that^is probably it!¤Make sure you finish this journey in one^piece, make that special person proud!";
            BrownbooUngaga[11] = "Sometimes I like to sit along the pier^and just listen to the waves.¤I think about a lot of things like all^of my friends here in Brownboo and even^life on Terra.¤Do you think that we´ll get rid of the^Dark Genie for good?¤I just want us all to live in peace^without the fear of being trapped in^Atla!";
            BrownbooUngaga[12] = "Some of you lost loved ones from the^Dark Genie´s attack, we can live here in^protection and comfort but we are not^numb to the suffering of the outside¤world. We, the people of Brownboo, are^here for you.";

            BrownbooUngaga2[6] = "I don´t do well in fighting but one^thing I always excelled at was hoarding^items!¤If you ever need to store items bring^them to me!";
            BrownbooUngaga2[7] = "If the Dark Genie could grant me one^wish I would ask to be taller...";
            BrownbooUngaga2[8] = "I had a dream the other day that humans,^moon people and monsters all lived in^harmony.¤Everything was peaceful until the Opars^attacked.¤Never turn your back on the Opars...";
            BrownbooUngaga2[10] = "Mousse and I were enjoying a relaxing^walking through the Wise Owl Forest when^a giant orange King Prickly landed on^Mousse´s head!¤It was the funniest thing ever!";
            BrownbooUngaga2[11] = "I don´t get how the Dark Genie was freed^from his prison...";
            BrownbooUngaga2[12] = "The important thing is that you are all^healthy and safe, to be able to fight^means you must first have your health.¤Don´t forget that Ų, health^always comes first.";


            BrownbooOsmond[6] = "I don´t think Brownboo will ever adopt^the technology that is used in Yellow^Drops, although having a machine to pick¤all of the Moon Fruit for us would be^really handy!";
            BrownbooOsmond[7] = "Wow, are you serious?¤I´ve never seen an outsider who is a^Moon Person before! This is actually^surreal...¤I hope you enjoy your time at Brownboo,^tell everyone back home we say hello!";
            BrownbooOsmond[8] = "The Moon People are well known for their^skills in magic here on Terra, but^Yellow Drops has been leading the way in^technology.¤It makes me wonder, will technology^replace magic one day?";
            BrownbooOsmond[10] = "For an outsider, you are very polite!^It´s hard to believe that you are the^boss of the Moon Factory, we can use a^good leader here in Brownboo!¤Our leader is a bit of an odd duck to^say the least but we value his guidance^all the same haha!";
            BrownbooOsmond[11] = "Against all odds, Ť and his^allies have been fighting against the^Genie´s nefarious work.¤What´s even better is that they are^actually making a difference!";
            BrownbooOsmond[12] = "Usually whenever we get an outsider in^the village, we tie them up and put them^through a trial of questions.¤It´s pretty fun!";

            BrownbooOsmond2[6] = "Thanks for visiting Brownboo Ō^we don´t get very many outsiders here!";
            BrownbooOsmond2[7] = "If only we knew that the Genie would^come back, we would have maintained^better contact with our people back^home...¤I guess there´s nothing that could be^done about that now.¤Moving forward, we can make a change for^the better and stay in contact.¤Life is too short to stay distant from^one another.";
            BrownbooOsmond2[8] = "You may think we live a primitive life^here in Brownboo compared to the^technologically rich society of Yellow^Drops.¤In reality, we value the simple things^in life...¤Although, having a heli-pack like that^would be pretty cool!";
            BrownbooOsmond2[10] = "Yikes, is that a gun?! Watch where you^point that thing, you´ll poke someone´s^eye out!";
            BrownbooOsmond2[11] = "I don´t think we did a good job staying^in touch with everyone in Yellow Drops,^make sure you tell them that we´re all^doing well and that we can handle any¤challenge that comes our way. Dark Genie^or otherwise!";
            BrownbooOsmond2[12] = "I saw you walking side by side with^Ų and boy did he make you look^tiny!¤Don´t get me wrong, Moon People are not^known for their towering heights but^standing next to someone taller than you^didn´t do any favors!";


            //Storage guard, kiwi, mango, suger, natade, mousse
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue

            DarkheavenXiao = "I must admit that prior to this^cataclysm, I had a hard time^trusting humans.¤However, I watched from afar how each^of the villagers of Norune did their^part to look after a stray like you,^as if you were one of their own.¤Their actions and kindness opened^my eyes to the great potential that^humanity holds. Perhaps that is what^led me to trust your friend Ť.¤Thank you for teaching this old^fairy a valuable lesson Ӿ.";
            DarkheavenXiao2 = "This is the final act Ӿ.^Although you grew up as a stray, know^that Ť and his allies cared^for you as if you were family.¤In order for peace to return to Terra,^the threat of the Dark Genie^must be vanquished.¤Take caution as there is no guarantee^that you will all survive this battle...¤Be Strong Ӿ, fight hard and^defend your allies: your family.";
            DarkheavenGoro = "Once upon a time, long long ago the^Dark Genie ravaged all of Terra: that^is all except your neighboring^village of Brownboo.¤This tiny village hidden away deep^in the Forest was once the only^surviving bastion from the influence^of the Dark Genie.¤The remaining survivors from all over^Terra journeyed to that village^as a safe haven.¤Many years have since passed and the^Dark Clouds have once again begun to^stir, the black winds have begun to howl^to the two moons on this night^of our final act.¤We must make haste Ʊ.";
            DarkheavenGoro2 = "It´s time to make a stand brave Hunter,^like all of those hunters who came^before you: in the name of the young^who will come after you.¤For the sake of all the life on^Terra... For the sake of your Father.¤Make him proud Ʊ,^he´ll always be watching you.¤May the Spirits guide your way.";
            DarkheavenRuby = "Whether it truly be for the personal^glory of proving that you are the best^Genie or a genuine concern for the^fate of Terra, it was very admirable^of you to aid Ť on his quest.¤To involve yourself in the affairs of^others, let alone aiding human, you^now have your chance to prove you are^indeed the most powerful genie in^all of the land.¤But know this, the Dark Genie will^not go out without a fight...";
            DarkheavenRuby2 = "This is the final battle Ʀ,^Genie against Genie, magic against magic,^the Spirits cry out as you clash.¤The fate of Terra depends on which^side will prevail!¤This perilous journey has taken you^and your allies all around Terra and^now it´s finally met it´s end.";
            DarkheavenUngaga = "On that tragic night when the Genie^ravaged Terra, you fought to protect^your village.¤You fought to protect the^village you called home.¤As fate would have it you lost^everything, even the will to live...¤Despite the Genie´s best efforts to^destroy life on Terra, here you stand.¤That´s a testament to the strength^you and your allies wield and the will^to succeed.¤Know that you are not fighting to^defend just your kinsfolk but also^those who you matter to you:^for Mikara.";
            DarkheavenUngaga2 = "The warriors of Muska Lacka have^always been renowned for their bravery.¤The Spirits and I have watched from^afar for generations as the desert^tribes clashed on the battlefield.¤Ų, you possess a skill which^many warriors do not: compassion.¤Perhaps after the threat of the Genie^has been vanquished you can use that^compassion to unify the tribes and^bring peace to the desert.";
            DarkheavenOsmond = "I must say, when Ť and I^travelled to Yellow Drops I took the^opportunity to look down upon Terra^from the Moons.¤Looking at our world, our home, the^joy and suffering, it was a sobering^experience.¤Mankind has often dealt with an issue^of self-importance, Flagg Gilgister^is proof of that, but when I look¤at Ť and his allies I see a^band of brave adventurers taking^a stand to defend all life.¤I humbly thank you for involving^yourself in the affairs of Terra.¤Like it or not but for the moment,^this is where we make our stand.¤To preserve and cherish all life^in our world.";
            DarkheavenOsmond2 = "I wonder how the magical power of the^Dark Genie will compare with the cutting^edge technology of Yellow Drops´^greatest inventor.¤I must say that strange weapon you^wield scares me ohohohohoho!";



            //macho, gaffer, gina, laura, alnet, pike, komacho, carl, paige, renee, claude, hag
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            NoruneFinishedDialogue[0] = "Fighting is what a person lives for,^sometimes we forget this and lose^our way. The most important thing is^that we pick ourselves up^and continue the fight.¤Never lose your smile my friend.";
            NoruneFinishedDialogue[1] = "Being a merchant as long as I have,^you see some pretty amazing things.^Never would have thought I would^see a gem like Ť´s.";
            NoruneFinishedDialogue[2] = "Gina had a dream that the Dark Genie^came to the village to say sorry,^we all had food and played.^It was so much fun.";
            NoruneFinishedDialogue[3] = "Maybe with the Dark Genie out, it^will give the Mayor second thoughts^about being noisy!¤For all we know the Genie could^have targeted us because of that^awful commotion caused by the mayor!";
            NoruneFinishedDialogue[4] = "Thank you for helping the village^everyone, make sure Ť doesn´t^fall for anyone else on his travels or^else it would break poor Paige´s heart.";
            NoruneFinishedDialogue[5] = "You know, once this is all said and^done, I´ll have to reward you all with^a fishing trip as long as you make^sure everyone behaves.";
            NoruneFinishedDialogue[6] = "It´s hard to believe that in an instant^the Genie was able to destroy our^village, everything we hold dear.¤You all brought it back,^we´re in your debt.";
            NoruneFinishedDialogue[7] = "Don´t tell my older sister but I´m^secretly planning on joining your^adventure, I don´t tell Alnet or^she will get angry!";
            NoruneFinishedDialogue[8] = "It´s hard to believe that Norune Village´s^Ť is leading a band of warriors^that are going to stop the Dark Genie.¤You´re all special in your own way^but Ť is special to me...^Take care of him.";
            NoruneFinishedDialogue[9] = "Even after the Genie destroyed our^village, the morning sun continued^to rise each day, this never changed.¤I can´t imagine losing my son or our^loved ones like that again. Thank you^for helping restore the world.";
            NoruneFinishedDialogue[10] = "Do you think if we gave the Dark Genie^a box full of candies he would leave^us all alone? I mean, that works^for me all the time.";
            NoruneFinishedDialogue[11] = "I´ve been gifted by the Spirits with^the ability to foresee the future,^however for the first time I´m^afraid to use this gift.¤I´m afraid of what I will see.^This Genie is a fearsome opponent^indeed, be sure to take caution.";
            NoruneFinishedDialogue[12] = "Nothing here.";


            //ro, annie, momo, pao, gob, kye, baron, cacao, kululu, bunbuku, couscous, mr mustache
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MatatakiFinishedDialogue[0] = "I lived long enough to know that^it´s not easy for people to put aside^their differences to solve a problem,^but here we are.¤The people of Matataki are^forever in your debt.";
            MatatakiFinishedDialogue[1] = "It brings a tear to my eye seeing^young people band together for a single^cause, to help save the world^from this cataclysm.¤Thank you for fixing our^home and our lives.";
            MatatakiFinishedDialogue[2] = "Thanks for finishing our home,^do you think you can build^a few more stores?¤Mr.Mustache´s shop can be a little^expensive and I only have so much^Gilda in my wallet!";
            MatatakiFinishedDialogue[3] = "This village is full of the strongest^hunters, but you are by far the^best builders here.";
            MatatakiFinishedDialogue[4] = "I bet you would have finished^rebuilding my home if you had^a better diet.¤Haha, I´m only kidding.^Any time you feel hungry,^my cooking services are yours!";
            MatatakiFinishedDialogue[5] = "Just you wait, in the future we´ll^be building houses with giant flying^robots with huge eyes and hands, it´ll^even be piloted by dwarves!¤You´ll see, you´ll all see!";
            MatatakiFinishedDialogue[6] = "Congratulations young warriors,^you´ve made the hunters of Matataki^very proud.";
            MatatakiFinishedDialogue[7] = "My home is whole once again and^I´m able to write music again.¤Perhaps I´ll write a song about the^young hunter Ʊ, son of Fudoh.";
            MatatakiFinishedDialogue[8] = "You helped me when I was scared...^thank you very much.";
            MatatakiFinishedDialogue[9] = "Wow, my house is better then it´s^ever been. Maybe being attacked by^the Genie was a blessing!¤Don´t tell the others I said that!";
            MatatakiFinishedDialogue[10] = "Thank you so much friend, I wish^I could give you a hug but I don´t^want to hurt you!";
            MatatakiFinishedDialogue[11] = "Don´t think that I owe you anything^for rebuilding my shop, you were^doing your civic duty for the^people of Matataki.";

            //king, sam, ruty, suzy, lana, basker, stew, joker, phil, jake, wilder, yaya, jack
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            QueensFinishedDialogue[0] = "The people of Queens owe your group^a great debt of gratitude, but^remember I owe you nothing now.¤I could have stopped the Genie myself^but the people of Queens needed me.";
            QueensFinishedDialogue[1] = "We may not be defeating the Dark^Genie, but we´ll keep Queens and her^people safe from criminals like King.";
            QueensFinishedDialogue[2] = "I spent so much time fishing to earn^a living that I forgot to take it^easy and live life slow.¤This is the lesson I want to take^from this. Gilda isn´t everything, it^doesn´t hurt to enjoy life every^now and then.";
            QueensFinishedDialogue[3] = "You know, I´m not as strong as you all^but I do want to be stronger.¤I guess what I´m trying to say is^that where we are today isn´t where^we will be tomorrow.";
            QueensFinishedDialogue[4] = "Thank you for all your help, it´s^scary to think that the Dark Genie^made everything on Terra disappear^in the blink of an eye.¤Where would we be without your help?";
            QueensFinishedDialogue[5] = "Thanks to you I can get back to^business, I hope King doesn´t decide^to raise taxes again.";
            QueensFinishedDialogue[6] = "King is getting ready to run for mayor.^Defeating the Dark Genie is^our #1 campaign goal.";
            QueensFinishedDialogue[7] = "Who knows, maybe when the Dark Genie^obliterates the world, more rare^gems will resurface.¤However, destroying the world would^be bad for business...^You all need to stop him.";
            QueensFinishedDialogue[8] = "What happened in Queens with La Saia^is nothing short of tragic, but^today we stand before many brave^warriors who are taking a stand^to save all of Terra.¤May you find peace in your journey.";
            QueensFinishedDialogue[9] = "Being King´s assistant is a big job,^much more challenging than fighting^that Dark Genie wimp.";
            QueensFinishedDialogue[10] = "I´m not surprised that you all^fixed up the police station.¤You´re led by Ť from Norune^and he learned everything he knows^from my family in Norune haha!";
            QueensFinishedDialogue[11] = "I heard from the other villagers^that you were all complaining about^my Pumpkin Panty Fortune Telling.¤You know as much as me that if it^wasn´t for my services, your quest^would not have moved forward!";
            QueensFinishedDialogue[12] = "Jack´s shop is back in business,^if you ever need weapons or powders^I´m your guy! Just don´t let^Sheriff Wilder know.";

            //jibubu, chief bonka, zabo, mikara, nagita, devia, enga, brooke, gron, toto, gosuke
            //Ť = Toan, Ӿ = Xiao, Ʊ = Goro, Ʀ = Ruby, Ų = Ungaga, Ō = Osmond
            // ^ = Next Line, ¤ = Next Dialogue Bubble. 40 symbols max per line, more than that can clip dialogue
            MuskaFinishedDialogue[0] = "To think that someone of your beauty^standards actually rebuilt my house^down to the last elegant detail.";
            MuskaFinishedDialogue[1] = "In the blink of an eye everything^disappeared: villagers, animals,^all life in the desert.¤The Genie is always going to be a^threat to the world. The last thing^we should do is continue these^age old tribal conflicts.¤Thank you for your help.";
            MuskaFinishedDialogue[2] = "Hey thanks for fixing up the place!^I was really worried, I thought that^I may have to find somewhere^else to live!¤Home is where the heart is and^I´m forever grateful.";
            MuskaFinishedDialogue[3] = "Words cannot express the gratitude that^all the villagers have for you.¤Thank you for all the hard work.";
            MuskaFinishedDialogue[4] = "Hmph... I guess you did a good job^putting our house back together.";
            MuskaFinishedDialogue[5] = "Thank you so much for fixing our home,^I can´t believe that the Dark Genie^actually had us trapped in those^weird bubbles.";
            MuskaFinishedDialogue[6] = "Never in all my years would I have^thought that everything in Muska Lacka^would disappear the way that it did.¤I´m very pleased with my house,^thank you.";
            MuskaFinishedDialogue[7] = "Many warriors have come and gone,^your group is special.¤Keep fighting the darkness and^never give up. Things won´t be easy^moving forward. I am in your debt.";
            MuskaFinishedDialogue[8] = "You must be real proud of yourself^rebuilding this jail cell, would it^have killed you to not include^the locked gate?";
            MuskaFinishedDialogue[9] = "When I get older I want to join you^on your adventures. Old Enga has^even been teaching me how to fight!";
            MuskaFinishedDialogue[10] = "Wait, how are you reading this?";
        }
    }
}
