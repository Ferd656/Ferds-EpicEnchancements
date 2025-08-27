using System;
using BepInEx;
using HarmonyLib;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;
using System.Reflection;
using System.Collections;
using static CharacterDrop;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FerdItemsUp
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("Yggdrah.DragonRiders", BepInDependency.DependencyFlags.HardDependency)]
    public class FerdItemsUp : BaseUnityPlugin
    {
        public const string PluginGuid = "ferditemsup";
        public const string PluginName = "FerdItemsUp";
        public const string PluginVersion = "1.0.0";

        private static ManualLogSource LogS;
        private Harmony _harmony;

        internal static FerdItemsUp Instance;
        private static bool _appliedOnce = false;

        private static readonly string[] TargetPrefabNames =
        {
            "Dragon_Cape_Ygg",
            "DragonChest_Ygg",
            "DragonLeggings_Ygg",
            "DragonCrown_Ygg",
            "DragonShield_Ygg",
            "HelmetModer_Ygg"
        };

        private static readonly Dictionary<string, (string res, int amt)[]> CraftCosts =
            new Dictionary<string, (string, int)[]>
            {
                ["DragonChest_Ygg"] = new[]
                {
                    ("ArmorRootChest",      1),
                    ("ArmorCarapaceChest",  1),
                    ("DragonScale_Ygg",     4),
                    ("Electrum_Bar_Ygg",    5)
                },
                ["DragonLeggings_Ygg"] = new[]
                {
                    ("ArmorRootLegs",       1),
                    ("ArmorCarapaceLegs",   1),
                    ("DragonScale_Ygg",     4),
                    ("Electrum_Bar_Ygg",    5)
                },
                ["DragonCrown_Ygg"] = new[]
                {
                    ("FlametalNew",        25),
                    ("DragonScale_Ygg",     3),
                    ("Electrum_Bar_Ygg",    5),
                    ("DragonTear",         20)
                },
                ["HelmetModer_Ygg"] = new[]
                {
                    ("HelmetDrake",         1),
                    ("TrophyDragonQueen",   1),
                    ("DragonScale_Ygg",     2),
                    ("DragonTear",         20)
                },
                ["DragonShield_Ygg"] = new[]
                {
                    ("ShieldSerpentscale",  1),
                    ("DragonScale_Ygg",     3),
                    ("Electrum_Bar_Ygg",    5),
                    ("DragonTear",         20)
                },
                ["Dragon_Cape_Ygg"] = new[]
                {
                    ("CapeFeather",        1),
                    ("CapeAsh",            1),
                    ("DragonScale_Ygg",    2),
                    ("DragonTear",         20)
                }
            };

        private static readonly Dictionary<string, (string res, int amt)[]> UpgradeCosts =
            new Dictionary<string, (string, int)[]>
            {
                ["DragonChest_Ygg"] = new[]
                {
                    ("DragonScale_Ygg",   1),
                    ("Electrum_Bar_Ygg",  5),
                    ("Eitr",             15),
                    ("DragonTear",        5)
                },
                ["DragonLeggings_Ygg"] = new[]
                {
                    ("DragonScale_Ygg",   1),
                    ("Electrum_Bar_Ygg",  5),
                    ("Eitr",             15),
                    ("DragonTear",        5)
                },
                ["DragonCrown_Ygg"] = new[]
                {
                    ("FlametalNew",       5),
                    ("Electrum_Bar_Ygg",  5),
                    ("DragonTear",       10)
                },
                ["HelmetModer_Ygg"] = new[]
                {
                    ("TrophyHatchling",   5),
                    ("DragonScale_Ygg",   1),
                    ("Obsidian",         20),
                    ("DragonTear",        5)
                },
                ["DragonShield_Ygg"] = new[]
                {
                    ("DragonScale_Ygg",   1),
                    ("Silver",            5),
                    ("DragonTear",        5)
                },
                ["Dragon_Cape_Ygg"] = new[]
                {
                    ("DragonScale_Ygg",   1),
                    ("Tar",              20),
                    ("DragonTear",        5)
                }
            };

        private void Awake()
        {
            Instance = this;
            LogS = Logger;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Patch_InventoryGui_Show)); // UI refresh only

            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            StartCoroutine(WaitAndPatch()); // one-shot after world ready
            LogS.LogInfo($"{PluginName} {PluginVersion} loaded; will apply upgrade rules once after world is ready.");
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            try { _harmony?.UnpatchSelf(); } catch { /* ignore */ }
        }

        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            if (!_appliedOnce)
                StartCoroutine(WaitAndPatch());
        }

        private static bool IsDragonRidersLoaded()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Any(a =>
                {
                    var n = a.GetName().Name;
                    return n.IndexOf("DragonRaiders", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("DragonRiders", StringComparison.OrdinalIgnoreCase) >= 0;
                });
        }

        private IEnumerator WaitAndPatch()
        {
            if (_appliedOnce) yield break;

            // World subsystems
            while (ZNetScene.instance == null) yield return null;
            while (ObjectDB.instance == null) yield return null;

            // Wait for DragonRiders (up to ~10s)
            const int maxFrames = 10 * 60;
            int frames = 0;
            while (!IsDragonRidersLoaded() && frames < maxFrames) { frames++; yield return null; }

            if (!IsDragonRidersLoaded())
                LogS.LogWarning("DragonRiders did not appear to load within the expected time. Continuing anyway.");
            else
                LogS.LogInfo("Detected DragonRiders assembly. Continuing…");

            // ObjectDB populated?
            while (ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0) yield return null;

            // IMPORTANT: reset frame counter before waiting for target prefabs (bugfix)
            frames = 0;
            while (!AllTargetsPresent() && frames < maxFrames) { frames++; yield return null; }

            // Give late loaders a few frames
            for (int i = 0; i < 10; i++) yield return null;

            // Prevents duplication. Rmove any pre-existing recipes for our targets before applying ours.

            if (!_appliedOnce)
            {
                int removed = RemoveExistingRecipesForTargets(ObjectDB.instance);
                if (removed > 0)
                LogS.LogInfo($"Removed {removed} pre-existing recipe(s) for target items before applying new ones.");
                TryApplyUpgrades(ObjectDB.instance);
                LogS.LogInfo("FerdItemsUp: upgrade rules applied once after world load.");
            }
        }

        private static bool AllTargetsPresent()
        {
            if (ZNetScene.instance == null) return false;
            foreach (var n in TargetPrefabNames)
                if (ZNetScene.instance.GetPrefab(n) == null) return false;
            return true;
        }

        // ===================== Core upgrade logic =====================

        internal static void TryApplyUpgrades(ObjectDB odb)
        {
            if (odb == null) return;

            var blackForge = ResolveBlackForge();
            if (blackForge == null)
            {
                if (!string.Equals(SceneManager.GetActiveScene().name, "start", StringComparison.OrdinalIgnoreCase))
                    LogS?.LogWarning("BlackForge crafting station not found. Recipes will keep their current station.");
            }

            int patched = 0;

            foreach (var prefabName in TargetPrefabNames)
            {
                GameObject go = ZNetScene.instance?.GetPrefab(prefabName);
                if (!go)
                {
                    LogS?.LogInfo($"Prefab not yet available: {prefabName} (will not retry; one-shot mode)");
                    continue;
                }

                var itemDrop = go.GetComponent<ItemDrop>();
                var shared = itemDrop?.m_itemData?.m_shared;
                if (itemDrop == null || shared == null)
                {
                    LogS?.LogWarning($"GameObject '{prefabName}' missing ItemDrop/SharedData.");
                    continue;
                }

                // Force exact max quality = 3 (requirement)
                shared.m_maxQuality = 3;

                // Durability per level = 0
                shared.m_durabilityPerLevel = 0f;

                // +2 armor per level for armor-like items
                if (IsArmorLike(shared))
                    shared.m_armorPerLevel = 2f;

                // Find or create the recipe
                Recipe recipe;
                if (_appliedOnce == true)
                    recipe = FindRecipeForItem(odb, itemDrop);
                else
                    recipe = CreateRecipeForItem(odb, itemDrop);

                if (recipe == null)
                {
                    LogS?.LogWarning($"No Recipe found/created for {prefabName}; skipping.");
                    continue;
                }

                // BlackForge station levels: craft=3 → Q2=4 → Q3=5
                if (blackForge != null) recipe.m_craftingStation = blackForge;
                if (recipe.m_minStationLevel < 3) recipe.m_minStationLevel = 3;
                recipe.m_enabled = true;

                // Apply costs 
                if (CraftCosts.TryGetValue(prefabName, out var ccosts))
                {
                    ApplyCraftCosts(odb, recipe, ccosts);
                }
                else
                {
                    LogS?.LogWarning($"No crafting cost table for {prefabName} (skipping cost setup).");
                }

                if (UpgradeCosts.TryGetValue(prefabName, out var costs))
                {
                    ApplyPerLevelCosts(odb, recipe, costs);
                }
                else
                {
                    LogS?.LogWarning($"No upgrade cost table for {prefabName} (skipping cost setup).");
                }

                // Debug: summary
                int q2Level = recipe.m_minStationLevel + 1;
                int q3Level = recipe.m_minStationLevel + 2;
                LogS?.LogInfo($"[{prefabName}] Station={(recipe.m_craftingStation ? recipe.m_craftingStation.name : "NULL")} " +
                              $"minLevel={recipe.m_minStationLevel} (q2={q2Level}, q3={q3Level}) " +
                              $"maxQ={shared.m_maxQuality} armorPerLvl={shared.m_armorPerLevel} durPerLvl={shared.m_durabilityPerLevel}");

                if (recipe.m_resources != null)
                {
                    foreach (var req in recipe.m_resources)
                    {
                        if (req?.m_resItem)
                            LogS?.LogInfo($"  - req {req.m_resItem.name}: base={req.m_amount}, perLvl={req.m_amountPerLevel}");
                        else
                            LogS?.LogWarning("  - req NULL resource (check prefab name)");
                    }
                }

                patched++;
                LogS?.LogInfo($"Configured upgradeability for: {prefabName}");
            }

            _appliedOnce = true;

            LogS?.LogInfo($"Finished. Patched {patched}/{TargetPrefabNames.Length} items.");

            // Refresh ObjectDB and UI so the changes show at the station
            RefreshObjectDB(odb);
        }

        // --- Helpers ---

        private static string Clean(string name) => string.IsNullOrEmpty(name) ? name : name.Replace("(Clone)", "").Trim();

        private static bool IsArmorLike(ItemDrop.ItemData.SharedData s)
        {
            var t = s.m_itemType;
            return t == ItemDrop.ItemData.ItemType.Chest
                   || t == ItemDrop.ItemData.ItemType.Legs
                   || t == ItemDrop.ItemData.ItemType.Helmet
                   || t == ItemDrop.ItemData.ItemType.Shoulder; // capes
        }

        private static int RemoveExistingRecipesForTargets(ObjectDB odb)
        {
            if (odb == null) return 0;
            int removedTotal = 0;

            foreach (var prefabName in TargetPrefabNames)
            {
                var go = ZNetScene.instance?.GetPrefab(prefabName);
                var itemDrop = go ? go.GetComponent<ItemDrop>() : null;
                if (!itemDrop) continue;

                string clean = Clean(itemDrop.name);

                var matches = odb.m_recipes
                    .Where(r => r && r.m_item && (r.m_item == itemDrop || Clean(r.m_item.name) == clean))
                    .ToList();

                if (matches.Count == 0) continue;

                foreach (var r in matches)
                {
                    odb.m_recipes.Remove(r);
                    removedTotal++;
                    LogS?.LogInfo($"Removed existing recipe '{r.name}' for item {clean}");
                }
            }

            return removedTotal;
        }

        private static Recipe FindRecipeForItem(ObjectDB odb, ItemDrop item)
        {
            var itemName = Clean(item.name);
            // match by reference OR by cleaned name (covers (Clone) cases)
            return odb.m_recipes.FirstOrDefault(r =>
                r && r.m_item && (r.m_item == item || Clean(r.m_item.name) == itemName));
        }

        private static Recipe CreateRecipeForItem(ObjectDB odb, ItemDrop item)
        {
            var rec = ScriptableObject.CreateInstance<Recipe>();
            rec.name = $"Recipe_{Clean(item.name)}";
            rec.m_item = item;
            rec.m_amount = 1;
            rec.m_enabled = true;
            rec.m_resources = Array.Empty<Piece.Requirement>();
            odb.m_recipes.Add(rec);
            return rec;
        }

        private static CraftingStation ResolveBlackForge()
        {
            // direct prefab(s)
            var prefab = ZNetScene.instance?.GetPrefab("blackforge");
            if (prefab)
            {
                var cs = prefab.GetComponent<CraftingStation>();
                if (cs) return cs;
            }
            prefab = ZNetScene.instance?.GetPrefab("piece_blackforge");
            if (prefab)
            {
                var cs = prefab.GetComponent<CraftingStation>();
                if (cs) return cs;
            }

            // last resort: scan
            foreach (var cs in Resources.FindObjectsOfTypeAll<CraftingStation>())
                if (cs && cs.name.IndexOf("blackforge", StringComparison.OrdinalIgnoreCase) >= 0)
                    return cs;

            return null;
        }

        private static void ApplyCraftCosts(ObjectDB odb, Recipe recipe, (string res, int amt)[] costs)
        {
            var result = new List<Piece.Requirement>();
            var existing = (recipe.m_resources ?? Array.Empty<Piece.Requirement>()).ToList();

            foreach (var (resName, baseAmt) in costs)
            {
                var drop = ResolveItemDrop(odb, resName);
                if (drop == null)
                {
                    LogS?.LogWarning($"[CraftCosts] Resource prefab not found: {resName}");
                    continue;
                }

                var ex = existing.FirstOrDefault(r => r != null && r.m_resItem == drop);
                if (ex != null)
                {
                    ex.m_amount = baseAmt;
                    result.Add(ex);
                }
                else
                {
                    result.Add(new Piece.Requirement
                    {
                        m_resItem = drop,
                        m_amount = baseAmt,
                        m_amountPerLevel = 0
                    });
                }
            }

            // Replace recipe resources with ONLY our craft list
            recipe.m_resources = result.ToArray();
        }


        private static void ApplyPerLevelCosts(ObjectDB odb, Recipe recipe, (string res, int amt)[] costs)
        {
            var list = new List<Piece.Requirement>();
            if (recipe.m_resources != null && recipe.m_resources.Length > 0)
                list.AddRange(recipe.m_resources);

            foreach (var (resName, perLevel) in costs)
            {
                var resDrop = ResolveItemDrop(odb, resName);
                if (resDrop == null)
                {
                    LogS?.LogWarning($"Resource item not found for recipe: {resName}");
                    continue;
                }

                var existing = list.FirstOrDefault(r => r != null && r.m_resItem == resDrop);
                if (existing != null)
                {
                    if (existing.m_amountPerLevel < perLevel)
                        existing.m_amountPerLevel = perLevel;
                }
                else
                {
                    var req = new Piece.Requirement
                    {
                        m_resItem = resDrop,
                        m_amount = 0,               // base craft unchanged
                        m_amountPerLevel = perLevel // per-level upgrade cost
                    };
                    list.Add(req);
                }
            }

            recipe.m_resources = list.ToArray();
        }

        private static ItemDrop ResolveItemDrop(ObjectDB odb, string prefabName)
        {
            GameObject go = null;
            try { go = odb.GetItemPrefab(prefabName); } catch { }
            if (!go)
            {
                try { go = ZNetScene.instance?.GetPrefab(prefabName); } catch { }
            }
            return go ? go.GetComponent<ItemDrop>() : null;
        }

        private static void RefreshObjectDB(ObjectDB odb)
        {
            if (odb == null) return;

            void InvokeIfExists(string name)
            {
                var mi = typeof(ObjectDB).GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    try { mi.Invoke(odb, null); } catch { }
                }
            }

            InvokeIfExists("UpdateItemHashes");
            InvokeIfExists("UpdateRecipeHashes");
            InvokeIfExists("UpdateItems");
            InvokeIfExists("UpdateRecipes");
            InvokeIfExists("PostProcessInit");

            TryUpdateAvailableRecipes();
            TryRefreshCraftingPanel();
        }

        private static void TryUpdateAvailableRecipes()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var methods = typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                        .Where(m => m.Name == "UpdateAvailableRecipes");
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                try
                {
                    if (ps.Length == 0) { m.Invoke(player, null); return; }
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(bool)) { m.Invoke(player, new object[] { false }); return; }
                }
                catch { }
            }
        }

        private static void TryRefreshCraftingPanel()
        {
            var mi = typeof(InventoryGui).GetMethod("UpdateCraftingPanel",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null && InventoryGui.instance != null)
            {
                try { mi.Invoke(InventoryGui.instance, null); } catch { }
            }
        }

        // ===================== Harmony patches (UI refresh only) =====================

        [HarmonyPatch(typeof(InventoryGui), "Show")]
        private static class Patch_InventoryGui_Show
        {
            [HarmonyPostfix, HarmonyPriority(HarmonyLib.Priority.Last)]
            private static void Postfix()
            {
                if (ObjectDB.instance != null)
                {
                    // Just refresh the panel; we do not reapply rules here.
                    var mi = typeof(InventoryGui).GetMethod("UpdateCraftingPanel",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null && InventoryGui.instance != null)
                    {
                        try { mi.Invoke(InventoryGui.instance, null); } catch { /* ignore */ }
                    }
                }
            }
        }
    }

    internal static class TypeSafeExt
    {
        public static Type[] GetTypesSafe(this Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }
    }
}

