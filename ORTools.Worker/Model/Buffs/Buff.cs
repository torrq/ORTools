namespace ORTools.Worker;

// Worker-only Buff: data-only, no Bitmap/IResourceLoader needed.
public class Buff
{
    public string         Name          { get; set; } = "";
    public EffectStatusIDs EffectStatusID { get; set; }
    public string IconName { get; set; } = "";

    public Buff() { }
    public Buff(string name, EffectStatusIDs effectStatus, string iconName = "")
    {
        Name          = name;
        EffectStatusID = effectStatus;
        IconName = iconName;
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
    public static readonly Dictionary<int, List<Buff>> FoodBuffs = new Dictionary<int, List<Buff>>();
    public static readonly Dictionary<int, List<Buff>> BoxBuffs = new Dictionary<int, List<Buff>>();
    public static readonly Dictionary<int, List<Buff>> ScrollBuffs = new Dictionary<int, List<Buff>>();
    public static readonly Dictionary<int, List<Buff>> EtcBuffs = new Dictionary<int, List<Buff>>();
    public static readonly List<Buff> FishBuffs = new List<Buff>();

    // DEBUFFS
    public static readonly List<Buff> Debuffs = new List<Buff>();


    // B() helper — parses effectStatusName and creates a data-only Buff
    private static Buff B(string name, string effectStatusName, string iconName)
    {
        if (!Enum.TryParse<EffectStatusIDs>(effectStatusName, out var effect))
            DebugLogger.Debug($"[Buff] Unknown EffectStatusID: {effectStatusName} for buff: {name}");
        return new Buff(name, effect, iconName);
    }

    public static void Initialize()
    {
        InitializeSkillBuffs();
        InitializeItemBuffs();
        InitializeDebuffs();
    }

    private static void InitializeSkillBuffs() {
        ArcherBuffs[0] = new List<Buff>
        {
            B("Improve Concentration", "AC_CONCENTRATION", "ac_concentration"),
            B("True Sight", "SN_SIGHT", "sn_sight"),
            B("Wind Walk", "SN_WINDWALK", "sn_windwalk")
        };

        SwordmanBuffs[0] = new List<Buff>
        {
            B("Endure", "SM_ENDURE", "sm_endure"),
            B("Auto Berserk", "SM_AUTOBERSERK", "sm_autoberserk"),
            B("Auto Guard", "CR_AUTOGUARD", "cr_autoguard"),
            B("Reflect Shield", "CR_REFLECTSHIELD", "cr_reflectshield"),
            B("Spear Quicken", "CR_SPEARQUICKEN", "cr_spearquicken"),
            B("Defender", "CR_DEFENDER", "cr_defender"),
            B("Concentration", "LK_CONCENTRATION", "lk_concentration"),
            B("Berserk", "LK_BERSERK", "lk_berserk"),
            B("Two-Hand Quicken", "KN_TWOHANDQUICKEN", "mer_quicken"),
            B("Parry", "LK_PARRYING", "ms_parrying"),
            B("Aura Blade", "LK_AURABLADE", "lk_aurablade"),
            B("Shrink", "CR_SHRINK", "cr_shrink"),
            B("Magnum Break", "SM_MAGNUM", "magnum"),
            B("One-Hand Quicken", "KN_ONEHAND", "lk_onehand"),
            B("Provoke", "SM_PROVOKE", "provoke"),
            B("Providence", "CR_PROVIDENCE", "providence")
        };
        SwordmanBuffs[1] = new List<Buff>
        {
            B("Endure", "SM_ENDURE", "sm_endure"),
            B("Auto Berserk", "SM_AUTOBERSERK", "sm_autoberserk"),
            B("Auto Guard", "CR_AUTOGUARD", "cr_autoguard"),
            B("Reflect Shield", "CR_REFLECTSHIELD", "cr_reflectshield"),
            B("Spear Quicken", "CR_SPEARQUICKEN", "cr_spearquicken"),
            B("Defender", "CR_DEFENDER", "cr_defender"),
            B("Concentration", "LK_CONCENTRATION", "lk_concentration"),
            B("Berserk", "LK_BERSERK", "lk_berserk"),
            B("Two-Hand Quicken", "KN_TWOHANDQUICKEN", "mer_quicken"),
            B("Parry", "LK_PARRYING", "ms_parrying"),
            B("Aura Blade", "LK_AURABLADE", "lk_aurablade"),
            B("Shrink", "CR_SHRINK", "cr_shrink"),
            B("Magnum Break", "SM_MAGNUM", "magnum"),
            B("One-Hand Quicken", "KN_ONEHAND", "lk_onehand"),
            B("Provoke", "SM_PROVOKE", "provoke"),
            B("Providence", "CR_PROVIDENCE", "providence"),
            B("Mana Shield", "MANA_SHIELD", "manashield")
        };

        MageBuffs[0] = new List<Buff>
        {
            B("Energy Coat", "MG_ENERGYCOAT", "mg_energycoat"),
            B("Sight Blaster", "WZ_SIGHTBLASTER", "wz_sightblaster"),
            B("Autospell", "SA_AUTOSPELL", "sa_autospell"),
            B("Double Casting", "PF_DOUBLECASTING", "pf_doublecasting"),
            B("Memorize", "PF_MEMORIZE", "pf_memorize"),
            B("Amplify Magic Power / Mystical Amplification", "HW_MAGICPOWER", "amplify"),
            B("Mind Breaker", "PF_MINDBREAKER", "mindbreaker")
        };

        MerchantBuffs[0] = new List<Buff>
        {
            B("Crazy Uproar", "MC_LOUD", "mc_loud"),
            B("Overthrust", "BS_OVERTHRUST", "bs_overthrust"),
            B("Adrenaline Rush", "BS_ADRENALINE", "bs_adrenaline"),
            B("Full Adrenaline Rush", "BS_ADRENALINE2", "bs_adrenaline2"),
            B("Weapon Perfection", "BS_WEAPONPERFECT", "bs_weaponperfect"),
            B("Maximize Power", "BS_MAXIMIZE", "bs_maximize"),
            B("Cart Boost", "WS_CARTBOOST", "ws_cartboost"),
            B("Meltdown", "WS_MELTDOWN", "ws_meltdown"),
            B("Maximum Overthrust", "WS_OVERTHRUSTMAX", "ws_overthrustmax"),
            B("Greed Parry", "WS_GREEDPARRY", "ws_greedparry")
        };
        MerchantBuffs[1] = new List<Buff>
        {
            B("Crazy Uproar", "MC_LOUD", "mc_loud"),
            B("Overthrust", "BS_OVERTHRUST", "bs_overthrust"),
            B("Adrenaline Rush", "BS_ADRENALINE", "bs_adrenaline"),
            B("Full Adrenaline Rush", "BS_ADRENALINE2", "bs_adrenaline2"),
            B("Weapon Perfection", "BS_WEAPONPERFECT", "bs_weaponperfect"),
            B("Maximize Power", "BS_MAXIMIZE", "bs_maximize"),
            B("Cart Boost", "WS_CARTBOOST", "ws_cartboost"),
            B("Meltdown", "WS_MELTDOWN", "ws_meltdown"),
            B("Maximum Overthrust", "WS_OVERTHRUSTMAX", "ws_overthrustmax"),
            B("Greed Parry", "RESIST_PROPERTY_FIRE", "ws_greedparry")
        };

        ThiefBuffs[0] = new List<Buff>
        {
            B("Poison React", "AS_POISONREACT", "as_poisonreact"),
            B("Reject Sword", "ST_REJECTSWORD", "st_rejectsword"),
            B("Preserve", "ST_PRESERVE", "st_preserve"),
            B("Enchant Deadly Poison", "ASC_EDP", "asc_edp"),
            B("Hide", "TF_HIDING", "hiding"),
            B("Cloak", "AS_CLOAKING", "cloaking"),
            B("Chase Walk", "ST_CHASEWALK", "chase_walk")
        };
        ThiefBuffs[1] = new List<Buff>
        {
            B("Poison React", "AS_POISONREACT", "as_poisonreact"),
            B("Reject Sword", "ST_REJECTSWORD", "st_rejectsword"),
            B("Preserve", "ST_PRESERVE", "st_preserve"),
            B("Enchant Deadly Poison", "ASC_EDP", "asc_edp"),
            B("Hide", "TF_HIDING", "hiding"),
            B("Cloak", "AS_CLOAKING", "cloaking"),
            B("Chase Walk", "ST_CHASEWALK", "chase_walk"),
            B("Enchant Poison Armor", "ENCHANT_POISON_ARMOR", "enchantpoisonarmor")
        };

        AcolyteBuffs[0] = new List<Buff>
        {
            B("Blessing", "AL_BLESSING", "al_blessing"),
            B("Increase Agility", "AL_INCAGI", "al_incagi"),
            B("Gloria", "PR_GLORIA", "pr_gloria"),
            B("Magnificat", "PR_MAGNIFICAT", "pr_magnificat"),
            B("Angelus", "AL_ANGELUS", "al_angelus"),
            B("Impositio Manus", "PR_IMPOSITIO", "impositio_manus"),
            B("Basilica", "HP_BASILICA", "basilica"),
            B("Fury", "MO_EXPLOSIONSPIRITS", "fury"),
            B("Steel Body", "MO_STEELBODY", "steel_body"),
        };
        AcolyteBuffs[1] = new List<Buff>
        {
            B("Blessing", "AL_BLESSING", "al_blessing"),
            B("Increase Agility", "AL_INCAGI", "al_incagi"),
            B("Gloria", "PR_GLORIA", "pr_gloria"),
            B("Magnificat", "PR_MAGNIFICAT", "pr_magnificat"),
            B("Angelus", "AL_ANGELUS", "al_angelus"),
            B("Impositio Manus", "PR_IMPOSITIO", "impositio_manus"),
            B("Basilica", "HP_BASILICA", "basilica"),
            B("Fury", "MO_EXPLOSIONSPIRITS", "fury"),
            B("Steel Body", "MO_STEELBODY", "steel_body"),
            B("Refraction", "REFRACTION", "refraction"),
            B("Shallow Grave", "SL_KAIZEL", "shallowgrave")
        };

        NinjaBuffs[0] = new List<Buff>
        {
            B("Ninja Aura", "NJ_NEN", "nj_nen"),
            B("Cast-off Cicada / Cicada SS", "NJ_UTSUSEMI", "nj_utsusemi"),
            B("Illusion Shadow / Mirror Image", "NJ_BUNSINJYUTSU", "bunsinjyutsu")
        };

        TaekwonBuffs[0] = new List<Buff>
        {
            B("Mild Wind (Earth)", "SA_SEISMICWEAPON", "tk_mild_earth"),
            B("Mild Wind (Fire)", "SA_FLAMELAUNCHER", "tk_mild_fire"),
            B("Mild Wind (Water)", "SA_FROSTWEAPON", "tk_mild_water"),
            B("Mild Wind (Wind)", "SA_LIGHTNINGLOADER", "tk_mild_wind"),
            B("Mild Wind (Ghost)", "PROPERTYTELEKINESIS", "tk_mild_ghost"),
            B("Mild Wind (Holy)", "PR_ASPERSIO", "tk_mild_holy"),
            B("Mild Wind (Shadow)", "PROPERTYDARK", "tk_mild_shadow"),
            B("Tumbling", "MO_DODGE", "tumbling"),
            B("Solar, Lunar, and Stellar Warmth", "WARM", "sun_warm"),
            B("Comfort of the Sun", "SG_SUN_COMFORT", "sun_comfort"),
            B("Comfort of the Moon", "SG_MOON_COMFORT", "moon_comfort"),
            B("Comfort of the Stars", "SG_STAR_COMFORT", "star_comfort"),
            B("Kaupe", "SL_KAUPE", "kaupe"),
            B("Kaite", "SL_KAITE", "kaite"),
            B("Kaizel", "SL_KAIZEL", "kaizel"),
            B("Kaahi", "SL_KAAHI", "kaahi")
        };

        GunslingerBuffs[0] = new List<Buff>
        {
            B("Gatling Fever", "GS_GATLINGFEVER", "gatling_fever"),
            B("Last Stand", "GS_MADNESSCANCEL", "madnesscancel"),
            B("Adjustment", "GS_ADJUSTMENT", "adjustment"),
            B("Increased Accuracy", "GS_INCREASING", "increase_accuracy")
        };

        PadawanBuffs[1] = new List<Buff>
        {
            B("Force Element (Earth)", "SA_SEISMICWEAPON", "forceelement_earth"),
            B("Force Element (Wind)", "SA_LIGHTNINGLOADER", "forceelement_wind"),
            B("Force Element (Water)", "SA_FROSTWEAPON", "forceelement_water"),
            B("Force Element (Fire)", "SA_FLAMELAUNCHER", "forceelement_fire"),
            B("Force Element (Ghost)", "PROPERTYTELEKINESIS", "forceelement_ghost"),
            B("Force Element (Shadow)", "PROPERTYDARK", "forceelement_shadow"),
            B("Force Element (Holy)", "PR_ASPERSIO", "forceelement_holy"),
            B("Force Projection", "HR_PROJECTION", "hr_forceprojection"),
            B("Cold Skin", "RESIST_PROPERTY_WATER", "hr_coldskin"),
            B("Saber Parry", "HR_SABERPARRY", "hr_saberparry"),
            B("Force Concentration", "HR_FORCECONCENTRATE", "hr_forceconcentrate"),
            B("Saber Thrust", "LK_CONCENTRATION", "hr_saberthrust"),
            B("Force Persuasion", "HR_FORCEPERSUASION", "hr_forcepersuasion"),
            B("Force Haste", "HR_FORCEHASTE", "forcehaste"),
            B("Force Sacrifice", "HR_FORCESACRIFICE", "hr_forcesacrifice"),
            B("Jedi Frenzy", "HR_JEDIFRENZY", "hr_jedifrenzy")
        };
        PadawanBuffs[0] = new List<Buff>
        {
            B("Force Element (Earth)", "PD_ELEMENT_EARTH", "forceelement_earth"),
            B("Force Element (Wind)", "PD_ELEMENT_WIND", "forceelement_wind"),
            B("Force Element (Water)", "PD_ELEMENT_WATER", "forceelement_water"),
            B("Force Element (Fire)", "PD_ELEMENT_FIRE", "forceelement_fire"),
            B("Force Element (Ghost)", "PD_ELEMENT_GHOST", "forceelement_ghost"),
            B("Force Element (Shadow)", "PD_ELEMENT_SHADOW", "forceelement_shadow"),
            B("Force Element (Holy)", "PD_ELEMENT_HOLY", "forceelement_holy"),
            B("Force Projection", "SI_PROJECTION", "forceprojection"),
            B("Cold Skin", "SI_COLDSKIN", "coldskin"),
            B("Saber Parry", "JS_SABERPARRY", "saberparry"),
            B("Force Concentration", "JS_CONCENTRATE", "forceconcentrate"),
            B("Saber Thrust", "SI_SABERTHRUST", "saberthrust"),
            B("Force Persuasion", "JS_PERSUADE", "forcepersuasion"),
            B("Jedi Stealth", "JE_STEALTH", "jedistealth"),
            B("Force Levitate", "JE_LEVITATE", "forcelevitate"),
            B("Jedi Frenzy", "JE_FRENZY", "jedifrenzy"),
            B("Force Sacrifice", "JE_SACRIFICE", "forcesacrifice")
        };
     }

    private static void InitializeItemBuffs() {
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
            B("Concentration Potion", "ATTHASTE_POTION1", "concentration_potion"),
            B("Awakening Potion", "ATTHASTE_POTION2", "awakening_potion"),
            B("Berserk Potion", "ATTHASTE_POTION3", "berserk_potion")
        });

