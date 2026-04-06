//!CompilerOption:Optimize:On
using System.Linq;

namespace Kombatant.Constants
{
    /// <summary>
    /// Static list of noteworthy GameObjects.
    /// </summary>
    internal static class GameObject
    {
        /// <summary>
        /// Constructor, will prepare the concatinated lists.
        /// </summary>
        static GameObject()
        {
            DungeonBosses = new uint[] { }
                .Concat(ARealmRebornDungeonBosses)
                .Concat(HeavenswardDungeonBosses)
                .Concat(StormbloodDungeonBosses)
                .Concat(ShadowBringerDungeonBosses)
                .Concat(EndwalkerDungeonBosses)
                .Concat(DawntrailDungeonBosses)
                .ToArray();
        }

        /// <summary>
        /// The NpcId of the Striking Dummy.
        /// </summary>
        internal static readonly uint StrikingDummy = 541;

        /// <summary>
        /// The NpcIds of all A Realm Reborn dungeon bosses.
        /// </summary>
        internal static readonly uint[] ARealmRebornDungeonBosses =
        {
            // Sastasha
            1204,    // Chopper
            1382,    // Captain Madison
            1206,    // Denn the Orcatoothed

            // The Tam-Tara Deepcroft
            455,    // Void Soulcounter
            73,    // Galvanth the Dominator

            // Copperbell Mines
            548,    // Kottos
            554,    // Ichorous Ire
            101,    // Gyges The Great

            // Halatali
            1194,    // Firemane
            1196,    // Thunderclap Guivre
            1192,    // Pit Peiste
            1197,    // Tangata

            // The Thousand Maws of Toto-Rak
            442,    // Coeurl O' Nine Tails
            444,    // Graffias

            // Haukke Manor
            423,    // Manor Claviger
            426,    // Manor Jester
            427,    // Manor Steward
            422,    // Lady Amandine

            // Brayflox's Longstop
            1280,    // Great Yellow Pelican
            1284,    // Inferno Drake
            1286,    // Hellbender
            1297,    // Deep Jungle Coeurl
            1279,    // Aiatar

            // The Sunken Temple of Qarn
            1567,    // Teratotaur
            1569,    // Temple Guardian
            1570,    // Adjudicator

            // Cutter's Cry
            1585,    // Myrmidon Princess
            1589,    // Giant Tunnel Worm
            1590,    // Chimera

            // The Stone Vigil
            1677,    // Chudo-Yudo
            1678,    // Koshchei
            1680,    // Isgebind

            // Dzemael Darkhold
            1397,    // All-seeing Eye
            1415,    // Taulurd
            1396,    // Batraal

            // The Aurum Vale
            1534,    // Locksmith
            1533,    // Coincounter
            1532,    // Miser's Mistress

            // The Wanderer's Palace
            1548,    // Keeper of Halidom
            1549,    // Giant Bavarois
            1547,    // Tonberry King

            // Amdapor Keep
            1689,    // Psycheflayer
            1694,    // Demon Wall
            1696,    // Anantaboga

            // Pharos Sirius
            2259,    // Symond the Unsinkable
            2261,    // Zu
            2264,    // Tyrant
            2265,    // Siren

            // Copperbell Mines (Hard)
            2282,    // Hecatoncheir Mastermind
            2285,    // Gogmagolem
            2289,    // Ouranos

            // Haukke Manor (Hard)
            2341,    // Ash
            2346,    // Halicarnassus

            // The Lost City of Amdapor
            2556,    // Decaying Gourmand
            2560,    // Arioch
            2564,    // Diabolos

            // Halatali (Hard)
            2597,    // Pyracmon
            2598,    // Catoblepas
            2602,    // Mumuepo the Beholden

            // Brayflox's Longstop (Hard)
            2548,    // Illuminati Commander
            2549,    // Magitek Vangob G-III
            2547,    // Gobmachine G-VI

            // Hullbreaker Isle
            2901,    // Sasquatch
            2903,    // Sjoorm
            2904,    // Kraken
            2905,    // Kranken's Arm

            // The Stone Vigil (Hard)
            2778,    // Gorynich
            2775,    // Cuca Fera
            2774,    // Giruveganaus

            // Tam-Tara Deepcroft (Hard)
            2852,    // Liavinne
            2855,    // Spare Body
            2860,    // Avere Bravearm

            // Snowcloak
            3038,    // Wandil
            3040,    // Yeti
            3044,    // Fenrir

            // Sastasha (Hard)
            3014,    // Karlabos
            3015,    // Captain Madison

            // The Sunken Temple of Qarn (Hard)
            3065,    // Damaged Adjudicator
            3071,    // Sabotender Emperatriz
            3075,    // Vicegerent To The Warden

            // The Keeper of the Lake
            3369,    // Einhander
            3373,    // Magitek Gunship
            3374,    // Midgardsormr

            // The Wanderer's Palace (Hard)
            3091,    // Frumious Koheel Ja
            3093,    // Slithy Zolool Ja
            3095,    // Manxome Molaa Ja Ja

            // Amdapor Keep (Hard)
            3272,    // Anchag
            3274,    // Boogyman
            3280,    // Ferdiad
        };

