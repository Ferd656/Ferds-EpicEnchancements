// File: 02 - FerdsItemFactory.cs
// Target: .NET Framework 4.7.2
using FerdEpicEnhancements.JotunnEntities;
using FerdEpicEnhancements.JotunnManagers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FerdEpicEnhancements
{
    internal class FerdsItemFactory {
        public static void ForgeTrinkets()
        {
            BuildTrinket(
                trinket_obj: FerdsEpicEnhancementsPlugin.TrinketIceDragonPrefab,
                worldPrefabName: "TrinketIceDragon_Frd",
                displayName: "Vetr Fadmr",
                description: "Remains from a heir of Moder are used to craft this relic. Cold wind coils around this frost-infused artifact",
                trinketIcon: FerdsEpicEnhancementsPlugin.TrinketIceDragonPrefab_icon,
                recipeName: "Recipe_TrinketIceDragon_Frd",
                reqs: new Piece.Requirement[] { Req("SilverNecklace", 1, 0), Req("Degg_Moder_Ygg", 1, 0), Req("YmirRemains", 10, 0), Req("GemstoneGreen", 3, 0) },
                trinket_se: FerdsEpicEnhancementsPlugin.IceTrinket_SE
            );
            BuildTrinket(
                trinket_obj: FerdsEpicEnhancementsPlugin.TrinketFireDragonPrefab,
                worldPrefabName: "TrinketFireDragon_Frd",
                displayName: "Eldr Fadmr",
                description: "Remains from a heir of Smaug are used to craft this relic. Warm to the touch, with everlasting embers",
                trinketIcon: FerdsEpicEnhancementsPlugin.TrinketFireDragonPrefab_icon,
                recipeName: "Recipe_TrinketFireDragon_Frd",
                reqs: new Piece.Requirement[] { Req("SilverNecklace", 1, 0), Req("Degg_Fire_Ygg", 1, 0), Req("DragonScale_Ygg", 1, 0), Req("GemstoneRed", 3, 0) },
                trinket_se: FerdsEpicEnhancementsPlugin.FireTrinket_SE
            );
            BuildTrinket(
                trinket_obj: FerdsEpicEnhancementsPlugin.TrinketLightningDragonPrefab,
                worldPrefabName: "TrinketLightningDragon_Frd",
                displayName: "Stormr Fadmr",
                description: "Remains from a heir of Grimthor are used to craft this relic. Perpetually crackles with stored stormlight",
                trinketIcon: FerdsEpicEnhancementsPlugin.TrinketLightningDragonPrefab_icon,
                recipeName: "Recipe_TrinketLightningDragon_Frd",
                reqs: new Piece.Requirement[] { Req("SilverNecklace", 1, 0), Req("Degg_Blue_Ygg", 1, 0), Req("DragonScale_Ygg", 1, 0), Req("GemstoneBlue", 2, 0) },
                trinket_se: FerdsEpicEnhancementsPlugin.LightningTrinket_SE
            );
            // ItemManager.RegisterCustomData(ObjectDB.instance)
            var itemManagerType = typeof(FerdEpicEnhancements.JotunnManagers.ItemManager);
            var registerCustomDataMethod = itemManagerType.GetMethod("RegisterCustomData", BindingFlags.Instance | BindingFlags.NonPublic);
            var itemManagerInstance = itemManagerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);

            if (registerCustomDataMethod != null && itemManagerInstance != null && ObjectDB.instance != null)
            {
                registerCustomDataMethod.Invoke(itemManagerInstance, new object[] { ObjectDB.instance });
            }
            else
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] Couldn't call ItemManager.RegisterCustomData(ObjectDB.instance).");
            }
            void BuildTrinket(
                GameObject trinket_obj,
                string worldPrefabName,
                string displayName,
                string description,
                Sprite trinketIcon,
                string recipeName,
                Piece.Requirement[] reqs,
                StatusEffect trinket_se = null)
            {
                if (!ObjectDB.instance || !ZNetScene.instance || !trinket_obj)
                {
                    if (!ObjectDB.instance) FerdsEpicEnhancementsPlugin.LogS.LogError("ObjectDB is NULL before trinket registration!");
                    if (!ZNetScene.instance) FerdsEpicEnhancementsPlugin.LogS.LogError("ZNetScene is NULL before trinket registration!");
                    if (!trinket_obj) FerdsEpicEnhancementsPlugin.LogS.LogError($"{worldPrefabName} is NULL before registration!");
                    return;
                }
                if (trinket_se == null) trinket_se = ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectLightning);
                try
                {
                    trinket_obj.name = worldPrefabName;
                    if (!trinket_obj.GetComponent<ZNetView>()) trinket_obj.AddComponent<ZNetView>();
                    if (!trinket_obj.GetComponent<ZSyncTransform>()) trinket_obj.AddComponent<ZSyncTransform>();
                    ZNetView component = trinket_obj.GetComponent<ZNetView>();
                    component.m_persistent = true;
                    component.m_distant = false;
                    component.m_type = ZDO.ObjectType.Default;
                    component.m_syncInitialScale = false;
                    ZSyncTransform component2 = trinket_obj.GetComponent<ZSyncTransform>();
                    component2.m_syncPosition = true;
                    component2.m_syncRotation = true;
                    component2.m_syncScale = false;
                    component2.m_syncBodyVelocity = false;
                    component2.m_characterParentSync = false;
                    if (!trinket_obj.GetComponent<Collider>()) { var c = trinket_obj.AddComponent<BoxCollider>(); c.isTrigger = false; }
                    ItemDrop trinket_idp = EnsureItemDrop(trinket_obj);
                    if (trinket_idp.m_itemData.m_shared == null)
                        trinket_idp.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
                    // Displays
                    trinket_idp.m_itemData.m_shared.m_name = displayName;
                    trinket_idp.m_itemData.m_dropPrefab = trinket_obj;
                    trinket_idp.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Trinket;
                    trinket_idp.m_itemData.m_shared.m_description = description;
                    trinket_idp.m_itemData.m_shared.m_icons = new[] { trinketIcon };
                    // Hardcoded properties
                    trinket_idp.m_itemData.m_shared.m_weight = 0.3f;
                    trinket_idp.m_itemData.m_shared.m_maxStackSize = 1;
                    trinket_idp.m_itemData.m_shared.m_maxAdrenaline = 60;
                    if (trinket_se == null) trinket_se = ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectLightning);
                    trinket_idp.m_itemData.m_shared.m_fullAdrenalineSE = trinket_se;
                    trinket_idp.m_itemData.m_dropPrefab = trinket_obj;
                    // Recipe
                    RemoveExistingRecipe(trinket_idp, recipeName: recipeName);
                    var trinket_recipe = ScriptableObject.CreateInstance<Recipe>();
                    trinket_recipe.name = recipeName;
                    trinket_recipe.m_item = trinket_idp;
                    trinket_recipe.m_craftingStation = ZNetScene.instance?.GetPrefab("piece_artisanstation").GetComponent<CraftingStation>();
                    trinket_recipe.m_minStationLevel = 2;
                    trinket_recipe.m_enabled = true;
                    trinket_recipe.m_resources = reqs;
                    // Register item in game using Jotunn's process
                    var world = PrefabManager.Instance.GetPrefab(worldPrefabName) ?? ZNetScene.instance.GetPrefab(worldPrefabName);
                    var ci = new CustomItem(world, fixReference: true);
                    ItemManager.Instance.AddItem(ci);
                    var cr = new CustomRecipe(trinket_recipe, fixReference: true, fixRequirementReferences: true);
                    ItemManager.Instance.AddRecipe(cr);
                }
                catch (Exception ex) { FerdsEpicEnhancementsPlugin.LogS?.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] Failed building trinket: '{displayName}'({worldPrefabName}) \nReason:\n{ex}"); }
            }
        }
        internal static ItemDrop EnsureItemDrop(GameObject go)
        {
            var drop = go.GetComponent<ItemDrop>() ?? go.AddComponent<ItemDrop>();
            if (drop.m_itemData == null) drop.m_itemData = new ItemDrop.ItemData();
            if (drop.m_itemData.m_shared == null) drop.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
            return drop;
        }
        public static Piece.Requirement Req(string name, int amt, int perLevel)
            => new Piece.Requirement { m_resItem = ResolveItemDrop(name), m_amount = amt, m_amountPerLevel = perLevel };
        internal static void RemoveExistingRecipe(ItemDrop item, string recipeName = null, CraftingStation station = null)
        {
            ObjectDB.instance.m_recipes.RemoveAll(r => r && ((!string.IsNullOrEmpty(recipeName) && r.name == recipeName) ||
                    (r.m_item == item && (station == null || r.m_craftingStation == station)))
            );
            ObjectDB.instance.m_recipes.Remove(ObjectDB.instance.m_recipes.FirstOrDefault(r => r && r.m_item == item));
        }
        internal static ItemDrop ResolveItemDrop(string prefabName)
        {
            GameObject go = new GameObject();
            try { go = ObjectDB.instance.GetItemPrefab(prefabName); } catch { }
            if (!go) { try { go = ZNetScene.instance?.GetPrefab(prefabName); } catch { } }
            return go ? go.GetComponent<ItemDrop>() : null;
        }
    }
    [HarmonyPatch]
    public static class Player_Equip_Patches
    {
        private static readonly Dictionary<Player, GameObject> Spawned = new Dictionary<Player, GameObject>();
        private static bool s_targetIsPlayer = false;

        static MethodBase TargetMethod()
        {
            var ve = typeof(VisEquipment);
            var m = AccessTools.DeclaredMethod(ve, "UpdateEquipmentVisuals", Type.EmptyTypes)
                 ?? AccessTools.Method(ve, "UpdateEquipmentVisuals", Type.EmptyTypes);
            if (m != null) { s_targetIsPlayer = false; return m; }

            var pl = typeof(Player);
            m = AccessTools.DeclaredMethod(pl, "SetupEquipment", Type.EmptyTypes)
              ?? AccessTools.Method(pl, "SetupEquipment", Type.EmptyTypes);
            if (m != null) { s_targetIsPlayer = true; return m; }

            m = AccessTools.Method(pl, "UpdateEquipment", Type.EmptyTypes);
            if (m != null) { s_targetIsPlayer = true; return m; }

            throw new MissingMethodException($"[{FerdsEpicEnhancementsPlugin.PluginName}] Could not locate a suitable equipment-visuals method.");
        }
        static void Postfix(object __instance)
        {
            try
            {
                Player player = null;
                GameObject prefab = null;
                GameObject equipped_prefab = null;
                var vis = __instance as VisEquipment;
                var allowedNames = new HashSet<string>
                {
                    "TrinketFireDragon_Frd",
                    "TrinketLightningDragon_Frd",
                    "TrinketIceDragon_Frd"
                };
                if (s_targetIsPlayer) player = __instance as Player;
                else if (vis != null) player = vis.GetComponentInParent<Player>();

                if (player == null || !FerdsEpicEnhancementsPlugin._wasDragonRidersLoaded)
                    return;

                var prefab_name = GetEquippedTrinketPrefabName(player);
                if (string.IsNullOrEmpty(prefab_name))
                {
                    RemoveVisual(player, reason: "no-our-item-equipped");
                    return;
                }

                prefab = ZNetScene.instance?.GetPrefab(prefab_name);
                if (prefab == null)
                {
                    RemoveVisual(player, reason: "no-visual-def");
                    return;
                }

                if (prefab_name == "TrinketFireDragon_Frd")
                    equipped_prefab = FerdsEpicEnhancementsPlugin.TrinketFireDragonPrefab_equipped;
                else if (prefab_name == "TrinketLightningDragon_Frd")
                    equipped_prefab = FerdsEpicEnhancementsPlugin.TrinketLightningDragonPrefab_equipped;
                else if (prefab_name == "TrinketIceDragon_Frd")
                    equipped_prefab = FerdsEpicEnhancementsPlugin.TrinketIceDragonPrefab_equipped;
                else
                    equipped_prefab = ZNetScene.instance?.GetPrefab(prefab_name);

                if (equipped_prefab == null)
                {
                    RemoveVisual(player, reason: "no-equipped-prefab");
                    return;
                }

                if (Spawned.TryGetValue(player, out var existing) && existing)
                {
                    if (!existing.name.Equals(equipped_prefab.name, System.StringComparison.Ordinal))
                    {
                        RemoveVisual(player, reason: "switching-visual");
                        if (allowedNames.Contains(prefab_name))
                        {
                            var inst = UnityEngine.Object.Instantiate(equipped_prefab);
                            inst.name = equipped_prefab.name;
                            SafePrepareVisualInstance(inst);
                            AttachToNeck(player, inst.transform);
                            TryRebindAllSkinnedMeshesToPlayer(player, inst.transform);
                            Spawned[player] = inst;
                        }
                    }
                    else if (!existing.transform.parent)
                    {
                        AttachToNeck(player, existing.transform);
                        TryRebindAllSkinnedMeshesToPlayer(player, existing.transform);
                    }
                }
                else if (allowedNames.Contains(prefab_name))
                {
                    var inst = UnityEngine.Object.Instantiate(equipped_prefab);
                    inst.name = equipped_prefab.name;
                    SafePrepareVisualInstance(inst);
                    AttachToNeck(player, inst.transform);
                    TryRebindAllSkinnedMeshesToPlayer(player, inst.transform);
                    Spawned[player] = inst;
                }
            }
            catch (System.Exception ex)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}][EQUIP] Exception: {ex}");
            }
        }
        private static string GetEquippedTrinketPrefabName(Player p)
        {
            if (p == null) return "";
            foreach (var it in GetEquippedItemsCompat(p))
            {
                if (it?.m_shared?.m_itemType == ItemDrop.ItemData.ItemType.Trinket)
                    return it.m_dropPrefab?.name;
            }
            return "";
        }
        private static IEnumerable<ItemDrop.ItemData> GetEquippedItemsCompat(Player p)
        {
            if (!p) yield break;
            var inv = p.GetInventory();
            if (inv == null) yield break;
            foreach (var it in inv.GetAllItems())
                if (it != null && it.m_equipped) yield return it;
        }
        private static void AttachToNeck(Player player, Transform visual)
        {
            var attachPoint = player.transform.Find("Visual/Armature/Hips/Spine/Spine1/Spine2");
            if (attachPoint == null) attachPoint = player.transform;
            visual.SetParent(attachPoint, false);
            visual.localPosition = new UnityEngine.Vector3(0f, -0.021f, -0.00258f);
            visual.localEulerAngles = new UnityEngine.Vector3(15f, 0, 0);
            visual.localScale = new UnityEngine.Vector3(0.013f, 0.013f, 0.00995f);
        }
        private static void TryRebindAllSkinnedMeshesToPlayer(Player p, Transform visualRoot)
        {
            var armature = p.transform.Find("Visual/Armature");
            if (!armature)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"[{FerdsEpicEnhancementsPlugin.PluginName}][EQUIP] Player armature not found; cannot rebind skinned meshes.");
                return;
            } 
            var boneMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in armature.GetComponentsInChildren<Transform>(true))
                boneMap[t.name] = t;
            foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.bones == null || smr.bones.Length == 0)
                {
                    FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}][REBIND] {smr.name}: no bones listed; skipping.");
                    continue;
                }
                int matched = 0;
                var newBones = new Transform[smr.bones.Length];
                for (int i = 0; i < smr.bones.Length; i++)
                {
                    var b = smr.bones[i];
                    if (b && boneMap.TryGetValue(b.name, out var repl))
                    {
                        newBones[i] = repl; matched++;
                    }
                }
                if (matched > 0)
                {
                    var rootName = smr.rootBone ? smr.rootBone.name : "Hips";
                    if (!boneMap.TryGetValue(rootName, out var newRoot))
                        boneMap.TryGetValue("Hips", out newRoot);

                    smr.rootBone = newRoot ? newRoot : armature;
                    smr.bones = newBones;
                    smr.updateWhenOffscreen = true;
                    FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}][REBIND] {smr.name}: {matched}/{smr.bones.Length} bones matched; root={(smr.rootBone ? smr.rootBone.name : "(null)")}");
                }
                else
                {
                    FerdsEpicEnhancementsPlugin.LogS.LogWarning($"[{FerdsEpicEnhancementsPlugin.PluginName}][REBIND] {smr.name}: no bone names matched player armature. Model may be invisible.");
                }
            }
        }
        private static void SafePrepareVisualInstance(GameObject inst)
        {
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.Destroy(rb);
            foreach (var col in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(col);

            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!m) continue;
                    if (!m.shader) m.shader = Shader.Find("Standard") ?? m.shader;
                    if (m.HasProperty("_Color")) { var c = m.color; c.a = 1f; m.color = c; }
                    if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 0f);
                    if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
                }
            }
        }
        private static void RemoveVisual(Player player, string reason = null)
        {
            if (Spawned.TryGetValue(player, out var go) && go)
            {
                UnityEngine.Object.Destroy(go);
                Spawned.Remove(player);
                //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}][EQUIP] Removed visual for '{player.name}' ({reason})");
            }
        }
    }
    /*
    ╔════════════════════════════════════╗
    ║ Harmony patches                    ║
    ╚════════════════════════════════════╝
    */
    [HarmonyPatch]
    public static class Patch_Saddle_UpdateStamina_BeastLord
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method("Sadle:UpdateStamina");
        }
        private static float? originalStaminaRegenHungry = null;
        private static readonly Dictionary<object, float> healTimers = new Dictionary<object, float>();
        static void Prefix(object __instance, ref float dt)
        {
            var m_character = AccessTools.Field(__instance.GetType(), "m_character").GetValue(__instance) as Character;
            if ( m_character == null) return;
            var sadleInstance = __instance;
            // Get rider ID
            var getUserMethod = AccessTools.Method(sadleInstance.GetType(), "GetUser");
            long riderZDOID = 0;
            if (getUserMethod != null)
            {
                riderZDOID = (long)getUserMethod.Invoke(sadleInstance, null);
            }
            // Get rider
            Player rider = Player.GetAllPlayers().FirstOrDefault(p => p.GetZDOID().UserID == riderZDOID);
            // Rider has beastlord?
            bool hasBeastLord = false;
            if (rider != null)
            {
                hasBeastLord = rider.GetSEMan()?.GetStatusEffects().Any(se => se.name == "BeastLord") == true;
            }
            //staminaRegen regen fields
            var staminaRegenField = AccessTools.Field(__instance.GetType(), "m_staminaRegen");
            var staminaRegenHungryField = AccessTools.Field(__instance.GetType(), "m_staminaRegenHungry");
            float staminaRegen = (float)staminaRegenField.GetValue(__instance);
            if (originalStaminaRegenHungry == null)
                originalStaminaRegenHungry = (float)staminaRegenHungryField.GetValue(__instance);
            if (hasBeastLord)
            {
                // Ignore hunger
                staminaRegenHungryField.SetValue(__instance, staminaRegen);
                // Heal
                float timer = healTimers.ContainsKey(__instance) ? healTimers[__instance] : 0f;
                timer += dt;
                if (timer >= 1f)
                {
                    float maxHealth = m_character.GetMaxHealth();
                    float healAmount = Mathf.Ceil(maxHealth * 0.0008f);
                    if (healAmount > 0f)
                        m_character.Heal(healAmount, false);
                    timer = 0f;
                }
                healTimers[__instance] = timer;
            }
            else
            {
                // Back to normal
                if (originalStaminaRegenHungry != null)
                    staminaRegenHungryField.SetValue(__instance, originalStaminaRegenHungry.Value);
            }
        }
    }
}