        ElementBuffs[0] = new List<Buff>
        {
            B("Fire Elemental Converter", "ENDOW_FIRE", "ele_fire_converter"),
            B("Wind Elemental Converter", "ENDOW_WIND", "ele_wind_converter"),
            B("Earth Elemental Converter", "ENDOW_EARTH", "ele_earth_converter"),
            B("Box of Storms / Water Converter", "BOX_OF_STORMS", "boxofstorms"),
            B("Cursed Water", "ENDOW_DARK", "cursed_water"),
            B("Fireproof Potion", "RESIST_PROPERTY_FIRE", "fireproof"),
            B("Coldproof Potion", "RESIST_PROPERTY_WATER", "coldproof"),
            B("Thunderproof Potion", "RESIST_PROPERTY_WIND", "thunderproof"),
            B("Earthproof Potion", "RESIST_PROPERTY_GROUND", "earthproof")
        };

        ElementBuffs[1] = new List<Buff>
        {
            B("Fire Elemental Converter", "SA_FLAMELAUNCHER", "ele_fire_converter"),
            B("Wind Elemental Converter", "SA_LIGHTNINGLOADER", "ele_wind_converter"),
            B("Earth Elemental Converter", "SA_SEISMICWEAPON", "ele_earth_converter"),
            B("Box of Storms / Water Converter", "SA_FROSTWEAPON", "boxofstorms"),
            B("Cursed Water", "PROPERTYDARK", "cursed_water"),
/* HR has no statuses for these (yet)
            B("Fireproof Potion", "RESIST_PROPERTY_FIRE", "fireproof"),
            B("Coldproof Potion", "RESIST_PROPERTY_WATER", "coldproof"),
            B("Thunderproof Potion", "RESIST_PROPERTY_WIND", "thunderproof"),
            B("Earthproof Potion", "RESIST_PROPERTY_GROUND", "earthproof")
*/
        };

