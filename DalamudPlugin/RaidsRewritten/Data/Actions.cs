using System.Collections.Generic;

namespace RaidsRewritten.Data;

public class Actions
{
    public static readonly HashSet<uint> DamageActions =
    [
        // ================ PALADIN ================
        9, // Fast Blade
        15, // Riot Blade
        16, // Shield Bash
        //17, // Sentinel
        //20, // Fight or Flight
        21, // Rage of Halone
        //22, // Bulwark
        23, // Circle of Scorn
        24, // Shield Lob
        //27, // Cover
        //28, // Iron Will
        29, // Spirits Within
        //30, // Hallowed Ground
        3538, // Goring Blade
        3539, // Royal Authority
        //3540, // Divine Veil
        //3541, // Clemency
        //3542, // Sheltron
        7381, // Total Eclipse
        //7382, // Intervention
        7383, // Requiescat
        7384, // Holy Spirit
        //7385, // Passage of Arms
        16457, // Prominence
        16458, // Holy Circle
        16459, // Confiteor
        16460, // Atonement
        16461, // Intervene
        //25746, // Holy Sheltron
        25747, // Expiacion
        25748, // Blade of Faith
        25749, // Blade of Truth
        25750, // Blade of Valor
        //32065, // Release Iron Will
        36918, // Supplication
        36919, // Sepulchre
        //36920, // Guardian
        36921, // Imperator
        36922, // Blade of Honor

        // ================ WARRIOR ================
        31, // Heavy Swing
        37, // Maim
        //38, // Berserk
        //40, // Thrill of Battle
        41, // Overpower
        42, // Storm's Path
        //43, // Holmgang
        //44, // Vengeance
        45, // Storm's Eye
        46, // Tomahawk
        //48, // Defiance
        49, // Inner Beast
        51, // Steel Cyclone
        //52, // Infuriate
        3549, // Fell Cleave
        3550, // Decimate
        //3551, // Raw Intuition
        //3552, // Equilibrium
        7386, // Onslaught
        7387, // Upheaval
        //7388, // Shake It Off
        //7389, // Inner Release
        16462, // Mythril Tempest
        16463, // Chaotic Cyclone
        //16464, // Nascent Flash
        16465, // Inner Chaos
        //25751, // Bloodwhetting
        25752, // Orogeny
        25753, // Primal Rend
        //32066, // Release Defiance
        //36923, // Damnation
        36924, // Primal Wrath
        36925, // Primal Ruination

        // ================ DARK KNIGHT ================
        3617, // Hard Slash
        3621, // Unleash
        3623, // Syphon Strike
        3624, // Unmend
        //3625, // Blood Weapon
        //3629, // Grit
        3632, // Souleater
        //3634, // Dark Mind
        //3636, // Shadow Wall
        //3638, // Living Dead
        3639, // Salted Earth
        3641, // Abyssal Drain
        3643, // Carve and Spit
        //7390, // Delirium
        7391, // Quietus
        7392, // Bloodspiller
        //7393, // The Blackest Night
        16466, // Flood of Darkness
        16467, // Edge of Darkness
        16468, // Stalwart Soul
        16469, // Flood of Shadow
        16470, // Edge of Shadow
        //16471, // Dark Missionary
        16472, // Living Shadow
        //25754, // Oblation
        25755, // Salt and Darkness
        25756, // Salt and Darkness
        25757, // Shadowbringer
        //32067, // Release Grit
        //36926, // Shadowstride
        //36927, // Shadowed Vigil
        36928, // Scarlet Delirium
        36929, // Comeuppance
        36930, // Torcleaver
        36931, // Impalement
        36932, // Disesteem

        // ================ GUNBREAKER ================
        16137, // Keen Edge
        //16138, // No Mercy
        16139, // Brutal Shell
        //16140, // Camouflage
        16141, // Demon Slice
        //16142, // Royal Guard
        16143, // Lightning Shot
        16144, // Danger Zone
        16145, // Solid Barrel
        16146, // Gnashing Fang
        16147, // Savage Claw
        //16148, // Nebula
        16149, // Demon Slaughter
        16150, // Wicked Talon
        //16151, // Aurora
        //16152, // Superbolide
        16153, // Sonic Break
        16155, // Continuation
        16156, // Jugular Rip
        16157, // Abdomen Tear
        16158, // Eye Gouge
        16159, // Bow Shock
        //16160, // Heart of Light
        //16161, // Heart of Stone
        16162, // Burst Strike
        16163, // Fated Circle
        //16164, // Bloodfest
        16165, // Blasting Zone
        //25758, // Heart of Corundum
        25759, // Hypervelocity
        25760, // Double Down
        //32068, // Release Royal Guard
        //36934, // Trajectory
        //36935, // Great Nebula
        36936, // Fated Brand
        36937, // Reign of Beasts
        36938, // Noble Blood
        36939, // Lion Heart

        // ================ WHITE MAGE ================
        119, // Stone
        //120, // Cure
        121, // Aero
        //124, // Medica
        //125, // Raise
        127, // Stone II
        //131, // Cure III
        132, // Aero II
        //133, // Medica II
        //135, // Cure II
        //136, // Presence of Mind
        //137, // Regen
        139, // Holy
        //140, // Benediction
        3568, // Stone III
        //3569, // Asylum
        //3570, // Tetragrammaton
        //3571, // Assize
        //7430, // Thin Air
        7431, // Stone IV
        //7432, // Divine Benison
        //7433, // Plenary Indulgence
        //16531, // Afflatus Solace
        16532, // Dia
        16533, // Glare
        //16534, // Afflatus Rapture
        16535, // Afflatus Misery
        //16536, // Temperance
        25859, // Glare III
        25860, // Holy III
        //25861, // Aquaveil
        //25862, // Liturgy of the Bell
        //25863, // Liturgy of the Bell
        //25864, // Liturgy of the Bell
        //28509, // Liturgy of the Bell
        //37008, // Aetherial Shift
        37009, // Glare IV
        //37010, // Medica III
        //37011, // Divine Caress

        // ================ SCHOLAR ================
        //166, // Aetherflow
        167, // Energy Drain
        //185, // Adloquium
        //186, // Succor
        //188, // Sacred Soil
        //189, // Lustrate
        //190, // Physick
        //802, // Embrace
        //803, // Whispering Dawn
        //805, // Fey Illumination
        //3583, // Indomitability
        3584, // Broil
        //3585, // Deployment Tactics
        //3586, // Emergency Tactics
        //3587, // Dissipation
        //7434, // Excogitation
        7435, // Broil II
        7436, // Chain Stratagem
        //7437, // Aetherpact
        //7438, // Fey Union
        //7869, // Dissolve Union
        //16537, // Whispering Dawn
        //16538, // Fey Illumination
        16539, // Art of War
        16540, // Biolysis
        16541, // Broil III
        //16542, // Recitation
        //16543, // Fey Blessing
        //16544, // Fey Blessing
        //16545, // Summon Seraph
        //16546, // Consolation
        //16547, // Consolation
        //16548, // Seraphic Veil
        //16550, // Angel's Whisper
        //16551, // Seraphic Illumination
        //17215, // Summon Eos
        17864, // Bio
        17865, // Bio II
        17869, // Ruin
        17870, // Ruin II
        25865, // Broil IV
        25866, // Art of War II
        //25867, // Protraction
        //25868, // Expedient
        37012, // Baneful Impaction
        //37013, // Concitation
        //37014, // Seraphism
        //37015, // Manifestation
        //37016, // Accession
        //37037, // Emergency Tactics

        // ================ ASTROLOGIAN ================
        //3594, // Benefic
        //3595, // Aspected Benefic
        3596, // Malefic
        3598, // Malefic II
        3599, // Combust
        //3600, // Helios
        //3601, // Aspected Helios
        //3603, // Ascend
        //3606, // Lightspeed
        3608, // Combust II
        //3610, // Benefic II
        //3612, // Synastry
        //3613, // Collective Unconscious
        //3614, // Essential Dignity
        3615, // Gravity
        //7439, // Earthly Star
        //7440, // Stellar Burst
        //7441, // Stellar Explosion
        7442, // Malefic III
        7444, // Lord of Crowns
        //7445, // Lady of Crowns
        //8324, // Stellar Detonation
        //16552, // Divination
        //16553, // Celestial Opposition
        16554, // Combust III
        16555, // Malefic IV
        //16556, // Celestial Intersection
        //16557, // Horoscope
        //16558, // Horoscope
        //16559, // Neutral Sect
        25871, // Fall Malefic
        25872, // Gravity II
        //25873, // Exaltation
        //25874, // Macrocosmos
        //25875, // Microcosmos
        //37017, // Astral Draw
        //37018, // Umbral Draw
        //37019, // Play I
        //37020, // Play II
        //37021, // Play III
        //37022, // Minor Arcana
        //37023, // the Balance
        //37024, // the Arrow
        //37025, // the Spire
        //37026, // the Spear
        //37027, // the Bole
        //37028, // the Ewer
        37029, // Oracle
        //37030, // Helios Conjunction
        //37031, // Sun Sign

        // ================ SAGE ================
        24283, // Dosis
        //24284, // Diagnosis
        //24285, // Kardia
        //24286, // Prognosis
        //24287, // Egeiro
        //24288, // Physis
        24289, // Phlegma
        //24290, // Eukrasia
        //24291, // Eukrasian Diagnosis
        //24292, // Eukrasian Prognosis
        24293, // Eukrasian Dosis
        //24294, // Soteria
        //24295, // Icarus
        //24296, // Druochole
        24297, // Dyskrasia
        //24298, // Kerachole
        //24299, // Ixochole
        //24300, // Zoe
        //24301, // Pepsis
        //24302, // Physis II
        //24303, // Taurochole
        24304, // Toxikon
        //24305, // Haima
        24306, // Dosis II
        24307, // Phlegma II
        24308, // Eukrasian Dosis II
        //24309, // Rhizomata
        //24310, // Holos
        //24311, // Panhaima
        24312, // Dosis III
        24313, // Phlegma III
        24314, // Eukrasian Dosis III
        24315, // Dyskrasia II
        24316, // Toxikon II
        //24317, // Krasis
        //24318, // Pneuma
        //27524, // Pneuma
        //28119, // Kardia
        37032, // Eukrasian Dyskrasia
        37033, // Psyche
        //37034, // Eukrasian Prognosis II
        //37035, // Philosophia
        //37036, // Eudaimonia

        // ================ MONK ================
        53, // Bootshine
        54, // True Strike
        56, // Snap Punch
        61, // Twin Snakes
        62, // Arm of the Destroyer
        //65, // Mantra
        66, // Demolish
        //69, // Perfect Balance
        70, // Rockbreaker
        74, // Dragon Kick
        3543, // Tornado Kick
        3545, // Elixir Field
        3547, // the Forbidden Chakra
        //4262, // Form Shift
        //7394, // Riddle of Earth
        //7395, // Riddle of Fire
        //7396, // Brotherhood
        16473, // Four-point Fury
        16474, // Enlightenment
        16476, // Six-sided Star
        25761, // Steel Peak
        //25762, // Thunderclap
        25763, // Howling Fist
        25764, // Masterful Blitz
        25765, // Celestial Revolution
        //25766, // Riddle of Wind
        25767, // Shadow of the Destroyer
        25768, // Rising Phoenix
        25769, // Phantom Rush
        25882, // Flint Strike
        //36940, // Steeled Meditation
        //36941, // Inspirited Meditation
        //36942, // Forbidden Meditation
        //36943, // Enlightened Meditation
        //36944, // Earth's Reply
        36945, // Leaping Opo
        36946, // Rising Raptor
        36947, // Pouncing Coeurl
        36948, // Elixir Burst
        36949, // Wind's Reply
        36950, // Fire's Reply

        // ================ DRAGOON ================
        75, // True Thrust
        78, // Vorpal Thrust
        //83, // Life Surge
        84, // Full Thrust
        //85, // Lance Charge
        86, // Doom Spike
        87, // Disembowel
        88, // Chaos Thrust
        90, // Piercing Talon
        92, // Jump
        //94, // Elusive Jump
        96, // Dragonfire Dive
        3554, // Fang and Claw
        3555, // Geirskogul
        3556, // Wheeling Thrust
        //3557, // Battle Litany
        7397, // Sonic Thrust
        7399, // Mirage Dive
        7400, // Nastrond
        16477, // Coerthan Torment
        16478, // High Jump
        16479, // Raiden Thrust
        16480, // Stardiver
        25770, // Draconian Fury
        25771, // Heavens' Thrust
        25772, // Chaotic Spring
        25773, // Wyrmwind Thrust
        //36951, // Winged Glide
        36952, // Drakesbane
        36953, // Rise of the Dragon
        36954, // Lance Barrage
        36955, // Spiral Blow
        36956, // Starcross

        // ================ NINJA ================
        2240, // Spinning Edge
        //2241, // Shade Shift
        2242, // Gust Slash
        //2245, // Hide
        2246, // Assassinate
        2247, // Throwing Dagger
        2248, // Mug
        2254, // Death Blossom
        2255, // Aeolian Edge
        2258, // Trick Attack
        2259, // Ten
        2260, // Ninjutsu
        2261, // Chi
        //2262, // Shukuchi
        2263, // Jin
        2264, // Kassatsu
        2265, // Fuma Shuriken
        2266, // Katon
        2267, // Raiton
        2268, // Hyoton
        2269, // Huton
        2270, // Doton
        2271, // Suiton
        //2272, // Rabbit Medium
        3563, // Armor Crush
        3566, // Dream Within a Dream
        7401, // Hellfrog Medium
        7402, // Bhavacakra
        //7403, // Ten Chi Jin
        16488, // Hakke Mujinsatsu
        //16489, // Meisui
        16491, // Goka Mekkyaku
        16492, // Hyosho Ranryu
        //16493, // Bunshin
        17413, // Spinning Edge
        17414, // Gust Slash
        17415, // Aeolian Edge
        17417, // Armor Crush
        17418, // Throwing Dagger
        17419, // Death Blossom
        17420, // Hakke Mujinsatsu
        18805, // Ten
        18806, // Chi
        18807, // Jin
        18873, // Fuma Shuriken
        18874, // Fuma Shuriken
        18875, // Fuma Shuriken
        18876, // Katon
        18877, // Raiton
        18878, // Hyoton
        18879, // Huton
        18880, // Doton
        18881, // Suiton
        25774, // Phantom Kamaitachi
        25775, // Phantom Kamaitachi
        25776, // Hollow Nozuchi
        25777, // Forked Raiju
        25778, // Fleeting Raiju
        25878, // Forked Raiju
        25879, // Fleeting Raiju
        36957, // Dokumori
        36958, // Kunai's Bane
        36959, // Deathfrog Medium
        36960, // Zesho Meppo
        36961, // Tenri Jindo

        // ================ SAMURAI ================
        7477, // Hakaze
        7478, // Jinpu
        7479, // Shifu
        7480, // Yukikaze
        7481, // Gekko
        7482, // Kasha
        7483, // Fuga
        7484, // Mangetsu
        7485, // Oka
        7486, // Enpi
        7487, // Midare Setsugekka
        7488, // Tenka Goken
        7489, // Higanbana
        7490, // Hissatsu: Shinten
        7491, // Hissatsu: Kyuten
        7492, // Hissatsu: Gyoten
        7493, // Hissatsu: Yaten
        //7495, // Hagakure
        7496, // Hissatsu: Guren
        //7497, // Meditate
        //7498, // Third Eye
        //7499, // Meikyo Shisui
        7867, // Iaijutsu
        16481, // Hissatsu: Senei
        //16482, // Ikishoten
        16483, // Tsubame-gaeshi
        16485, // Kaeshi: Goken
        16486, // Kaeshi: Setsugekka
        16487, // Shoha
        25780, // Fuko
        25781, // Ogi Namikiri
        25782, // Kaeshi: Namikiri
        //36962, // Tengentsu
        36963, // Gyofu
        36964, // Zanshin
        36965, // Tendo Goken
        36966, // Tendo Setsugekka
        36967, // Tendo Kaeshi Goken
        36968, // Tendo Kaeshi Setsugekka

        // ================ REAPER ================
        24373, // Slice
        24374, // Waxing Slice
        24375, // Infernal Slice
        24376, // Spinning Scythe
        24377, // Nightmare Scythe
        24378, // Shadow of Death
        24379, // Whorl of Death
        24380, // Soul Slice
        24381, // Soul Scythe
        24382, // Gibbet
        24383, // Gallows
        24384, // Guillotine
        24385, // Plentiful Harvest
        24386, // Harpe
        //24387, // Soulsow
        24388, // Harvest Moon
        24389, // Blood Stalk
        24390, // Unveiled Gibbet
        24391, // Unveiled Gallows
        24392, // Grim Swathe
        24393, // Gluttony
        //24394, // Enshroud
        24395, // Void Reaping
        24396, // Cross Reaping
        24397, // Grim Reaping
        24398, // Communio
        24399, // Lemure's Slice
        24400, // Lemure's Scythe
        //24401, // Hell's Ingress
        //24402, // Hell's Egress
        //24403, // Regress
        //24404, // Arcane Crest
        //24405, // Arcane Circle
        36969, // Sacrificium
        36970, // Executioner's Gibbet
        36971, // Executioner's Gallows
        36972, // Executioner's Guillotine
        36973, // Perfectio

        // ================ VIPER ================
        34606, // Steel Fangs
        34607, // Reaving Fangs
        34608, // Hunter's Sting
        34609, // Swiftskin's Sting
        34610, // Flanksting Strike
        34611, // Flanksbane Fang
        34612, // Hindsting Strike
        34613, // Hindsbane Fang
        34614, // Steel Maw
        34615, // Reaving Maw
        34616, // Hunter's Bite
        34617, // Swiftskin's Bite
        34618, // Jagged Maw
        34619, // Bloodied Maw
        34620, // Vicewinder
        34621, // Hunter's Coil
        34622, // Swiftskin's Coil
        34623, // Vicepit
        34624, // Hunter's Den
        34625, // Swiftskin's Den
        34626, // Reawaken
        34627, // First Generation
        34628, // Second Generation
        34629, // Third Generation
        34630, // Fourth Generation
        34631, // Ouroboros
        34632, // Writhing Snap
        34633, // Uncoiled Fury
        34634, // Death Rattle
        34635, // Last Lash
        34636, // Twinfang Bite
        34637, // Twinblood Bite
        34638, // Twinfang Thresh
        34639, // Twinblood Thresh
        34640, // First Legacy
        34641, // Second Legacy
        34642, // Third Legacy
        34643, // Fourth Legacy
        34644, // Uncoiled Twinfang
        34645, // Uncoiled Twinblood
        //34646, // Slither
        //34647, // Serpent's Ire
        35920, // Serpent's Tail
        35921, // Twinfang
        35922, // Twinblood

        // ================ BARD ================
        97, // Heavy Shot
        98, // Straight Shot
        100, // Venomous Bite
        //101, // Raging Strikes
        106, // Quick Nock
        //107, // Barrage
        110, // Bloodletter
        //112, // Repelling Shot
        113, // Windbite
        //114, // Mage's Ballad
        //116, // Army's Paeon
        117, // Rain of Death
        //118, // Battle Voice
        3558, // Empyreal Arrow
        //3559, // the Wanderer's Minuet
        3560, // Iron Jaws
        //3561, // the Warden's Paean
        3562, // Sidewinder
        7404, // Pitch Perfect
        //7405, // Troubadour
        7406, // Caustic Bite
        7407, // Stormbite
        //7408, // Nature's Minne
        7409, // Refulgent Arrow
        16494, // Shadowbite
        16495, // Burst Shot
        16496, // Apex Arrow
        25783, // Ladonsbite
        25784, // Blast Arrow
        //25785, // Radiant Finale
        36974, // Wide Volley
        36975, // Heartbreak Shot
        36976, // Resonant Arrow
        36977, // Radiant Encore

        // ================ MACHINIST ================
        2864, // Rook Autoturret
        2866, // Split Shot
        2868, // Slug Shot
        2870, // Spread Shot
        2872, // Hot Shot
        2873, // Clean Shot
        2874, // Gauss Round
        //2876, // Reassemble
        2878, // Wildfire
        //2887, // Dismantle
        2890, // Ricochet
        7410, // Heat Blast
        7411, // Heated Split Shot
        7412, // Heated Slug Shot
        7413, // Heated Clean Shot
        //7414, // Barrel Stabilizer
        7415, // Rook Overdrive
        7416, // Rook Overload
        7418, // Flamethrower
        16497, // Auto Crossbow
        16498, // Drill
        16499, // Bioblaster
        16500, // Air Anchor
        16501, // Automaton Queen
        16502, // Queen Overdrive
        16503, // Pile Bunker
        16504, // Arm Punch
        16766, // Detonator
        //16889, // Tactician
        17206, // Roller Dash
        //17209, // Hypercharge
        25786, // Scattergun
        25787, // Crowned Collider
        25788, // Chain Saw
        36978, // Blazing Shot
        36979, // Double Check
        36980, // Checkmate
        36981, // Excavator
        36982, // Full Metal Field

        // ================ DANCER ================
        15989, // Cascade
        15990, // Fountain
        15991, // Reverse Cascade
        15992, // Fountainfall
        15993, // Windmill
        15994, // Bladeshower
        15995, // Rising Windmill
        15996, // Bloodshower
        //15997, // Standard Step
        //15998, // Technical Step
        //15999, // Emboite
        //16000, // Entrechat
        //16001, // Jete
        //16002, // Pirouette
        16003, // Standard Finish
        16004, // Technical Finish
        16005, // Saber Dance
        //16006, // Closed Position
        16007, // Fan Dance
        16008, // Fan Dance II
        16009, // Fan Dance III
        //16010, // En Avant
        //16011, // Devilment
        //16012, // Shield Samba
        //16013, // Flourish
        //16014, // Improvisation
        //16015, // Curing Waltz
        16191, // Single Standard Finish
        16192, // Double Standard Finish
        16193, // Single Technical Finish
        16194, // Double Technical Finish
        16195, // Triple Technical Finish
        16196, // Quadruple Technical Finish
        18073, // Ending
        //25789, // Improvised Finish
        25790, // Tillana
        25791, // Fan Dance IV
        25792, // Starfall Dance
        33215, // Single Technical Finish
        33216, // Double Technical Finish
        33217, // Triple Technical Finish
        33218, // Quadruple Technical Finish
        36983, // Last Dance
        36984, // Finishing Move
        36985, // Dance of the Dawn

        // ================ BLACK MAGE ================
        141, // Fire
        142, // Blizzard
        144, // Thunder
        147, // Fire II
        //149, // Transpose
        152, // Fire III
        153, // Thunder III
        154, // Blizzard III
        //155, // Aetherial Manipulation
        156, // Scathe
        //157, // Manaward
        //158, // Manafont
        159, // Freeze
        162, // Flare
        //3573, // Ley Lines
        3576, // Blizzard IV
        3577, // Fire IV
        //7419, // Between the Lines
        7420, // Thunder IV
        //7421, // Triplecast
        7422, // Foul
        7447, // Thunder II
        16505, // Despair
        //16506, // Umbral Soul
        16507, // Xenoglossy
        25793, // Blizzard II
        25794, // High Fire II
        25795, // High Blizzard II
        //25796, // Amplifier
        25797, // Paradox
        36986, // High Thunder
        36987, // High Thunder II
        //36988, // Retrace
        36989, // Flare Star

        // ================ SUMMONER ================
        163, // Ruin
        172, // Ruin II
        181, // Fester
        3578, // Painflare
        3579, // Ruin III
        //3581, // Dreadwyrm Trance
        3582, // Deathflare
        7426, // Ruin IV
        //7427, // Summon Bahamut
        7428, // Wyrmwave
        7429, // Enkindle Bahamut
        7449, // Akh Morn
        //16230, // Physick
        16508, // Energy Drain
        16510, // Energy Siphon
        16511, // Outburst
        16514, // Fountain of Fire
        16515, // Brand of Purgatory
        16516, // Enkindle Phoenix
        //16517, // Everlasting Flight
        16518, // Revelation
        16519, // Scarlet Flame
        //25798, // Summon Carbuncle
        //25799, // Radiant Aegis
        25800, // Aethercharge
        //25801, // Searing Light
        25802, // Summon Ruby
        25803, // Summon Topaz
        25804, // Summon Emerald
        25805, // Summon Ifrit
        25806, // Summon Titan
        25807, // Summon Garuda
        25808, // Ruby Ruin
        25809, // Topaz Ruin
        25810, // Emerald Ruin
        25811, // Ruby Ruin II
        25812, // Topaz Ruin II
        25813, // Emerald Ruin II
        25814, // Ruby Outburst
        25815, // Topaz Outburst
        25816, // Emerald Outburst
        25817, // Ruby Ruin III
        25818, // Topaz Ruin III
        25819, // Emerald Ruin III
        25820, // Astral Impulse
        25821, // Astral Flare
        25822, // Astral Flow
        25823, // Ruby Rite
        25824, // Topaz Rite
        25825, // Emerald Rite
        25826, // Tri-disaster
        25827, // Ruby Disaster
        25828, // Topaz Disaster
        25829, // Emerald Disaster
        25830, // Rekindle
        //25831, // Summon Phoenix
        25832, // Ruby Catastrophe
        25833, // Topaz Catastrophe
        25834, // Emerald Catastrophe
        25835, // Crimson Cyclone
        25836, // Mountain Buster
        25837, // Slipstream
        25838, // Summon Ifrit II
        25839, // Summon Titan II
        25840, // Summon Garuda II
        //25841, // Radiant Aegis
        25843, // Glittering Ruby
        25844, // Glittering Topaz
        25845, // Glittering Emerald
        25846, // Burning Strike
        25847, // Rock Buster
        25848, // Aerial Slash
        25849, // Inferno
        25850, // Earthen Fury
        25851, // Aerial Blast
        25852, // Inferno
        25853, // Earthen Fury
        25854, // Aerial Blast
        25883, // Gemshine
        25884, // Precious Brilliance
        25885, // Crimson Strike
        36990, // Necrotize
        36991, // Searing Flash
        //36992, // Summon Solar Bahamut
        36993, // Luxwave
        36994, // Umbral Impulse
        36995, // Umbral Flare
        36996, // Sunflare
        //36997, // Lux Solaris
        36998, // Enkindle Solar Bahamut
        36999, // Exodus

        // ================ RED MAGE ================
        7503, // Jolt
        7504, // Riposte
        7505, // Verthunder
        7506, // Corps-a-corps
        7507, // Veraero
        7509, // Scatter
        7510, // Verfire
        7511, // Verstone
        7512, // Zwerchhau
        7513, // Moulinet
        //7514, // Vercure
        7515, // Displacement
        7516, // Redoublement
        7517, // Fleche
        //7518, // Acceleration
        7519, // Contre Sixte
        //7520, // Embolden
        //7521, // Manafication
        //7523, // Verraise
        7524, // Jolt II
        7525, // Verflare
        7526, // Verholy
        7527, // Enchanted Riposte
        7528, // Enchanted Zwerchhau
        7529, // Enchanted Redoublement
        7530, // Enchanted Moulinet
        16524, // Verthunder II
        16525, // Veraero II
        16526, // Impact
        16527, // Engagement
        16528, // Enchanted Reprise
        16529, // Reprise
        16530, // Scorch
        25855, // Verthunder III
        25856, // Veraero III
        //25857, // Magick Barrier
        25858, // Resolution
        37002, // Enchanted Moulinet Deux
        37003, // Enchanted Moulinet Trois
        37004, // Jolt III
        37005, // Vice of Thorns
        37006, // Grand Impact
        37007, // Prefulgence

        // ================ PICTOMANCER ================
        34650, // Fire in Red
        34651, // Aero in Green
        34652, // Water in Blue
        34653, // Blizzard in Cyan
        34654, // Stone in Yellow
        34655, // Thunder in Magenta
        34656, // Fire II in Red
        34657, // Aero II in Green
        34658, // Water II in Blue
        34659, // Blizzard II in Cyan
        34660, // Stone II in Yellow
        34661, // Thunder II in Magenta
        34662, // Holy in White
        34663, // Comet in Black
        //34664, // Pom Motif
        //34665, // Wing Motif
        //34666, // Claw Motif
        //34667, // Maw Motif
        //34668, // Hammer Motif
        //34669, // Starry Sky Motif
        34670, // Pom Muse
        34671, // Winged Muse
        34672, // Clawed Muse
        34673, // Fanged Muse
        //34674, // Striking Muse
        //34675, // Starry Muse
        34676, // Mog of the Ages
        34677, // Retribution of the Madeen
        34678, // Hammer Stamp
        34679, // Hammer Brush
        34680, // Polishing Hammer
        34681, // Star Prism
        34682, // Star Prism
        //34683, // Subtractive Palette
        //34684, // Smudge
        //34685, // Tempera Coat
        //34686, // Tempera Grassa
        34688, // Rainbow Drip
        34689, // Creature Motif
        //34690, // Weapon Motif
        //34691, // Landscape Motif
        35347, // Living Muse
        //35348, // Steel Muse
        //35349, // Scenic Muse

        // ================ BLUE MAGE ================
    ];
}