        /// <summary>
        /// The NpcIds of all Heavensward dungeon bosses.
        /// </summary>
        internal static readonly uint[] HeavenswardDungeonBosses =
        {
            // The Dusk Vigil
            3405,    // Towering Oliphant
            3406,    // Ser Yuhelmeric
            3409,    // Opinicus

            // Sohm Al
            3791,    // Raskovnik
            3793,    // Myath
            3798,    // Tioman

            // The Aery
            3452,    // Rangda
            3455,    // Gyascutus
            3458,    // Nidhogg

            // The Vault
            3849,    // Ser Adelphel Brightblade
            3634,    // Ser Adelphel
            3850,    // Ser Grinnaux The Bull
            3639,    // Ser Grinnaux
            3642,    // Ser Charibert

            // The Great Gubal Library
            3923,    // Demon Tome
            3925,    // Byblos
            3930,    // The Everliving Bibliotaph

            // The Aetherochemical Research Facility
            3818,    // Regula Van Hydrus
            3821,    // Harmachis
            3822,    // Igeyorhm
            3823,    // Ascian Prime

            // Neverreap
            3726,    // Nunyenunc
            3727,    // Nunyenunc's Shadow
            3734,    // Canu Vanu
            3740,    // Waukkeon

            // The Fractal Continuum
            3428,    // Phantom Ray
            3429,    // Minotaur
            3434,    // The Curator

            // Saint Mocianne's Arboretum
            4653,    // Rose Garden
            4656,    // Queen Hawk
            4658,    // Belladonna

            // Pharos Sirius (Hard)
            4567,    // Ghrah Luminary
            4571,    // 8th Order Patriarch Be Gu
            4575,    // Progenitrix
            4576,    // Progenitor

            // The Antitower
            4805,    // Zuro Roggo
            4808,    // Ziggy
            4811,    // Calca
            4812,    // Brina
            4813,    // Calcabrina

            // The Lost City of Amdapor (Hard)
            4744,    // Achamoth
            4745,    // Winged Lion
            4747,    // Kuribu

            // Sohr Khai
            4943,    // Chieftain Moglin
            4952,    // Poqhiraj
            4954,    // Hraesvelgr

            // Hullbreaker Isle (Hard)
            4909,    // Sanguine Sirens Gunner
            4908,    // Sanguine Sirens Lodeswoman
            4907,    // Sanguine Sirens Targirl
            4911,    // Ymir
            4913,    // Grand Storm Marshal Slafyrsyn

            // Xelphatol
            5265,    // Nuzal Hueloc
            5269,    // Dotoli Ciloc
            5272,    // Tozol Huatotl

            // The Great Gubal Library (Hard)
            5216,    // Demon Of The Tome
            5218,    // Liquid Flame
            5219,    // Strix

            // Baelsar's Wall
            5560,    // Magitek Predator
            5562,    // Armored Weapon
            5564,    // The Griffin

            // Sohm Al (Hard)
            5529,    // The Leightonward
            5530,    // Gowrow
            5531,    // Lava Scorpion
        };

