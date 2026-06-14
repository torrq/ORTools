namespace ORTools.Worker;

// Worker-only Buff: data-only, no Bitmap/IResourceLoader needed.
public class Buff
{
    public string         Name          { get; set; } = "";
    public EffectStatusIDs EffectStatusID { get; set; }

    public Buff() { }
    public Buff(string name, EffectStatusIDs effectStatus)
    {
        Name          = name;
        EffectStatusID = effectStatus;
    }
}

// ── Buff definitions ──────────────────────────────────────────────────────────
// All collections are keyed 0 = MR, 1 = HR where server-specific.
// BuffService selects the right index via AppConfig.ServerMode.

public static class BuffDefinitions
{

    // ARCHER buffs
    public static readonly Dictionary<int, List<Buff>> ArcherBuffs = new Dictionary<int, List<Buff>>();
    // SWORDMAN buffs
    public static readonly Dictionary<int, List<Buff>> SwordmanBuffs = new Dictionary<int, List<Buff>>();
    // MAGE buffs
    public static readonly Dictionary<int, List<Buff>> MageBuffs = new Dictionary<int, List<Buff>>();
    // MERCHANT buffs
    public static readonly Dictionary<int, List<Buff>> MerchantBuffs = new Dictionary<int, List<Buff>>();
    // THIEF buffs
    public static readonly Dictionary<int, List<Buff>> ThiefBuffs = new Dictionary<int, List<Buff>>();
    // ACOLYTE buffs
    public static readonly Dictionary<int, List<Buff>> AcolyteBuffs = new Dictionary<int, List<Buff>>();
    // NINJA buffs
    public static readonly Dictionary<int, List<Buff>> NinjaBuffs = new Dictionary<int, List<Buff>>();
    // TAEKWON buffs
    public static readonly Dictionary<int, List<Buff>> TaekwonBuffs = new Dictionary<int, List<Buff>>();
    // GUNSLINGER buffs
    public static readonly Dictionary<int, List<Buff>> GunslingerBuffs = new Dictionary<int, List<Buff>>();
    // PADAWAN buffs
    public static readonly Dictionary<int, List<Buff>> PadawanBuffs = new Dictionary<int, List<Buff>>();

    // ITEM buffs
    public static readonly List<Buff> PotionBuffs = new List<Buff>();
    public static readonly Dictionary<int, List<Buff>> ElementBuffs = new Dictionary<int, List<Buff>>();
    public static readonly List<Buff> FoodBuffs = new List<Buff>();
    public static readonly List<Buff> BoxBuffs = new List<Buff>();
    public static readonly List<Buff> ScrollBuffs = new List<Buff>();
    public static readonly List<Buff> EtcBuffs = new List<Buff>();
    public static readonly List<Buff> FishBuffs = new List<Buff>();

    // DEBUFFS
    public static readonly List<Buff> Debuffs = new List<Buff>();


    // B() helper — parses effectStatusName and creates a data-only Buff
    private static Buff B(string name, string effectStatusName)
    {
        if (!Enum.TryParse<EffectStatusIDs>(effectStatusName, out var effect))
            DebugLogger.Debug($"[Buff] Unknown EffectStatusID: {effectStatusName} for buff: {name}");
        return new Buff(name, effect);
    }

    public static void Initialize()
    {
        InitializeSkillBuffs();
        InitializeItemBuffs();
        InitializeDebuffs();
    }