        BoxBuffs[0] = new List<Buff>
        {
            B("Box of Drowsiness / Tasty W. Ration", "PLUSMAGICPOWER", "drowsiness"),
            B("Box of Resentment / Tasty P. Ration / Chewy Ricecake", "PLUSATTACKPOWER", "resentment"),
            B("Box of Gloom", "AC_CONCENTRATION", "gloom"),
            B("Box of Thunder / Speed Potion", "BOX_OF_THUNDER", "boxofthunder"),
            B("Anodyne", "SM_ENDURE", "anodyne"),
            B("Aloe Vera", "SM_PROVOKE", "aloevera"),
            B("Abrasive", "CRITICALPERCENT", "abrasive"),
        };

        BoxBuffs[1] = new List<Buff>
        {
            B("Box of Drowsiness", "PLUSMAGICPOWER", "drowsiness"),
            B("Box of Resentment", "PLUSATTACKPOWER", "resentment"),
            B("Box of Gloom", "AC_CONCENTRATION", "gloom"),
            B("Speed Potion", "MOVHASTE_POTION", "speedpotion"),
            B("Anodyne", "SM_ENDURE", "anodyne"),
            B("Aloe Vera", "SM_PROVOKE", "aloevera"),
            B("Abrasive", "CRITICALPERCENT", "abrasive"),
        };