        /// <summary>
        /// The NpcIds of all Stormblood dungeon bosses.
        /// </summary>
        internal static readonly uint[] StormbloodDungeonBosses =
        {
            // The Sirensong Sea
            6071,    // Lugat
            6072,    // The Governor
            6074,    // Lorelei

            // Shisui of the Violet Tides
            6237,    // Amikiri
            6238,    // Kamikiri
            6239,    // Amikiri Leg
            6241,    // Ruby Princess
            6243,    // Shisui Yohi

            // Bardam's Mettle
            6173,    // Garula
            6177,    // Bardam
            6155,    // Yol

            // Doma Castle
            6200,    // Magitek Rearguard
            6203,    // Magitek Hexadrone
            6205,    // Hypertuned Grynewaht

            // Castrum Abania
            6263,    // Magna Roader
            6267,    // Number XXIV
            6268,    // Inferno

            // Ala Mhigo
            6037,    // Magitek Scorpion
            6038,    // Aulus Mal Asina
            6039,    // Zenos Yae Galvus

            // Kugane Castle
            6085,    // Zuiko-maru
            6087,    // Dojun-maru
            6089,    // Yojimbo

            // The Temple of the Fist
            6120,    // Coeurl Smriti
            6118,    // Arbuda
            6117,    // Ivon Coeurlfist

            // The Drowned City of Skalla
            6907,    // Kelpie
            6908,    // The Old One
            6910,    // Hrodric Poisontongue

            // Hell's Lid
            6994,    // Otake-maru
            6995,    // Kamaitachi
            6996,    // Genbu

            // The Fractal Continuum (Hard)
            7056,    // Motherbit
            7055,    // The Ultima Warrior
            7058,    // The Ultima Beast

            // The Swallow's Compass
            7200,    // Otengu
            7202,    // Daidarabotchi
            7203,    // Qitian Dasheng
            7204,    // Shadow of the Sage
            7205,    // Servant of the Sage

            // The Burn
            7667,    // Hedetet
            7669,    // Defective Drone
            7672,    // Mist Dragon

            // The Ghimlyt Dark
            7659,    // Cerberus
            7660,    // Prometheus
            7662,    // Mark III-B Magitek Colossus

            // The Ghimlyt Dark (continued)
            7855,    // Mark III-B Magitek Colossus (variant)
            7856,    // Prometheus (variant)
            7857,    // Julia Quo Soranus
            7858,    // Annia Quo Soranus
            7859,    // Julia Quo Soranus (phase 2)
            7860,    // Annia Quo Soranus (phase 2)

            // The Royal Menagerie (Trial)
            6333,    // Shinryu

            // Sigmascape V4.0 (Raid O8)
            7633,    // Omega-M
            7634,    // Omega-F

            // Alphascape V3.0 (Raid O11)
            7635,    // Omega
            7636,    // Omega (phase 2)

            // Alphascape V4.0 (Raid O12)
            7695,    // Omega

            // The Orbonne Monastery (Alliance Raid)
            8014,    // Ultima, the High Seraph
        };

        /// <summary>
        /// The NpcIds of all Shadowbringers dungeon bosses.
        /// </summary>
        internal static readonly uint[] ShadowBringerDungeonBosses =
        {
            // The Twinning
            8299,    // Forgiven Dissonance
            8300,    // Forgiven Tesleen
            8301,    // Philia

            // Mt. Gulg
            8141,    // Forgiven Innocence
            8143,    // Forgiven Cruelty
            8146,    // Forgiven Obscenity

            // Akadaemia Anyder
            8231,    // Quetzalcoatl
            8232,    // Lacovie
            8233,    // Aether

            // Holminster Switch
            8252,    // Forgiven Whimsy
            8250,    // Tesleen, the Forgiven
            8249,    // Storge

            // The Grand Cosmos
            8260,    // Seeker of Solitude
            8261,    // Leannan Sidhe
            8262,    // Lugus

            // Amaurot
            8201,    // The First Beast
            8202,    // The Second Beast
            8210,    // Therion

            // Anamnesis Anyder
            8235,    // OOO
            8236,    // AAA
            8272,    // Seiryu (Anamnesis)
            8273,    // Quetzalcoatl (Anamnesis)

            // The Heroes' Gauntlet
            8162,    // Spectral Zenos
            8165,    // Spectral Gaius
            8167,    // Spectral Thordan

            // Matoya's Relict
            9041,    // Mudman
            9046,    // Lugsus
            9044,    // Mothercrystal (Relict)

            // Paglth'an
            9260,    // Lunar Bahamut
            9261,    // Teratotaur
            9263,    // Rynphaes
            9264,    // Lunar Ifrit

            // The Dying Gasp (Trial)
            9020,    // Hades

            // Eden's Promise: Eternity (Raid E12)
            9481,    // Eden's Promise

            // The Tower at Paradigm's Breach (Alliance Raid, patch 5.5)
            10160,   // Her Inflorescence
        };