    private static void InitializeSkillBuffs()
    {

        ArcherBuffs[0] = new List<Buff>
        {
            B("Improve Concentration", "AC_CONCENTRATION"),
            B("True Sight", "SN_SIGHT"),
            B("Wind Walk", "SN_WINDWALK")
        };

        SwordmanBuffs[0] = new List<Buff>
        {
            B("Endure", "SM_ENDURE"),
            B("Auto Berserk", "SM_AUTOBERSERK"),
            B("Auto Guard", "CR_AUTOGUARD"),
            B("Reflect Shield", "CR_REFLECTSHIELD"),
            B("Spear Quicken", "CR_SPEARQUICKEN"),
            B("Defender", "CR_DEFENDER"),
            B("Concentration", "LK_CONCENTRATION"),
            B("Berserk", "LK_BERSERK"),
            B("Two-Hand Quicken", "KN_TWOHANDQUICKEN"),
            B("Parry", "LK_PARRYING"),
            B("Aura Blade", "LK_AURABLADE"),
            B("Shrink", "CR_SHRINK"),
            B("Magnum Break", "SM_MAGNUM"),
            B("One-Hand Quicken", "KN_ONEHAND"),
            B("Provoke", "SM_PROVOKE"),
            B("Providence", "CR_PROVIDENCE")
        };
        SwordmanBuffs[1] = new List<Buff>
        {
            B("Endure", "SM_ENDURE"),
            B("Auto Berserk", "SM_AUTOBERSERK"),
            B("Auto Guard", "CR_AUTOGUARD"),
            B("Reflect Shield", "CR_REFLECTSHIELD"),
            B("Spear Quicken", "CR_SPEARQUICKEN"),
            B("Defender", "CR_DEFENDER"),
            B("Concentration", "LK_CONCENTRATION"),
            B("Berserk", "LK_BERSERK"),
            B("Two-Hand Quicken", "KN_TWOHANDQUICKEN"),
            B("Parry", "LK_PARRYING"),
            B("Aura Blade", "LK_AURABLADE"),
            B("Shrink", "CR_SHRINK"),
            B("Magnum Break", "SM_MAGNUM"),
            B("One-Hand Quicken", "KN_ONEHAND"),
            B("Provoke", "SM_PROVOKE"),
            B("Providence", "CR_PROVIDENCE"),
            B("Mana Shield", "MANA_SHIELD")
        };

        MageBuffs[0] = new List<Buff>
        {
            B("Energy Coat", "MG_ENERGYCOAT"),
            B("Sight Blaster", "WZ_SIGHTBLASTER"),
            B("Autospell", "SA_AUTOSPELL"),
            B("Double Casting", "PF_DOUBLECASTING"),
            B("Memorize", "PF_MEMORIZE"),
            B("Amplify Magic Power / Mystical Amplification", "HW_MAGICPOWER"),
            B("Mind Breaker", "PF_MINDBREAKER")
        };

        MerchantBuffs[0] = new List<Buff>
        {
            B("Crazy Uproar", "MC_LOUD"),
            B("Overthrust", "BS_OVERTHRUST"),
            B("Adrenaline Rush", "BS_ADRENALINE"),
            B("Full Adrenaline Rush", "BS_ADRENALINE2"),
            B("Weapon Perfection", "BS_WEAPONPERFECT"),
            B("Maximize Power", "BS_MAXIMIZE"),
            B("Cart Boost", "WS_CARTBOOST"),
            B("Meltdown", "WS_MELTDOWN"),
            B("Maximum Overthrust", "WS_OVERTHRUSTMAX"),
            B("Greed Parry", "WS_GREEDPARRY")
        };
        MerchantBuffs[1] = new List<Buff>
        {
            B("Crazy Uproar", "MC_LOUD"),
            B("Overthrust", "BS_OVERTHRUST"),
            B("Adrenaline Rush", "BS_ADRENALINE"),
            B("Full Adrenaline Rush", "BS_ADRENALINE2"),
            B("Weapon Perfection", "BS_WEAPONPERFECT"),
            B("Maximize Power", "BS_MAXIMIZE"),
            B("Cart Boost", "WS_CARTBOOST"),
            B("Meltdown", "WS_MELTDOWN"),
            B("Maximum Overthrust", "WS_OVERTHRUSTMAX"),
            B("Greed Parry", "RESIST_PROPERTY_FIRE")
        };

        ThiefBuffs[0] = new List<Buff>
        {
            B("Poison React", "AS_POISONREACT"),
            B("Reject Sword", "ST_REJECTSWORD"),
            B("Preserve", "ST_PRESERVE"),
            B("Enchant Deadly Poison", "ASC_EDP"),
            B("Hide", "TF_HIDING"),
            B("Cloak", "AS_CLOAKING"),
            B("Chase Walk", "ST_CHASEWALK")
        };
        ThiefBuffs[1] = new List<Buff>
        {
            B("Poison React", "AS_POISONREACT"),
            B("Reject Sword", "ST_REJECTSWORD"),
            B("Preserve", "ST_PRESERVE"),
            B("Enchant Deadly Poison", "ASC_EDP"),
            B("Hide", "TF_HIDING"),
            B("Cloak", "AS_CLOAKING"),
            B("Chase Walk", "ST_CHASEWALK"),
            B("Enchant Poison Armor", "ENCHANT_POISON_ARMOR")
        };

        AcolyteBuffs[0] = new List<Buff>
        {
            B("Blessing", "AL_BLESSING"),
            B("Increase Agility", "AL_INCAGI"),
            B("Gloria", "PR_GLORIA"),
            B("Magnificat", "PR_MAGNIFICAT"),
            B("Angelus", "AL_ANGELUS"),
            B("Impositio Manus", "PR_IMPOSITIO"),
            B("Basilica", "HP_BASILICA"),
            B("Fury", "MO_EXPLOSIONSPIRITS"),
            B("Steel Body", "MO_STEELBODY"),
        };
        AcolyteBuffs[1] = new List<Buff>
        {
            B("Blessing", "AL_BLESSING"),
            B("Increase Agility", "AL_INCAGI"),
            B("Gloria", "PR_GLORIA"),
            B("Magnificat", "PR_MAGNIFICAT"),
            B("Angelus", "AL_ANGELUS"),
            B("Impositio Manus", "PR_IMPOSITIO"),
            B("Basilica", "HP_BASILICA"),
            B("Fury", "MO_EXPLOSIONSPIRITS"),
            B("Steel Body", "MO_STEELBODY"),
            B("Refraction", "REFRACTION"),
            B("Shallow Grave", "SL_KAIZEL")
        };

        NinjaBuffs[0] = new List<Buff>
        {
            B("Ninja Aura", "NJ_NEN"),
            B("Cast-off Cicada / Cicada SS", "NJ_UTSUSEMI"),
            B("Illusion Shadow / Mirror Image", "NJ_BUNSINJYUTSU")
        };

        TaekwonBuffs[0] = new List<Buff>
        {
            B("Mild Wind (Earth)", "SA_SEISMICWEAPON"),
            B("Mild Wind (Fire)", "SA_FLAMELAUNCHER"),
            B("Mild Wind (Water)", "SA_FROSTWEAPON"),
            B("Mild Wind (Wind)", "SA_LIGHTNINGLOADER"),
            B("Mild Wind (Ghost)", "PROPERTYTELEKINESIS"),
            B("Mild Wind (Holy)", "PR_ASPERSIO"),
            B("Mild Wind (Shadow)", "PROPERTYDARK"),
            B("Tumbling", "MO_DODGE"),
            B("Solar, Lunar, and Stellar Warmth", "WARM"),
            B("Comfort of the Sun", "SG_SUN_COMFORT"),
            B("Comfort of the Moon", "SG_MOON_COMFORT"),
            B("Comfort of the Stars", "SG_STAR_COMFORT"),
            B("Kaupe", "SL_KAUPE"),
            B("Kaite", "SL_KAITE"),
            B("Kaizel", "SL_KAIZEL"),
            B("Kaahi", "SL_KAAHI")
        };

        GunslingerBuffs[0] = new List<Buff>
        {
            B("Gatling Fever", "GS_GATLINGFEVER"),
            B("Last Stand", "GS_MADNESSCANCEL"),
            B("Adjustment", "GS_ADJUSTMENT"),
            B("Increased Accuracy", "GS_INCREASING")
        };

        PadawanBuffs[1] = new List<Buff>
        {
            B("Force Element (Earth)", "SA_SEISMICWEAPON"),
            B("Force Element (Wind)", "SA_LIGHTNINGLOADER"),
            B("Force Element (Water)", "SA_FROSTWEAPON"),
            B("Force Element (Fire)", "SA_FLAMELAUNCHER"),
            B("Force Element (Ghost)", "PROPERTYTELEKINESIS"),
            B("Force Element (Shadow)", "PROPERTYDARK"),
            B("Force Element (Holy)", "PR_ASPERSIO"),
            B("Force Projection", "HR_PROJECTION"),
            B("Cold Skin", "RESIST_PROPERTY_WATER"),
            B("Saber Parry", "HR_SABERPARRY"),
            B("Force Concentration", "HR_FORCECONCENTRATE"),
            B("Saber Thrust", "LK_CONCENTRATION"),
            B("Force Persuasion", "HR_FORCEPERSUASION"),
            B("Force Haste", "HR_FORCEHASTE"),
            B("Force Sacrifice", "HR_FORCESACRIFICE"),
            B("Jedi Frenzy", "HR_JEDIFRENZY")
        };
        PadawanBuffs[0] = new List<Buff>
        {
            B("Force Element (Earth)", "PD_ELEMENT_EARTH"),
            B("Force Element (Wind)", "PD_ELEMENT_WIND"),
            B("Force Element (Water)", "PD_ELEMENT_WATER"),
            B("Force Element (Fire)", "PD_ELEMENT_FIRE"),
            B("Force Element (Ghost)", "PD_ELEMENT_GHOST"),
            B("Force Element (Shadow)", "PD_ELEMENT_SHADOW"),
            B("Force Element (Holy)", "PD_ELEMENT_HOLY"),
            B("Force Projection", "SI_PROJECTION"),
            B("Cold Skin", "SI_COLDSKIN"),
            B("Saber Parry", "JS_SABERPARRY"),
            B("Force Concentration", "JS_CONCENTRATE"),
            B("Saber Thrust", "SI_SABERTHRUST"),
            B("Force Persuasion", "JS_PERSUADE"),
            B("Jedi Stealth", "JE_STEALTH"),
            B("Force Levitate", "JE_LEVITATE"),
            B("Jedi Frenzy", "JE_FRENZY"),
            B("Force Sacrifice", "JE_SACRIFICE")
        };
    }