        FoodBuffs[0] = new List<Buff>
        {
            B("Steamed Tongue (STR+10)", "FOOD_STR", "food_str"),
            B("Steamed Scorpion (AGI+10)", "FOOD_AGI", "food_agi"),
            B("Stew of Immortality (VIT+10)", "FOOD_VIT", "food_vit"),
            B("Dragon Breath Cocktail (INT+10)", "FOOD_INT", "food_int"),
            B("Hwergelmir's Tonic (DEX+10)", "FOOD_DEX", "food_dex"),
            B("Cooked Nine Tail's Tails (LUK+10)", "FOOD_LUK", "food_luk"),
        };

        FoodBuffs[1] = new List<Buff>
        {
            B("Steamed Tongue (STR+10)", "FOOD_STR", "food_str"),
            B("Steamed Scorpion (AGI+10)", "FOOD_AGI", "food_agi"),
            B("Stew of Immortality (VIT+10)", "FOOD_VIT", "food_vit"),
            B("Dragon Breath Cocktail (INT+10)", "FOOD_INT", "food_int"),
            B("Hwergelmir's Tonic (DEX+10)", "FOOD_DEX", "food_dex"),
            B("Cooked Nine Tail's Tails (LUK+10)", "FOOD_LUK", "food_luk"),
        };

