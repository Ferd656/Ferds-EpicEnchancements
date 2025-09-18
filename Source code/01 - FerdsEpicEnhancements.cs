// File: 01 - FerdsEpicEnhancements.cs
// Target: .NET Framework 4.7.2
using System;
using BepInEx;
using System.IO;
using HarmonyLib;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;
using System.Reflection;
using System.Collections;
using SoftReferenceableAssets;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FerdEpicEnhancements
{
    [BepInPlugin("Ferd.EpicEnhancements", "FerdsEpicEnhancements", "1.0.3")]
    [BepInDependency("Yggdrah.DragonRiders", BepInDependency.DependencyFlags.HardDependency)]
    public class FerdsEpicEnhancementsPlugin : BaseUnityPlugin
    {
        // Setup
        public const string PluginVersion = "1.0.3";
        public const string PluginGuid = "Ferd.EpicEnhancements";
        public const string PluginName = "FerdsEpicEnhancements";
        public static PluginInfo Metadata =>
            BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info) ? info : null;
        public static FerdsEpicEnhancementsPlugin Instance { get; private set; }
        // Sidepackages
        internal static Harmony _harmony;
        internal static ManualLogSource LogS;
        // Assets
        public static AssetBundle _assetBundle;
        private static string assetbundle_filename = "FerdsEpicAssets";
        public static Material NewCapeMaterial;
        public static AudioClip RenegadeBossMusic;
        internal static GameObject TrinketFireDragonPrefab;
        internal static GameObject TrinketIceDragonPrefab;
        internal static GameObject TrinketLightningDragonPrefab;
        internal static GameObject TrinketFireDragonPrefab_equipped;
        internal static GameObject TrinketIceDragonPrefab_equipped;
        internal static GameObject TrinketLightningDragonPrefab_equipped;
        internal static Sprite TrinketFireDragonPrefab_icon;
        internal static Sprite TrinketIceDragonPrefab_icon;
        internal static Sprite TrinketLightningDragonPrefab_icon;
        private static Sprite BeastLordIcon;
        private static Sprite ScaleWardIcon;
        private static Sprite DragonLeapIcon;
        // Controlllers
        private static bool _appliedOnce = false;
        private static volatile bool _isPatching = false;
        internal static bool _wasDragonRidersLoaded = false;
        internal static bool _wasRenegadeVikingsLoaded = false;
        // Status effects
        private static StatusEffect ScaleWard_SE;
        private static StatusEffect BeastLord_SE;
        private static StatusEffect DragonLeap_SE;
        internal static StatusEffect IceTrinket_SE;
        internal static StatusEffect LightningTrinket_SE;
        internal static StatusEffect FireTrinket_SE;
        private static StatusEffect BurnShock_SE;
        // Config
        private static bool override_yggdras_config = true;
        private static readonly string ConfigFilePath = Path.Combine(Paths.ConfigPath, "Yggdrah.DragonRiders.cfg");
        // Others
        public static GameObject NewDragonSaddleCollider;
        private static GameObject rootObject;
        internal static GameObject RootObject => GetRootObject();

        private void Awake()
        {
            Instance = this;
            LogS = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            // Jotunn
            FerdsEpicEnhancementsPlugin.GetRootObject();
            Runtime.MakeAllAssetsLoadable();
            Game.isModded = true;
            // Get assets
            _assetBundle = LoadEmbeddedBundle(assetbundle_filename);
            if (_assetBundle == null) { LogS.LogError($"[{PluginName}] AssetBundle '{assetbundle_filename}' was not found/loaded. Aborting."); return; }
            NewCapeMaterial = _assetBundle.LoadAsset<Material>("DracgonCape_Mat_New2_Frd");
            TrinketFireDragonPrefab = _assetBundle.LoadAsset<GameObject>("TrinketFireDragon_Frd");
            TrinketIceDragonPrefab = _assetBundle.LoadAsset<GameObject>("TrinketIceDragon_Frd");
            TrinketLightningDragonPrefab = _assetBundle.LoadAsset<GameObject>("TrinketLightningDragon_Frd");
            TrinketFireDragonPrefab_equipped = _assetBundle.LoadAsset<GameObject>("TrinketFD_Equipped_Frd");
            TrinketIceDragonPrefab_equipped = _assetBundle.LoadAsset<GameObject>("TrinketID_Equipped_Frd");
            TrinketLightningDragonPrefab_equipped = _assetBundle.LoadAsset<GameObject>("TrinketLD_Equipped_Frd");
            TrinketFireDragonPrefab_icon = _assetBundle.LoadAsset<Sprite>("FireDragonTrinket_Frd");
            TrinketIceDragonPrefab_icon = _assetBundle.LoadAsset<Sprite>("IceDragonTrinket_Frd");
            TrinketLightningDragonPrefab_icon = _assetBundle.LoadAsset<Sprite>("LightningDragonTrinket_Frd");
            NewDragonSaddleCollider = _assetBundle.LoadAsset<GameObject>("NewDragonSaddleCollider_Frd");
            RenegadeBossMusic = _assetBundle.LoadAsset<AudioClip>("Eihwar");
            ScaleWardIcon = _assetBundle.LoadAsset<Sprite>("ScaleWardIcon_YggFrd");
            BeastLordIcon = _assetBundle.LoadAsset<Sprite>("BeastLordIcon_YggFrd");
            DragonLeapIcon = _assetBundle.LoadAsset<Sprite>("DragonLeapIcon_YggFrd");
            SceneManager.activeSceneChanged += OnSceneChanged;
            if (MusicManager.instance == null)
            {
                var go = new GameObject("MusicManager");
                go.AddComponent<MusicManager>();
                DontDestroyOnLoad(go);
            }
            // ----------
            LogS.LogInfo($"[{PluginName}] {PluginVersion} loaded; will apply upgrade rules once after world is ready.");
        }
        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            bool bumb;
            bumb = IsDragonRidersLoaded() && IsRenegadeVikingsLoaded();
            _appliedOnce = false;
            if (FerdsEpicEnhancementsPlugin.Instance != null)
            {
                FerdsEpicEnhancementsPlugin.Instance.StartCoroutine(FerdsEpicEnhancementsPlugin.Instance.Orchestrator());
            }
        }
        private static bool IsDragonRidersLoaded()
        {
            bool result = false;
            result = AppDomain.CurrentDomain.GetAssemblies().Any(a => {
                var n = a.GetName().Name; return n.IndexOf("DragonRiders", StringComparison.OrdinalIgnoreCase) >= 0;
            });
            if (result) _wasDragonRidersLoaded = true;
            return result;
        }
        internal static bool IsRenegadeVikingsLoaded()
        {
            bool result = false;
            result = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name.IndexOf("RenegadeVikings", StringComparison.OrdinalIgnoreCase) >= 0
                || a.GetName().Name.IndexOf("blacks7ar.RenegadeVikings", StringComparison.OrdinalIgnoreCase) >= 0);
            if (result) _wasRenegadeVikingsLoaded = true;
            return result;
        }
        private void SetConfigOverride()
        {
            if (!IsRenegadeVikingsLoaded()) return;
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    LogS.LogInfo($"Yggdrah's config file not found: {ConfigFilePath}");
                    override_yggdras_config = true;
                    return;
                }
                var lines = File.ReadAllLines(ConfigFilePath).ToList();
                int foundIndex = -1;
                string foundLine = null;
                string keyName = "FerdsRebalanceOverride";
                string keyTrue = keyName + " = true";
                string keyFalse = keyName + " = false";
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Trim().StartsWith(keyName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndex = i;
                        foundLine = lines[i].Trim();
                        break;
                    }
                }
                if (foundIndex >= 0)
                {
                    if (foundLine.Equals(keyFalse, StringComparison.OrdinalIgnoreCase))
                    {
                        override_yggdras_config = false;
                        Logger.LogInfo($"[{PluginName}] {keyName} set to FALSE by Yggdrah's config file");
                    }
                    else if (foundLine.Equals(keyTrue, StringComparison.OrdinalIgnoreCase))
                    {
                        override_yggdras_config = true;
                        Logger.LogInfo($"[{PluginName}] {keyName} set to TRUE by Yggdrah's config file");
                    }
                    else
                    {
                        lines.RemoveAt(foundIndex);
                        lines.Insert(0, keyTrue);
                        override_yggdras_config = true;
                        try
                        {
                            File.WriteAllLines(ConfigFilePath, lines);
                            LogS.LogInfo($"[{PluginName}] Invalid value for {keyName} replaced by '{keyTrue}' at the top of Yggdrah's config file");
                        }
                        catch (Exception ex)
                        {
                            LogS.LogInfo($"[{PluginName}] Error writing config file: {ex.Message}");
                        }
                    }
                }
                else
                {
                    lines.Insert(0, keyTrue);
                    override_yggdras_config = true;
                    try
                    {
                        File.WriteAllLines(ConfigFilePath, lines);
                        LogS.LogInfo($"[{PluginName}] '{keyTrue}' added at the top of Yggdrah's config file");
                    }
                    catch (Exception ex)
                    {
                        LogS.LogInfo($"[{PluginName}] Error writing config file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogS.LogInfo($"[{PluginName}] Error reading Yggdrah's config file: {ex.Message}");
            }
        }
        private void OnDestroy() { try { _harmony?.UnpatchSelf(); } catch (Exception e) { LogS?.LogError(e); } }
        private void OnApplicationQuit() { AssetBundle.UnloadAllAssetBundles(false); }
        /*
        ╔════════════════════════════════════╗
        ║ Core logic                         ║
        ╚════════════════════════════════════╝
        */
        public IEnumerator Orchestrator() 
        {
            // Check if proceed
            if (_appliedOnce || _isPatching) yield break;
            _isPatching = true;
            bool logged = false;
            try
            {
                LogS.LogInfo($"[{PluginName}][Orchestrator] Starting . . .");
                while (ObjectDB.instance == null || ZNetScene.instance == null)
                {
                    if (!logged)
                    {
                        LogS.LogInfo($"[{PluginName}][Orchestrator] Waiting for Odb/Zns...");
                        logged = true;
                    }
                    yield return null;
                }
                logged = false;
                int frames = 0, maxFrames = 10 * 60;
                while ((ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0) && frames < maxFrames)
                {
                    if (!logged)
                    {
                        LogS.LogInfo($"[{PluginName}][Orchestrator] Waiting for ObjectDB.instance.m_items...");
                        logged = true;
                    }
                    yield return null;
                    frames++;
                }
                logged = false;
                frames = 0;
                while (!IsDragonRidersLoaded() && frames < maxFrames)
                {
                    if (!logged)
                    {
                        LogS.LogInfo($"[{PluginName}][Orchestrator] Waiting for DragonRiders...");
                        logged = true;
                    }
                    yield return null;
                    frames++;
                }
                if (!IsDragonRidersLoaded()) { 
                    LogS.LogError($"[{PluginName}] DragonRiders dependency did not appear; aborting core wiring."); yield break; 
                } else { // Check for assetbundle
                    if (_assetBundle == null) { LogS.LogError($"[{PluginName}] AssetBundle not loaded; aborting core logic."); yield break; 
                    } else {
                        // Init status effects
                        SetScaleWard();
                        SetDragonLeap();
                        SetBeastLord();
                        SetBurnShock();
                        SetVetrFadmrExplosion();
                        SetStormrFadmrExplosion();
                        SetEldrFadmrExplosion();
                        // Apply overrides
                        SetConfigOverride();
                        ApplyOverrides();
                        if (_appliedOnce == false) FerdsItemFactory.ForgeTrinkets();
                        // debug
                        //if (ObjectDB.instance.GetItemPrefab("TrinketFireDragon_Frd") == null) LogS?.LogError($"[{PluginName}] Trinket '{TrinketFireDragonPrefab.name}' registration failed!");
                        //if (ObjectDB.instance.GetItemPrefab("TrinketIceDragon_Frd") == null) LogS?.LogError($"[{PluginName}] Trinket '{TrinketIceDragonPrefab.name}' registration failed!");
                        //if (ObjectDB.instance.GetItemPrefab("TrinketLightningDragon_Frd") == null) LogS?.LogError($"[{PluginName}] Trinket '{TrinketLightningDragonPrefab.name}' registration failed!");
                        _appliedOnce = true;
                        LogS.LogInfo($"[{PluginName}][Orchestrator] CoreLogic ready");
                    }
                }
            }
            finally { _isPatching = false; }
        }
        /*
        ╔════════════════════════════════════╗
        ║ Builds & Overrides                 ║
        ╚════════════════════════════════════╝
        */
        private static void ApplyOverrides() {
            // PREFABS
            GameObject dragon_cape_prefab = ObjectDB.instance.GetItemPrefab("Dragon_Cape_Ygg");
            GameObject dragon_chest_prefab = ObjectDB.instance.GetItemPrefab("DragonChest_Ygg");
            GameObject dragon_leggings_prefab = ObjectDB.instance.GetItemPrefab("DragonLeggings_Ygg");
            GameObject dragon_crown_prefab = ObjectDB.instance.GetItemPrefab("DragonCrown_Ygg");
            GameObject dragon_shield_prefab = ObjectDB.instance.GetItemPrefab("DragonShield_Ygg");
            GameObject dragon_helmet_prefab = ObjectDB.instance.GetItemPrefab("HelmetModer_Ygg");
            GameObject dragon_essence_prefab = ObjectDB.instance.GetItemPrefab("Dragon_Essence_Ygg");
            GameObject icedragon_egg_prefab = ObjectDB.instance.GetItemPrefab("Degg_Moder_Ygg");
            GameObject firedragon_egg_prefab = ObjectDB.instance.GetItemPrefab("Degg_Fire_Ygg");
            GameObject lightningdragon_egg_prefab = ObjectDB.instance.GetItemPrefab("Degg_Blue_Ygg");
            GameObject dragonsaddle_prefab = ObjectDB.instance.GetItemPrefab("Saddle_DBone_Ygg");
            // Cape
            if (dragon_cape_prefab) {
                ItemDrop dragon_cape_id = FerdsItemFactory.EnsureItemDrop(dragon_cape_prefab);
                dragon_cape_id.m_itemData.m_shared.m_setName = "ScaleWard";
                dragon_cape_id.m_itemData.m_shared.m_setSize = 3;
                dragon_cape_id.m_itemData.m_shared.m_setStatusEffect = ScaleWard_SE;
                MaterialReplacer(ZNetScene.instance.GetPrefab("Dragon_Cape_Ygg".GetStableHashCode()), NewCapeMaterial, $"ZNS:{dragon_cape_prefab.name}");
                MaterialReplacer(dragon_cape_prefab, NewCapeMaterial, $"ODB:{dragon_cape_prefab.name}");
                if (override_yggdras_config) {
                    dragon_cape_id.m_itemData.m_shared.m_weight = 4;
                    dragon_cape_id.m_itemData.m_shared.m_armor = 16;
                    dragon_cape_id.m_itemData.m_shared.m_armorPerLevel = 2;
                    dragon_cape_id.m_itemData.m_shared.m_maxQuality = 3;
                    dragon_cape_id.m_itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair> {
                        new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant }
                    };
                    dragon_cape_id.m_itemData.m_shared.m_equipStatusEffect = DragonLeap_SE;
                    FerdsItemFactory.RemoveExistingRecipe(dragon_cape_id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = dragon_cape_id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("blackforge").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 4;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("CapeFeather",1,0),
                        FerdsItemFactory.Req("CapeAsh",1,0),
                        FerdsItemFactory.Req("DragonScale_Ygg",2,1),
                        FerdsItemFactory.Req("DragonTear",20,10),
                        FerdsItemFactory.Req("Tar",0,10)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'Dragon_Cape_Ygg' not found"); 
            // Chest
            if (dragon_chest_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragon_chest_prefab);
                id.m_itemData.m_shared.m_setName = "ScaleWard";
                id.m_itemData.m_shared.m_setSize = 3;
                id.m_itemData.m_shared.m_setStatusEffect = ScaleWard_SE; 
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_weight = 10;
                    id.m_itemData.m_shared.m_maxDurability = 9999;
                    id.m_itemData.m_shared.m_movementModifier = -0.05f;
                    id.m_itemData.m_shared.m_armor = 40;
                    id.m_itemData.m_shared.m_armorPerLevel = 2;
                    id.m_itemData.m_shared.m_maxQuality = 3;
                    id.m_itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair>(); 
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("blackforge").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 4;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("ArmorRootChest",1,0),
                        FerdsItemFactory.Req("ArmorCarapaceChest",1,0),
                        FerdsItemFactory. Req("DragonScale_Ygg",4,1),
                        FerdsItemFactory.Req("Electrum_Bar_Ygg",5,5),
                        FerdsItemFactory.Req("DragonTear",0,5),
                        FerdsItemFactory.Req("Eitr",0,15)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'DragonChest_Ygg' not found");
            // Leggings
            if (dragon_leggings_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragon_leggings_prefab);
                id.m_itemData.m_shared.m_setName = "ScaleWard";
                id.m_itemData.m_shared.m_setSize = 3;
                id.m_itemData.m_shared.m_setStatusEffect = ScaleWard_SE;
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_weight = 10;
                    id.m_itemData.m_shared.m_maxDurability = 9999;
                    id.m_itemData.m_shared.m_movementModifier = -0.05f;
                    id.m_itemData.m_shared.m_armor = 40;
                    id.m_itemData.m_shared.m_armorPerLevel = 2;
                    id.m_itemData.m_shared.m_maxQuality = 3;
                    id.m_itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair>();
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("blackforge").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 4;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("ArmorRootLegs",1,0),
                        FerdsItemFactory.Req("ArmorCarapaceLegs",1,0),
                        FerdsItemFactory.Req("DragonScale_Ygg",4,1),
                        FerdsItemFactory.Req("Electrum_Bar_Ygg",5,5),
                        FerdsItemFactory.Req("DragonTear",0,5),
                        FerdsItemFactory.Req("Eitr",0,15)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            }
            else LogS.LogError($"[{PluginName}] Prefab 'DragonLeggings_Ygg' not found");
            // Crown
            if (dragon_crown_prefab)
            {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragon_crown_prefab);
                id.m_itemData.m_shared.m_description = "Wearing this marvelous ornament thy rise as king of the beasts";
                id.m_itemData.m_shared.m_equipStatusEffect = BeastLord_SE; 
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_weight = 0.5f;
                    id.m_itemData.m_shared.m_armor = 10;
                    id.m_itemData.m_shared.m_armorPerLevel = 2;
                    id.m_itemData.m_shared.m_maxQuality = 3;
                    id.m_itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair> {
                        new HitData.DamageModPair { m_type = HitData.DamageType.Fire, m_modifier = HitData.DamageModifier.Resistant }
                    };
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("blackforge").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 4;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("FlametalNew",25,5),
                        FerdsItemFactory.Req("DragonScale_Ygg",2,0),
                        FerdsItemFactory.Req("Electrum_Bar_Ygg",5,2),
                        FerdsItemFactory.Req("DragonTear",20,10),
                        FerdsItemFactory.Req("Ruby",0,1)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'DragonCrown_Ygg' not found");
            // Saddle
            if (dragonsaddle_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragonsaddle_prefab);
                id.m_itemData.m_shared.m_name = "Dragon Saddle";
                id.m_itemData.m_shared.m_description = "Few have been those who have managed to handle the apex of power.";
                foreach (var t in GameObject.FindObjectsByType<Tameable>(FindObjectsSortMode.None)) {
                    try { var m = AccessTools.Method(typeof(Tameable), "HaveSaddle"); if (m != null && (bool)m.Invoke(t, null)) saddle_EnsureOn(t);}catch { }
                }
                if (override_yggdras_config) {
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("piece_artisanstation").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 2;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("SaddleAsksvin",1,0),
                        FerdsItemFactory.Req("Electrum_Bar_Ygg",5,0),
                        FerdsItemFactory.Req("CeramicPlate",10,0),
                        FerdsItemFactory.Req("LinenThread",50,0)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'Saddle_DBone_Ygg' not found");
            // Shield
            if (dragon_shield_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragon_shield_prefab);
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair> {
                        new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.SlightlyResistant }
                    };
                    id.m_itemData.m_shared.m_maxQuality = 3;

                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("blackforge").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 4;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("ShieldSerpentscale",1,0),
                        FerdsItemFactory.Req("DragonScale_Ygg",3,1),
                        FerdsItemFactory.Req("Electrum_Bar_Ygg",5,2),
                        FerdsItemFactory.Req("DragonTear",20,5),
                        FerdsItemFactory.Req("Silver",0,10)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'DragonShield_Ygg' not found");
            // Helmet
            if (dragon_helmet_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragon_helmet_prefab);
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair>();
                    id.m_itemData.m_shared.m_maxQuality = 3;

                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("blackforge").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 4;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("HelmetDrake",1,0),
                        FerdsItemFactory.Req("TrophyDragonQueen",1,0),
                        FerdsItemFactory.Req("DragonScale_Ygg",2,1),
                        FerdsItemFactory.Req("DragonTear",20,5),
                        FerdsItemFactory.Req("TrophyHatchling",0,5),
                        FerdsItemFactory.Req("Obsidian",0,20)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'HelmetModer_Ygg' not found");
            // Eggs
            if (icedragon_egg_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(icedragon_egg_prefab);
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_weight = 200;
                    id.m_itemData.m_shared.m_teleportable = false;
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("piece_artisanstation").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 1;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("DragonEgg",1,0),
                        FerdsItemFactory.Req("DragonTear",1,0),
                        FerdsItemFactory.Req("DragonsHeart_Ygg",1,0),
                        FerdsItemFactory.Req("Dragon_Essence_Ygg",1,0)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            }
            else LogS.LogError($"[{PluginName}] Prefab 'Degg_Moder_Ygg' not found"); 
            if (firedragon_egg_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(firedragon_egg_prefab);
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_weight = 200;
                    id.m_itemData.m_shared.m_teleportable = false;
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("piece_artisanstation").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 2;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("Degg_Moder_Ygg",1,0),
                        FerdsItemFactory.Req("TrophyGjall",5,0),
                        FerdsItemFactory.Req("GemstoneRed",2,0),
                        FerdsItemFactory.Req("SurtlingCore",20,0)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'Degg_Fire_Ygg' not found");
            if (lightningdragon_egg_prefab) {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(lightningdragon_egg_prefab);
                if (override_yggdras_config) {
                    id.m_itemData.m_shared.m_weight = 200;
                    id.m_itemData.m_shared.m_teleportable = false;
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("piece_artisanstation").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 2;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("Degg_Moder_Ygg",1,0),
                        FerdsItemFactory.Req("TrophyEikthyr",1,0),
                        FerdsItemFactory.Req("Thunderstone",5,0),
                        FerdsItemFactory.Req("Eitr",30,0)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'Degg_Blue_Ygg' not found");
            // Essence
            if (dragon_essence_prefab)
            {
                ItemDrop id = FerdsItemFactory.EnsureItemDrop(dragon_essence_prefab);
                if (override_yggdras_config)
                {
                    FerdsItemFactory.RemoveExistingRecipe(id);
                    var r = ScriptableObject.CreateInstance<Recipe>();
                    r.m_item = id;
                    r.m_craftingStation = ZNetScene.instance?.GetPrefab("piece_artisanstation").GetComponent<CraftingStation>();
                    r.m_minStationLevel = 1;
                    r.m_resources = new Piece.Requirement[] {
                        FerdsItemFactory.Req("FreezeGland",50,0),
                        FerdsItemFactory.Req("Spirit_Crystal_Ygg",30,0),
                        FerdsItemFactory.Req("TrophyDragonQueen",1,0),
                        FerdsItemFactory.Req("TrophySGolem",1,0)
                    };
                    ObjectDB.instance.m_recipes.Add(r);
                }
            } else LogS.LogError($"[{PluginName}] Prefab 'Dragon_Essence_Ygg' not found");
            // Dragons
            try {
                if (override_yggdras_config){
                    // ICE
                    TweakAttack("iceball_attack_Ygg", frost: 110, chop: 75, pick: 75, blunt: 33, se: "Frost", seChance: .5f);
                    TweakAttack("iceball_attack_Ygg2", frost: 165, chop: 100, pick: 100, blunt: 48, se: "Frost", seChance: .5f);
                    TweakAttack("Atk_stompmoder_ygg", frost: 297, chop: 150, pick: 150, blunt: 83, forceVal: 50f, se: "Frost", seChance: 1.0f);
                    TweakAttack("Atk_Ice_Spit_MD_Ygg", frost: 385, chop: 200, pick: 200, blunt: 110, forceVal: 50f, se: "Frost", seChance: 1.0f);
                    TweakAttack("Atk_Ice_Spit_MD_Elder_Ygg", frost: 495, chop: 200, pick: 200, blunt: 125, forceVal: 50f, se: "Frost", seChance: 1.0f);
                    // FIRE
                    TweakAttack("Dred_Attack1", fire: 83, chop: 75, pick: 75, blunt: 33, forceVal: 10f, se: "Burning", seChance: .5f);
                    TweakAttack("Dred_Attack2", fire: 220, chop: 100, pick: 100, blunt: 55, forceVal: 25f, se: "Burning", seChance: .5f);
                    TweakAttack("Atk_Dsmaug_Spit_Ygg2", fire: 330, chop: 200, pick: 200, blunt: 220, forceVal: 50f, se: "Burning", seChance: .75f);
                    TweakAttack("Atk_Dsmaug_Spit_Ygg3", fire: 330, chop: 200, pick: 200, blunt: 220, forceVal: 50f, se: "Burning", seChance: .75f);
                    TweakAttack("DragonFBreathe_Ygg", fire: 880, chop: 250, pick: 275, se: "Burning", seChance: 1.0f);
                    // LIGHTNING
                    TweakAttack("Atk_DTJ_Spit_Ygg", fire: 550, chop: 200, pick: 200, forceVal: 30f, seByName: "SE_TJBurnShock", seChance: 1.0f);
                    TweakAttack("DTJ_Punch_Ygg1", fire: 330, chop: 200, pick: 200, blunt: 220, forceVal: 100f, seByName: "SE_TJBurnShock", seChance: .6f);
                    TweakAttack("DTJ_Punch_Ygg2", fire: 330, chop: 200, pick: 200, blunt: 220, forceVal: 100f, seByName: "SE_TJBurnShock", seChance: .6f);
                }
            } catch (Exception ex) { LogS.LogError($"[{PluginName}] Error tweaking attacks: {ex.Message}"); }
            TweakDragons();
        }
        private static void TweakDragons() {
            void SetResistances(Character dragon, string dragontype = "None") {
                var dragontypes = new[] { "Fire", "Ice", "Lightning" };
                if (dragon == null) return;
                var modifs = new HitData.DamageModifiers();
                // Elemental
                modifs.m_fire = HitData.DamageModifier.Normal;
                modifs.m_frost = HitData.DamageModifier.Normal;
                modifs.m_spirit = HitData.DamageModifier.Immune;
                modifs.m_poison = HitData.DamageModifier.Normal;
                modifs.m_lightning = HitData.DamageModifier.Normal;
                // Physical
                modifs.m_slash = HitData.DamageModifier.Normal;
                modifs.m_blunt = HitData.DamageModifier.Normal;
                modifs.m_pierce = HitData.DamageModifier.Normal;
                // Misc
                modifs.m_chop = HitData.DamageModifier.Ignore;
                modifs.m_pickaxe = HitData.DamageModifier.Ignore;
                if (dragontypes.Contains(dragontype)) {
                    // For growth dragons to be physically sturdy
                    modifs.m_slash = HitData.DamageModifier.SlightlyResistant;
                    modifs.m_blunt = HitData.DamageModifier.SlightlyResistant;
                    modifs.m_pierce = HitData.DamageModifier.Resistant;
                }
                switch (dragontype) {
                    // Elemental resistances depending on type
                    case "Fire":
                        modifs.m_fire = HitData.DamageModifier.VeryResistant;
                        modifs.m_frost = HitData.DamageModifier.SlightlyResistant;
                        break;
                    case "Ice":
                        modifs.m_frost = HitData.DamageModifier.VeryResistant;
                        break;
                    case "Lightning":
                        modifs.m_fire = HitData.DamageModifier.SlightlyResistant;
                        modifs.m_frost = HitData.DamageModifier.SlightlyResistant;
                        modifs.m_lightning = HitData.DamageModifier.VeryResistant;
                        break;
                }
                dragon.m_damageModifiers = modifs;
            }
            void SetHPtpl(string prefab, float hp) {
                var go = ZNetScene.instance.GetPrefab(prefab);
                if (go && go.TryGetComponent<Character>(out var ch))
                {
                    ch.m_health = hp;
                    if (prefab == "DModer_Ygg3" || prefab == "DModer_Ygg4_Elder"){
                        SetResistances(ch, "Ice");
                    }  else if (prefab == "dragon_ygg3_fire" || prefab == "dragon_ygg4_fire_Elder"){
                        SetResistances(ch, "Fire");
                    }  else if (prefab == "dragon_ygg3_blue" || prefab == "dragon_ygg4_blue_Elder"){
                        SetResistances(ch, "Lightning");
                    } else SetResistances(ch);
                    // Log
                    //string dragon_name;
                    //switch (prefab)
                    //{
                    //    case "DModer_Ygg3":
                    //        dragon_name = "Adult Ice Dragon";
                    //        break;
                    //    case "DModer_Ygg4_Elder":
                    //        dragon_name = "Elder Ice Dragon";
                    //        break;
                    //    case "dragon_ygg3_fire":
                    //        dragon_name = "Adult Fire Dragon";
                    //        break;
                    //    case "dragon_ygg4_fire_Elder":
                    //        dragon_name = "Elder Fire Dragon";
                    //        break;
                    //    case "dragon_ygg3_blue":
                    //        dragon_name = "Adult Lightning Dragon";
                    //        break;
                    //    case "dragon_ygg4_blue_Elder":
                    //        dragon_name = "Elder Lightning Dragon";
                    //        break;
                    //    default:
                    //        dragon_name = prefab;
                    //        break;
                    //}
                    //HitData.DamageModifiers modifs;
                    //modifs = ch.m_damageModifiers;
                    //var fields = typeof(HitData.DamageModifiers).GetFields();
                    //var modsText = string.Join("\n",
                    //    fields
                    //        .Where(f => f.GetValue(modifs) != null)
                    //        .Select(f => {
                    //            var name = f.Name.StartsWith("m_") ? f.Name.Substring(2) : f.Name;
                    //            if (name.Length > 0)
                    //                name = char.ToUpper(name[0]) + name.Substring(1);
                    //            return $" ➣ {name}: {f.GetValue(modifs)}";
                    //        })
                    //);
                    //LogS.LogInfo($"[{PluginName}][Dragon rebalance] Template set for: '{dragon_name}'\n ➣ Max health = {hp}\n{modsText}");
                }
                else LogS.LogError($"[{PluginName}] Prefab '{prefab}' not found");
            }
            SetHPtpl("DModer_Ygg2", 3000);
            SetHPtpl("DModer_Ygg3", 6000);
            SetHPtpl("DModer_Ygg4_Elder", 10000);
            SetHPtpl("dragon_ygg2_fire", 3000);
            SetHPtpl("dragon_ygg3_fire", 6000);
            SetHPtpl("dragon_ygg4_fire_Elder", 10000);
            SetHPtpl("dragon_ygg2_blue", 2500f);
            SetHPtpl("dragon_ygg3_blue", 6000);
            SetHPtpl("dragon_ygg4_blue_Elder", 10000);
        }
        /*
        ╔════════════════════════════════════╗
        ║ Status effects registration        ║
        ╚════════════════════════════════════╝
        */
        internal static void SetScaleWard() {
            if (ObjectDB.instance == null || ScaleWard_SE != null) return;
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "ScaleWard";
            se.m_name = "ScaleWard";
            se.m_tooltip = "Combine the three parts together for your cover to be as sturdy as a dragon's body.";
            se.m_mods = new List<HitData.DamageModPair> {
                new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant }
            };
            se.m_ttl = 0f;
            se.m_icon = ScaleWardIcon;
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            ScaleWard_SE = se;
        }
        internal static void SetDragonLeap() {
            if (ObjectDB.instance == null || DragonLeap_SE != null) return;
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "DragonLeap";
            se.m_name = "DragonLeap";
            se.m_tooltip = "Dragons soar the skies with majestic gracefulness. Gift thyself a fraction of that mightiness";
            se.m_jumpModifier = new UnityEngine.Vector3(0f, 0.5f, 1f);
            se.m_maxMaxFallSpeed = 7f;
            se.m_fallDamageModifier = -1f;
            se.m_ttl = 0f;
            se.m_icon = DragonLeapIcon;
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            DragonLeap_SE = se;
        }
        internal static void SetBeastLord() {
            if (ObjectDB.instance == null || BeastLord_SE != null) return;
            var se = ScriptableObject.CreateInstance <FerdsFireworksLab.BeastLord>();
            se.m_icon = BeastLordIcon;
            se.name = "BeastLord";
            se.m_name = "BeastLord";
            se.m_tooltip =  "While equipped, your mount regenerates stamina as if it were never hungry and heals slightly.";
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            BeastLord_SE = se;
        }
        internal static void SetBurnShock() {
            if (ObjectDB.instance == null || BurnShock_SE != null) return;
            var se = ScriptableObject.CreateInstance<FerdsFireworksLab.SE_TJBurnShock>();
            se.name = "SE_TJBurnShock";
            se.m_name = "BurnShock";
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            BurnShock_SE = se;
        }
        internal static void SetEldrFadmrExplosion()
        {
            if (ObjectDB.instance == null || FireTrinket_SE != null) return;
            var se = ScriptableObject.CreateInstance<FerdsFireworksLab.EldrFadmr_Explosion>();
            se.name = "EldrFadmr_Explosion";
            se.m_name = "EldrFadmr_Explosion";
            se.m_tooltip = "A flaming torrent urging to be unleashed";
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            FireTrinket_SE = se;
        }
        internal static void SetVetrFadmrExplosion()
        {
            if (ObjectDB.instance == null || IceTrinket_SE != null) return;
            var se = ScriptableObject.CreateInstance<FerdsFireworksLab.VetrFadmr_Explosion>();
            se.name = "VetrFadmr_Explosion";
            se.m_name = "VetrFadmr_Explosion";
            se.m_tooltip = "Let the cold sheer to their core";
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            IceTrinket_SE = se;
        }
        internal static void SetStormrFadmrExplosion()
        {
            if (ObjectDB.instance == null || LightningTrinket_SE != null) return;
            var se = ScriptableObject.CreateInstance<FerdsFireworksLab.StormrFadmr_Explosion>();
            se.name = "StormrFadmr_Explosion";
            se.m_name = "StormrFadmr_Explosion";
            se.m_tooltip = "Few have weathered the storm and lived to tell the tale";
            if (ObjectDB.instance.m_StatusEffects == null) ObjectDB.instance.m_StatusEffects = new List<StatusEffect>();
            ObjectDB.instance.m_StatusEffects.Add(se);
            LightningTrinket_SE = se;
        }
        /*
        ╔════════════════════════════════════╗
        ║ Helpers                            ║
        ╚════════════════════════════════════╝
        */
        internal static void LogInit(string module)
        {
            LogS.LogInfo($"Initializing {module}");

            if (!Instance)
            {
                string message = $"{module} was accessed before {PluginName} Awake, this can cause unexpected behaviour.";
                LogS.LogWarning($"{FerdsEpicEnhancementsPlugin.Metadata?.Metadata}\n{message}");
            }
        }
        public static AssetBundle LoadEmbeddedBundle(string hint)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
            string resName = manifestResourceNames.FirstOrDefault(n => n.EndsWith("." + hint, StringComparison.OrdinalIgnoreCase))
                ?? manifestResourceNames.FirstOrDefault(n => n.EndsWith("." + hint + ".bytes", StringComparison.OrdinalIgnoreCase))
                ?? manifestResourceNames.FirstOrDefault(n => n.EndsWith("." + hint + ".assetbundle", StringComparison.OrdinalIgnoreCase))
                ?? manifestResourceNames.FirstOrDefault(n => n.ToLowerInvariant().Contains("." + hint.ToLowerInvariant() + "."));
            if (resName == null) { LogS?.LogError($"[{PluginName}] Embedded bundle not found. Resources: " + string.Join(", ", manifestResourceNames)); return null; }
            try
            {
                using (Stream s = executingAssembly.GetManifestResourceStream(resName))
                {
                    if (s == null) { LogS?.LogError($"[{PluginName}] Resource stream null: {resName}"); return null; }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        var ab = AssetBundle.LoadFromMemory(ms.ToArray());
                        if (!ab) LogS?.LogError($"[{PluginName}] AssetBundle invalid in resource: {resName}");
                        else LogS?.LogInfo($"[{PluginName}] AssetBundle loaded: {resName}");
                        return ab;
                    }
                }
            }
            catch (Exception ex) { LogS?.LogError($"[{PluginName}] Exception loading asset bundle '{resName}': {ex}"); return null; }
        }
        private static GameObject GetRootObject()
        {
            if (rootObject)
            {
                return rootObject;
            }

            // create root container for GameObjects in the DontDestroyOnLoad scene
            rootObject = new GameObject("_FerdsRoot");
            DontDestroyOnLoad(rootObject);
            return rootObject;
        }
        private static int MaterialReplacer(GameObject prefab, Material mat, string tag) {
            if (!prefab)  return 0;
            int changed = 0, seen = 0;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true)){
                if (changed == 0)
                {
                    changed++;
                    continue; //only the mesh renders
                }
                seen++;
                var mats = r.sharedMaterials;
                //LogS.LogInfo($"[{PluginName}][MatReplace:PREFAB] material name = {r.name}");
                if (mats == null || mats.Length == 0) continue;
                bool alreadyAll = true;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != mat) { alreadyAll = false; break; }
                if (alreadyAll) continue;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                changed++;
            }
            /*if (changed > 0)LogS.LogInfo($"[{PluginName}][MatReplace:PREFAB] {tag} -> renderersTouched={changed} / seen={seen}.");
            else LogS.LogInfo($"[{PluginName}][MatReplace:PREFAB] {tag} already using target material (seen={seen}).");*/
            return changed;
        }
        private static void TweakAttack(string prefabName,
            float frost = 0, float fire = 0, float chop = 0, float pick = 0, float blunt = 0, float forceVal = 0,
            string se = null, float seChance = 0, string seByName = null) {
            var go = ObjectDB.instance.GetItemPrefab(prefabName);
            if (!go) { LogS.LogError($"[{PluginName}] Prefab '{prefabName}' not found"); return; } 
            var id = FerdsItemFactory.EnsureItemDrop(go);
            id.m_itemData.m_shared.m_damages = new HitData.DamageTypes {
                m_frost = frost,
                m_fire = fire,
                m_chop = chop,
                m_pickaxe = pick,
                m_blunt = blunt
            };
            id.m_itemData.m_shared.m_attackForce = forceVal; 
            StatusEffect status = null;
            if (!string.IsNullOrEmpty(se))
                status = ObjectDB.instance.GetStatusEffect(se.GetStableHashCode());
            else if (!string.IsNullOrEmpty(seByName))
                status = ObjectDB.instance.GetStatusEffect(seByName.GetStableHashCode()); 
            id.m_itemData.m_shared.m_attackStatusEffect = status;
            id.m_itemData.m_shared.m_attackStatusEffectChance = seChance;
        }
        public static void saddle_EnsureOn(Tameable tameable) {
            try {
                if (!tameable || !_assetBundle) return;
                var template = NewDragonSaddleCollider;
                if (!template) { LogS.LogWarning("[Saddle] Template null"); return; }
                var candidates = tameable.transform.GetComponentsInChildren<Transform>(true)
                                   .Where(t => string.Equals(t.name, "saddle_d_ygg", StringComparison.OrdinalIgnoreCase))
                                   .ToArray();
                if (candidates.Length == 0) return;
                var saddleRoot = candidates.OrderBy(t => saddle_GetDepth(t)).Last();
                var targetNode = saddleRoot;
                if (targetNode.Find("[Frd]SaddleCollider")) return;
                var merged = UnityEngine.Object.Instantiate(template, targetNode, false);
                merged.name = "[Frd]SaddleCollider";
                merged.layer = targetNode.gameObject.layer;
                merged.tag = targetNode.gameObject.tag;
                var fwd = merged.AddComponent<FerdEpicEnhancements.InteractForwarder>();
                fwd.SearchRoot = targetNode;
                LogS.LogInfo($"[Saddle] Merged collider under: {GetFullPath(targetNode)}");
                foreach (var c in saddleRoot.GetComponentsInChildren<Collider>(true)){
                    if (!c) continue;
                    if (c.transform.IsChildOf(merged.transform)) continue;
                    c.enabled = false;
                }
            } catch (Exception ex) { LogS.LogError($"[{PluginName}][Saddle] EnsureOn exception:\n{ex}"); }
        }
        private static int saddle_GetDepth(Transform t) { int d = 0; while (t && (t = t.parent) != null) d++; return d; }
        private static string GetFullPath(Transform t) { var p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }
        /*
        ╔════════════════════════════════════╗
        ║ Harmony patches                    ║
        ╚════════════════════════════════════╝
        */
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        static class ObjectDB_Awake_Patch
        {
            static void Postfix(ObjectDB __instance)
            {
                if (ZNetScene.instance != null && FerdsEpicEnhancementsPlugin.Instance != null)
                {
                    FerdsEpicEnhancementsPlugin.Instance.StartCoroutine(FerdsEpicEnhancementsPlugin.Instance.Orchestrator());
                }
            }
        }
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        static class ZNetScene_Awake_Patch
        {
            static void Postfix(ZNetScene __instance)
            {
                if (ObjectDB.instance != null && FerdsEpicEnhancementsPlugin.Instance != null)
                {
                    FerdsEpicEnhancementsPlugin.Instance.StartCoroutine(FerdsEpicEnhancementsPlugin.Instance.Orchestrator());
                }
            }
        }
        [HarmonyPatch]
        static class ObjectDB_Init_Patch {
            static MethodBase TargetMethod() {
                return AccessTools.Method(typeof(ObjectDB), "Awake")
                    ?? AccessTools.Method(typeof(ObjectDB), "Start")
                    ?? AccessTools.Method(typeof(ObjectDB), "Init")
                    ?? AccessTools.Method(typeof(ObjectDB), "Initialize");
            } 
        }
        [HarmonyPatch(typeof(InventoryGui), "Show")]
        static class Patch_InventoryGui_Show {
            [HarmonyPostfix, HarmonyPriority(HarmonyLib.Priority.Last)]
            private static void Postfix() {
                if (ObjectDB.instance != null) {
                    var mi = typeof(InventoryGui).GetMethod("UpdateCraftingPanel",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null && InventoryGui.instance != null) { try { mi.Invoke(InventoryGui.instance, null); } catch {} }
                }
            }
        }
        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetChainTooltip")]
        static class ItemData_GetChainTooltip_Patch
        {
            static bool Prefix(ItemDrop.ItemData __instance, ref string __result, int quality, float skillLevel)
            {
                var shared = __instance?.m_shared;
                if (shared == null) { __result = ""; return false; }
                var t = shared.m_itemType;
                if (t.ToString().Equals("Utility", StringComparison.OrdinalIgnoreCase) ||
                    t.ToString().Equals("Trinket", StringComparison.OrdinalIgnoreCase))
                { __result = ""; return false; }
                return true;
            }
        }
        [HarmonyPatch(typeof(Tameable), "SetSaddle")]
        internal static class Patch_Tameable_SetSaddle
        {
            static void Postfix(Tameable __instance, bool enabled)
            {
                if (enabled) saddle_EnsureOn(__instance);
            }
        }
        [HarmonyPatch(typeof(Tameable), "RPC_SetSaddle")]
        internal static class Patch_Tameable_RPC_SetSaddle_Alias
        {
            static void Postfix(Tameable __instance, long sender, [HarmonyArgument("enabled")] bool saddled)
            {
                if (saddled) saddle_EnsureOn(__instance);
            }
        }
        [HarmonyPatch(typeof(ZNetScene), "RemoveObjects")]
        public static class Patch_ZNetScene_RemoveObjects
        {
            static void Prefix(List<GameObject> currentNearObjects, List<GameObject> currentDistantObjects)
            {
                if (currentNearObjects != null)
                    currentNearObjects.RemoveAll(obj => obj == null);
                if (currentDistantObjects != null)
                    currentDistantObjects.RemoveAll(obj => obj == null);
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
        public static class Patch_ZNetScene_CreateDestroyObjects
        {
            static void Prefix(ZNetScene __instance)
            {
                var fieldNear = AccessTools.Field(typeof(ZNetScene), "m_tempCurrentObjects");
                var fieldDistant = AccessTools.Field(typeof(ZNetScene), "m_tempCurrentDistantObjects");
                if (fieldNear != null)
                {
                    var list = fieldNear.GetValue(__instance) as List<GameObject>;
                    if (list != null)
                        list.RemoveAll(obj => obj == null);
                }
                if (fieldDistant != null)
                {
                    var list = fieldDistant.GetValue(__instance) as List<GameObject>;
                    if (list != null)
                        list.RemoveAll(obj => obj == null);
                }
            }
        }
        [HarmonyPatch(typeof(Recipe), "GetRequiredStationLevel")]
        public static class Patch_Recipe_GetRequiredStationLevel
        {
            // Lista de nombres de prefabs que quieres forzar el cálculo
            private static readonly HashSet<string> TargetPrefabs = new HashSet<string>
            {
                "DragonChest_Ygg",
                "DragonLeggings_Ygg",
                "DragonCrown_Ygg",
                "DragonShield_Ygg",
                "HelmetModer_Ygg",
                "Dragon_Cape_Ygg"
            };

            [HarmonyPostfix]
            public static void Postfix(Recipe __instance, int quality, ref int __result)
            {
                if (__instance.m_item != null)
                {
                    // Elimina "(Clone)" si existe y compara el nombre limpio
                    string cleanName = __instance.m_item.name.Replace("(Clone)", "").Trim();
                    if (TargetPrefabs.Contains(cleanName))
                    {
                        __result = __instance.m_minStationLevel + quality - 1;
                    }
                }
            }
        }
        [HarmonyPatch(typeof(ZNetScene), "Update")]
        public static class Patch_ZNetScene_Update
        {
            static void Prefix(ZNetScene __instance)
            {
                var fieldNear = AccessTools.Field(typeof(ZNetScene), "m_tempCurrentObjects");
                var fieldDistant = AccessTools.Field(typeof(ZNetScene), "m_tempCurrentDistantObjects");
                if (fieldNear != null)
                {
                    var list = fieldNear.GetValue(__instance) as List<GameObject>;
                    if (list != null)
                        list.RemoveAll(obj => obj == null);
                }
                if (fieldDistant != null)
                {
                    var list = fieldDistant.GetValue(__instance) as List<GameObject>;
                    if (list != null)
                        list.RemoveAll(obj => obj == null);
                }
            }
        }
    }
}