    private static void InitializeItemBuffs()
    {

        // Clear collections before adding to prevent duplicates
        PotionBuffs.Clear();
        ElementBuffs.Clear();
        FoodBuffs.Clear();
        BoxBuffs.Clear();
        ScrollBuffs.Clear();
        EtcBuffs.Clear();
        FishBuffs.Clear();

        PotionBuffs.AddRange(new[]
        {
            B("Concentration Potion", "ATTHASTE_POTION1"),
            B("Awakening Potion", "ATTHASTE_POTION2"),
            B("Berserk Potion", "ATTHASTE_POTION3")
        });

        ElementBuffs.Clear();

        ElementBuffs[0] = new List<Buff>
        {
            B("Fire Elemental Converter", "ENDOW_FIRE"),
            B("Wind Elemental Converter", "ENDOW_WIND"),
            B("Earth Elemental Converter", "ENDOW_EARTH"),
            B("Box of Storms / Water Converter", "BOX_OF_STORMS"),
            B("Cursed Water", "ENDOW_DARK"),
            B("Fireproof Potion", "RESIST_PROPERTY_FIRE"),
            B("Coldproof Potion", "RESIST_PROPERTY_WATER"),
            B("Thunderproof Potion", "RESIST_PROPERTY_WIND"),
            B("Earthproof Potion", "RESIST_PROPERTY_GROUND")
        };

        ElementBuffs[1] = new List<Buff>
        {
            B("Fire Elemental Converter", "ENDOW_FIRE"),
            B("Wind Elemental Converter", "ENDOW_WIND"),
            B("Earth Elemental Converter", "ENDOW_EARTH"),
            B("Box of Storms / Water Converter", "SA_FROSTWEAPON"),
            B("Cursed Water", "ENDOW_DARK"),
            B("Fireproof Potion", "RESIST_PROPERTY_FIRE"),
            B("Coldproof Potion", "RESIST_PROPERTY_WATER"),
            B("Thunderproof Potion", "RESIST_PROPERTY_WIND"),
            B("Earthproof Potion", "RESIST_PROPERTY_GROUND")
        };

        BoxBuffs.AddRange(new[]
        {
            B("Box of Drowsiness / Tasty W. Ration", "PLUSMAGICPOWER"),
            B("Box of Resentment / Tasty P. Ration / Chewy Ricecake", "PLUSATTACKPOWER"),
            B("Box of Gloom", "AC_CONCENTRATION"),
            B("Box of Thunder / Speed Potion", "BOX_OF_THUNDER"),
            B("Anodyne", "SM_ENDURE"),
            B("Aloe Vera", "SM_PROVOKE"),
            B("Abrasive", "CRITICALPERCENT"),
        });

        FoodBuffs.AddRange(new[]
        {
            B("Steamed Tongue (STR+10)", "FOOD_STR"),
            B("Steamed Scorpion (AGI+10)", "FOOD_AGI"),
            B("Stew of Immortality (VIT+10)", "FOOD_VIT"),
            B("Dragon Breath Cocktail (INT+10)", "FOOD_INT"),
            B("Hwergelmir's Tonic (DEX+10)", "FOOD_DEX"),
            B("Cooked Nine Tail's Tails (LUK+10)", "FOOD_LUK"),
            B("Glass of Illusion", "GLASS_OF_ILLUSION")
        });

        ScrollBuffs.AddRange(new[]
        {
            B("Increase Agility Scroll", "AL_INCAGI"),
            B("Bless Scroll", "AL_BLESSING"),
            B("Talisman / Link Scroll", "SOULLINK"),
            B("Assumptio Scroll", "HP_ASSUMPTIO"),
            B("Spray of Flowers / Flee Scroll", "FOOD_BASICAVOIDANCE"),
        });

        EtcBuffs.AddRange(new[]
        {
            B("VIP Ticket", "VIP_BONUS"),
         /*
          * This one won't work because the item is a box type with a dialog which can't be assigned to a hotkey.
          * B("SVIP Ticket", "SVIP_BONUS"),
          */
            B("Field Manual 100% / 300%", "FIELD_MANUAL"),
            B("Bubble Gum / HE Bubble Gum", "CASH_RECEIVEITEM"),
            B("Unlock Bubble Gum", "UNLOCK_BUBBLEGUM")
        });

        FishBuffs.AddRange(new[]
        {
            B("Energy Drink", "ENERGY_DRINK"),
            B("Max Potion", "MAX_POTION"),
            B("Dash Juice", "DASH_JUICE"),
            B("Demon Extract", "DEMON_EXTRACT"),
            B("Stoneskin Extract", "STONESKIN_EXTRACT"),
            B("Psychoserum", "PSYCHOSERUM"),
            B("STR Tonic", "STR_TONIC"),
            B("AGI Tonic", "AGI_TONIC"),
            B("VIT Tonic", "VIT_TONIC"),
            B("INT Tonic", "INT_TONIC"),
            B("DEX Tonic", "DEX_TONIC"),
            B("LUK Tonic", "LUK_TONIC")
        });
    }