        ScrollBuffs[0] = new List<Buff>
        {
            B("Increase AGI", "AL_INCAGI", "al_incagi"),
            B("Blessing", "AL_BLESSING", "al_blessing"),
            B("Talisman / Link Scroll", "SOULLINK", "sl_soullinker"),
            B("Assumptio", "HP_ASSUMPTIO", "assumptio"),
            B("Spray of Flowers / Flee Scrol", "FOOD_BASICAVOIDANCE", "flee_scroll"),
            B("Glass of Illusion", "GLASS_OF_ILLUSION", "Glass_Of_Illusion"),
        };

        ScrollBuffs[1] = new List<Buff>
        {
            B("Increase AGI / Box of Thunder", "AL_INCAGI", "al_incagi"),
            B("Blessing", "AL_BLESSING", "al_blessing"),
            B("Talisman / Link Scroll", "SOULLINK", "sl_soullinker_hr"),
            B("Assumptio", "HP_ASSUMPTIO", "assumptio"),
            B("Glass of Illusion", "GLASS_OF_ILLUSION", "Glass_Of_Illusion"),
            B("HP Increase Potion (Large)", "HP_INCREASE_POTION_LARGE", "hp_increase_potion_large"),
            B("Military Ration B", "ACCURACY_SCROLL", "military_ration"),
            B("Military Ration C", "FOOD_BASICAVOIDANCE", "military_ration"),
            B("SP Consumption Decrease Potion", "SPELLBREAKER", "sp_consumption_potion")
        };

