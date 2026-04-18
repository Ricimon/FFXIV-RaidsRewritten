namespace RaidsRewritten.Scripts.Encounters.UWU;

/// <summary>
/// Action IDs and boss data for The Weapon's Refrain (Ultimate).
/// Territory ID: 777
/// Source: cactbot timeline (quisquous/cactbot)
/// </summary>
public static class UwuData
{
    // Phases: Garuda -> Ifrit -> Titan -> Intermission (Lahabrea) -> Ultima Weapon
    // Ultima sub-phases: Predation -> Annihilation -> Suppression -> Primal Roulette -> Enrage

    public static class Garuda
    {
        public const uint Slipstream = 0x2B53;
        public const uint MistralSong = 0x2B42;
        public const uint MistralSongSister = 0x2B4B; // Chirada/Suparna
        public const uint MistralShriek = 0x2B54;
        public const uint GreatWhirlwind = 0x2B41;
        public const uint Downburst = 0x2B50;
        public const uint FeatherRain = 0x2B4D;
        public const uint Friction = 0x2B48;
        public const uint AerialBlast = 0x2B55;
        public const uint EyeOfTheStorm = 0x2B52;
        public const uint WickedWheel = 0x2B4E;
        public const uint WickedTornado = 0x2B4F;
        public const uint Mesohigh = 0x2B49;
    }

    public static class Ifrit
    {
        public const uint CrimsonCyclone = 0x2B5F;
        public const uint RadiantPlume = 0x2B61;
        public const uint Hellfire = 0x2B5E;
        public const uint VulcanBurst = 0x2B57;
        public const uint Incinerate = 0x2B56;
        public const uint InfernalFetters = 0x2C19;
        public const uint InfernoHowl = 0x2B5B; // Searing Wind
        public const uint Eruption = 0x2B5A;
        public const uint FlamingCrush = 0x2B5D;
    }

    public static class Titan
    {
        public const uint GeocrushEntry = 0x2CFD; // Phase transition Geocrush
        public const uint Geocrush = 0x2B66;      // In-phase Geocrush
        public const uint EarthenFury = 0x2B90;
        public const uint RockBuster = 0x2B62;
        public const uint MountainBuster = 0x2B63;
        public const uint WeightOfTheLand = 0x2B65;
        public const uint Upheaval = 0x2B67;
        public const uint Bury = 0x2B69;          // Bomb Boulder
        public const uint RockThrow = 0x2B6B;     // Gaols
        public const uint Landslide1 = 0x2B70;
        public const uint Landslide2 = 0x2C22;
        public const uint Tumult = 0x2C18;
    }

    public static class Intermission
    {
        public const uint Freefire = 0x2CF5;
        public const uint SelfDetonate = 0x2B72; // Magitek Bit
        public const uint Blight = 0x2B73;       // Lahabrea
        public const uint DarkIV = 0x2B74;        // Lahabrea
    }

    public static class Ultima
    {
        public const uint UltimaAbility = 0x2B8B;
        public const uint TankPurge = 0x2B87;
        public const uint ApplyViscous = 0x2B79;
        public const uint HomingLasers = 0x2B7B;
        public const uint ViscousAetheroplasm = 0x2B7A;
        public const uint ViscousAetheroplasmPost = 0x2B8F; // Post-suppression
        public const uint Aetheroplasm = 0x2B81;            // Orbs
        public const uint DiffractiveLaser = 0x2B78;
        public const uint CeruleumVent = 0x2B7C;
        public const uint VulcanBurst = 0x2CF4;             // Ultima's version
        public const uint RadiantPlume = 0x2B7D;             // Ultima's version
        public const uint Landslide = 0x2B7E;               // Ultima's version
        public const uint LightPillar = 0x2B82;
        public const uint AetherochemicalLaserMiddle = 0x2B84;
        public const uint AetherochemicalLaserRight = 0x2B85;
        public const uint AetherochemicalLaserLeft = 0x2B86;
        public const uint AethericBoom = 0x2B88;
        public const uint UltimatePredation = 0x2B76;
        public const uint UltimateAnnihilation = 0x2D4C;
        public const uint UltimateSuppression = 0x2D4D;
        public const uint SummonGaruda = 0x2CD3;
        public const uint SummonIfrit = 0x2CD4;
        public const uint SummonTitan = 0x2CD5;
        public const uint CitadelSiege = 0x2B92;
        public const uint SabikEnrage = 0x2B93;
        public const uint Enrage = 0x2B8C;
    }

    // Garuda's Mistral Song used during Suppression
    public static class SuppressionGaruda
    {
        public const uint MistralSong = 0x2B8E;
    }

    // Boss NPC Name IDs (for identifying combatants)
    public static class NpcNameIds
    {
        public const uint Garuda = 0x644;   // 1604
        public const uint Ifrit = 0x4A1;    // 1185
        public const uint Titan = 0x4A3;    // 1187
        public const uint Suparna = 0x671;  // 1649 - was 1645 in cactbot (may differ)
        public const uint Chirada = 0x672;  // 1650 - was 1646 in cactbot (may differ)
        public const uint Lahabrea = 0x862; // 2146
        public const uint MagitekBit = 0x859; // 2137
        public const uint BombBoulder = 0x70B; // 1803
        public const uint UltimaWeapon = 0x864; // 2148
    }
}