        /// <summary>
        /// The NpcIds of all Endwalker dungeon bosses.
        /// </summary>
        internal static readonly uint[] EndwalkerDungeonBosses =
        {
            // The Tower of Zot
            10256,   // Minduruva
            10257,   // Sanduruva
            10259,   // Cinduruva
            10265,   // The Magus Sisters

            // The Tower of Babil
            10279,   // Barnabas
            10281,   // Lugae
            10282,   // Lugae (phase 2)
            10285,   // Anima
            10288,   // Anima (phase 2)

            // The Tower of Babil (Criterion)
            12045,   // Suprae-Lugae

            // Vanaspati
            10717,   // Terminus Snatcher
            10718,   // Terminus Wrecker
            10719,   // Svarbhanu

            // Ktisis Hyperboreia
            10396,   // Lyssa
            10398,   // Ladon Lord
            10399,   // Hermes
            10363,   // Hermes (MSQ/trial form)

            // The Aitiascope
            10290,   // Livia the Undeterred
            10292,   // Rhitahtyn the Unshakable
            10293,   // Amon the Undying

            // The Dead Ends
            10313,   // Caustic Grebuloff
            10315,   // Peacekeeper
            10316,   // Ra-la

            // Smileton
            10331,   // Face
            10333,   // Frameworker
            10336,   // The Big Cheese

            // The Stigma Dreamscape
            10401,   // Proto-Omega
            10403,   // Arch-Lambda
            10404,   // Stigma-4

            // Alzadaal's Legacy
            11238,   // Kapikulu
            11239,   // Armored Chariot
            11241,   // Ambujam

            // The Fell Court of Troia
            11372,   // Scarmiglione
            11382,   // Evil Dreamer
            11384,   // Beatrice
            11407,   // Scarmiglione (powered-up)

            // Lapis Manalis
            10308,   // Galatea Magna
            11992,   // Albion
            11995,   // Cagnazzo

            // The Aetherfont
            12334,   // Octomammoth
            12336,   // Lyngbakr
            12337,   // Arkas

            // The Lunar Subterrane
            12484,   // Damcyan Antlion
            12500,   // Dark Elf
            12584,   // Durante
            12590,   // Durante (phase 2)

            // The Final Day (Trial)
            10434,   // The Endsinger

            // Pandaemonium: Anabaseios (Raid P12)
            12369,   // Athena

            // Thaleia (Alliance Raid)
            12604,   // Eulogia

            // Mount Rokkon (Variant/Criterion)
            12428,   // Shishio

            // Aloalo Island (Variant/Criterion)
            12605,   // Ketuduke
            12636,   // Loquloqui
            12675,   // Gulool Ja Ja
        };

        /// <summary>
        /// The NpcIds of all Dawntrail dungeon bosses.
        /// </summary>
        internal static readonly uint[] DawntrailDungeonBosses =
        {
            // Ihuykatumu
            12723,   // Prime Punutiy
            12716,   // Drowsie
            12711,   // Apollyon

            // Worqor Zormor
            12829,   // Iq Soq Xmajun
            12831,   // Keshkwa
            12854,   // Valigarmanda

            // The Skydeep Cenote
            12755,   // Feather Ray
            12888,   // Firearms
            12728,   // Maulskull

            // Vanguard
            12750,   // Vanguard Commander R8
            12757,   // Protector
            12752,   // Zander the Snakeskinner
            12743,   // Zoraal Ja
            12881,   // Zoraal Ja (trial phase 2)
            12882,   // Zoraal Ja (trial phase 3)

            // Origenics
            12741,   // Herpekaris
            12693,   // Deceiver
            12695,   // Ambrose the Undeparted

            // Alexandria
            12844,   // Antivirus X
            12864,   // Amalgam
            12729,   // The Eliminator
            12751,   // The Eliminator (alternate ID — from initial cross-reference)

            // Tender Valley
            12889,   // Barreltender
            12853,   // Anthracite
            12709,   // Greatest Serpent of Tural

            // The Strayborough Deadwalk
            13073,   // His Royal Headness Leonogg I
            12760,   // Jack-in-the-Pot
            12763,   // Träumerei

            // Yuweyawata Field Station
            13057,   // Wicked Thunder
            13058,   // Wicked Thunder (phase 2)
            13623,   // Lindblum Zaghnal
            13634,   // Overseer Kanilokka

            // The Underkeep
            13153,   // Gargant
            13757,   // Soldier S0
            13749,   // Valia Pira

            // The Meso Terminal
            13970,   // Chirurgeon General
            14239,   // Hooded Headsman
            13974,   // Immortal Remains

            // Mistwake
            14270,   // Treno Catoblepas
            14271,   // Amdusias
            14288,   // Thundergust Griffin

            // The Interphos (Trial)
            13264,   // Eternal Queen

            // AAC Light-heavyweight (Raid)
            13314,   // Black Cat
            13315,   // Honey B. Lovely
        };

        /// <summary>
        /// The NpcIds of all dungeon bosses in Final Fantasy XIV.
        /// </summary>
        internal static readonly uint[] DungeonBosses;
    }
}