        EtcBuffs[0] = new List<Buff>
        {
            B("VIP Ticket", "VIP_BONUS", "vip_ticket"),
         /*
          * This one won't work because the item is a box type with a dialog which can't be assigned to a hotkey.
          * B("SVIP Ticket", "SVIP_BONUS", "vip_ticket"),
          */
            B("Field Manual 100% / 300%", "FIELD_MANUAL", "fieldmanual"),
            B("Bubble Gum / HE Bubble Gum", "CASH_RECEIVEITEM", "he_bubble_gum"),
            B("Unlock Bubble Gum", "UNLOCK_BUBBLEGUM", "unlock_bbg")
        };

        EtcBuffs[1] = new List<Buff>
        {
            /* not a useable item on HR
            B("VIP Ticket", "VIP_BONUS", "vip_ticket"),
            */
            B("Field Manual 100% / 300%", "FIELD_MANUAL", "fieldmanual"),
            B("Bubble Gum / HE Bubble Gum", "CASH_RECEIVEITEM", "he_bubble_gum"),
            B("Unlock Bubble Gum", "UNLOCK_BUBBLEGUM", "unlock_bbg")
        };

        FishBuffs.AddRange(new[]
        {
            B("Energy Drink", "ENERGY_DRINK", "energy_drink"),
            B("Max Potion", "MAX_POTION", "max_potion"),
            B("Dash Juice", "DASH_JUICE", "dash_juice"),
            B("Demon Extract", "DEMON_EXTRACT", "demon_extract"),
            B("Stoneskin Extract", "STONESKIN_EXTRACT", "stoneskin_extract"),
            B("Psychoserum", "PSYCHOSERUM", "psychoserum"),
            B("STR Tonic", "STR_TONIC", "str_tonic"),
            B("AGI Tonic", "AGI_TONIC", "agi_tonic"),
            B("VIT Tonic", "VIT_TONIC", "vit_tonic"),
            B("INT Tonic", "INT_TONIC", "int_tonic"),
            B("DEX Tonic", "DEX_TONIC", "dex_tonic"),
            B("LUK Tonic", "LUK_TONIC", "luk_tonic")
        });
     }

    private static void InitializeDebuffs() {
        // Clear collection before adding to prevent duplicates
        Debuffs.Clear();

        Debuffs.AddRange(new List<Buff>
        {
            B("Burning", "BURNING", "burning"),
            B("Chaos / Confusion", "CONFUSION", "chaos"),
            B("Critical Wound", "NPC_CRITICALWOUND", "critical_wound"),
            B("Curse", "CURSE", "curse"),
            B("Decrease AGI", "AL_DECAGI", "decrease_agi"),
            B("Freezing", "FREEZING", "freezing"),
            B("Frozen", "FROZEN", "frozen"),
            B("Poison", "POISON", "poison_status"),
            B("Silence", "SILENCE", "silence"),
            B("Sit", "SIT", "sit"),
            B("Deep Sleep", "DEEP_SLEEP", "deep_sleep"),
            B("Sleep", "SLEEP", "sleep"),
            B("Slow Cast", "NPC_SLOWCAST", "slow_cast"),
            B("Stone Curse (initial stage)", "STONECURSE_ING", "stonecurse1"),
            B("Stone Curse (petrified)", "STONECURSE", "stonecurse2"),
            B("Stun", "STUN", "stun")
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
            ServerList(BuffDefinitions.FoodBuffs), ServerList(BuffDefinitions.BoxBuffs),
            ServerList(BuffDefinitions.ScrollBuffs), ServerList(BuffDefinitions.EtcBuffs),
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
    public static List<Buff> GetArcherBuffs()      { Initialize(); return ServerList(BuffDefinitions.ArcherBuffs); }
    public static List<Buff> GetSwordmanBuffs()    { Initialize(); return ServerList(BuffDefinitions.SwordmanBuffs); }
    public static List<Buff> GetMageBuffs()        { Initialize(); return ServerList(BuffDefinitions.MageBuffs); }
    public static List<Buff> GetMerchantBuffs()    { Initialize(); return ServerList(BuffDefinitions.MerchantBuffs); }
    public static List<Buff> GetThiefBuffs()       { Initialize(); return ServerList(BuffDefinitions.ThiefBuffs); }
    public static List<Buff> GetAcolyteBuffs()     { Initialize(); return ServerList(BuffDefinitions.AcolyteBuffs); }
    public static List<Buff> GetNinjaBuffs()       { Initialize(); return ServerList(BuffDefinitions.NinjaBuffs); }
    public static List<Buff> GetTaekwonBuffs()     { Initialize(); return ServerList(BuffDefinitions.TaekwonBuffs); }
    public static List<Buff> GetGunslingerBuffs()  { Initialize(); return ServerList(BuffDefinitions.GunslingerBuffs); }
    public static List<Buff> GetPadawanBuffs()     { Initialize(); return ServerList(BuffDefinitions.PadawanBuffs); }

    // Item buffs
    public static List<Buff> GetPotionBuffs()  { Initialize(); return new(BuffDefinitions.PotionBuffs); }
    public static List<Buff> GetElementBuffs() { Initialize(); return ServerList(BuffDefinitions.ElementBuffs); }
    public static List<Buff> GetFoodBuffs()    { Initialize(); return ServerList(BuffDefinitions.FoodBuffs); }
    public static List<Buff> GetBoxBuffs()     { Initialize(); return ServerList(BuffDefinitions.BoxBuffs); }
    public static List<Buff> GetScrollBuffs()  { Initialize(); return ServerList(BuffDefinitions.ScrollBuffs); }
    public static List<Buff> GetEtcBuffs()     { Initialize(); return ServerList(BuffDefinitions.EtcBuffs); }
    public static List<Buff> GetFishBuffs()    { Initialize(); return new(BuffDefinitions.FishBuffs); }

    // Debuffs
    public static List<Buff> GetDebuffs() { Initialize(); return new(BuffDefinitions.Debuffs); }
}