    private static void InitializeDebuffs()
    {

        // Clear collection before adding to prevent duplicates
        Debuffs.Clear();

        Debuffs.AddRange(new[]
        {
            B("Bleeding", "NPC_BLEEDING"),
            B("Burning", "BURNING"),
            B("Chaos / Confusion", "CONFUSION"),
            B("Critical Wound", "NPC_CRITICALWOUND"),
            B("Curse", "CURSE"),
            B("Decrease AGI", "AL_DECAGI"),
            B("Freezing", "FREEZING"),
            B("Frozen", "FROZEN"),
            B("Hallucination", "NPC_HALLUCINATION"),
            B("Poison", "POISON"),
            B("Silence", "SILENCE"),
            B("Sit", "SIT"),
            B("Deep Sleep", "DEEP_SLEEP"),
            B("Sleep", "SLEEP"),
            B("Slow Cast", "NPC_SLOWCAST"),
            B("Stone Curse (initial stage)", "STONECURSE_ING"),
            B("Stone Curse (petrified)", "STONECURSE"),
            B("Stun", "STUN")
        });
    }
}

// ── Static facade ─────────────────────────────────────────────────────────────
public static class BuffService
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        BuffDefinitions.Initialize();
        _initialized = true;
    }

    private static List<Buff> ServerList(Dictionary<int, List<Buff>> d)
        => d.TryGetValue(AppConfig.ServerMode, out var list) ? list : (d.TryGetValue(0, out var fb) ? fb : new List<Buff>());

    public static Buff? GetBuff(EffectStatusIDs statusId)
    {
        Initialize();
        
        var currentLists = new[]
        {
            ServerList(BuffDefinitions.ArcherBuffs), ServerList(BuffDefinitions.SwordmanBuffs),
            ServerList(BuffDefinitions.MageBuffs), ServerList(BuffDefinitions.MerchantBuffs),
            ServerList(BuffDefinitions.ThiefBuffs), ServerList(BuffDefinitions.AcolyteBuffs),
            ServerList(BuffDefinitions.NinjaBuffs), ServerList(BuffDefinitions.TaekwonBuffs),
            ServerList(BuffDefinitions.GunslingerBuffs), ServerList(BuffDefinitions.PadawanBuffs),
            BuffDefinitions.PotionBuffs, ServerList(BuffDefinitions.ElementBuffs),
            BuffDefinitions.FoodBuffs, BuffDefinitions.BoxBuffs,
            BuffDefinitions.ScrollBuffs, BuffDefinitions.EtcBuffs,
            BuffDefinitions.FishBuffs, BuffDefinitions.Debuffs
        };
        foreach (var list in currentLists)
        {
            var buff = list.FirstOrDefault(b => b.EffectStatusID == statusId);
            if (buff != null) return buff;
        }

        // Fallback: search all dictionaries regardless of server mode
        var allDictionaries = new[]
        {
            BuffDefinitions.ArcherBuffs, BuffDefinitions.SwordmanBuffs,
            BuffDefinitions.MageBuffs, BuffDefinitions.MerchantBuffs,
            BuffDefinitions.ThiefBuffs, BuffDefinitions.AcolyteBuffs,
            BuffDefinitions.NinjaBuffs, BuffDefinitions.TaekwonBuffs,
            BuffDefinitions.GunslingerBuffs, BuffDefinitions.PadawanBuffs,
            BuffDefinitions.ElementBuffs
        };
        foreach (var dict in allDictionaries)
        {
            foreach (var kvp in dict)
            {
                var buff = kvp.Value.FirstOrDefault(b => b.EffectStatusID == statusId);
                if (buff != null) return buff;
            }
        }

        return null;
    }

    // Skill buffs
    public static List<Buff> GetArcherBuffs()      => ServerList(BuffDefinitions.ArcherBuffs);
    public static List<Buff> GetSwordmanBuffs()    => ServerList(BuffDefinitions.SwordmanBuffs);
    public static List<Buff> GetMageBuffs()        => ServerList(BuffDefinitions.MageBuffs);
    public static List<Buff> GetMerchantBuffs()    => ServerList(BuffDefinitions.MerchantBuffs);
    public static List<Buff> GetThiefBuffs()       => ServerList(BuffDefinitions.ThiefBuffs);
    public static List<Buff> GetAcolyteBuffs()     => ServerList(BuffDefinitions.AcolyteBuffs);
    public static List<Buff> GetNinjaBuffs()       => ServerList(BuffDefinitions.NinjaBuffs);
    public static List<Buff> GetTaekwonBuffs()     => ServerList(BuffDefinitions.TaekwonBuffs);
    public static List<Buff> GetGunslingerBuffs()  => ServerList(BuffDefinitions.GunslingerBuffs);
    public static List<Buff> GetPadawanBuffs()     => ServerList(BuffDefinitions.PadawanBuffs);

    // Item buffs
    public static List<Buff> GetPotionBuffs()  => new(BuffDefinitions.PotionBuffs);
    public static List<Buff> GetElementBuffs() => ServerList(BuffDefinitions.ElementBuffs);
    public static List<Buff> GetFoodBuffs()    => new(BuffDefinitions.FoodBuffs);
    public static List<Buff> GetBoxBuffs()     => new(BuffDefinitions.BoxBuffs);
    public static List<Buff> GetScrollBuffs()  => new(BuffDefinitions.ScrollBuffs);
    public static List<Buff> GetEtcBuffs()     => new(BuffDefinitions.EtcBuffs);
    public static List<Buff> GetFishBuffs()    => new(BuffDefinitions.FishBuffs);

    // Debuffs
    public static List<Buff> GetDebuffs() => new(BuffDefinitions.Debuffs);
}
