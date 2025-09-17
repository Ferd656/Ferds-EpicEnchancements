// File: 06 - JotunnEntities.cs
// Target: .NET Framework 4.7.2
using System;
using BepInEx;
using UnityEngine;
using System.Reflection;
using FerdEpicEnhancements.JotunnUtils;
using FerdEpicEnhancements.JotunnManagers;

namespace FerdEpicEnhancements.JotunnEntities
{
    public abstract class CustomEntity
    {
        public BepInPlugin SourceMod { get; }
        internal CustomEntity()
        {
            SourceMod = FerdsEpicEnhancementsPlugin.Metadata?.Metadata;
        }
        internal CustomEntity(BepInPlugin sourceMod)
        {
            SourceMod = sourceMod;
        }
        internal CustomEntity(Assembly callingAssembly)
        {
            SourceMod = FerdsEpicEnhancementsPlugin.Metadata?.Metadata;
        }
    }
    public static class RecipeExtension
    {
        public static bool IsValid(this Recipe self)
        {
            bool result;
            try
            {
                string name = self.name;
                if (name.IndexOf('(') > 0)
                {
                    name = name.Substring(self.name.IndexOf('(')).Trim();
                }
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("Recipe must have a name !");
                }
                result = true;
            }
            catch (Exception e)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError(e);
                result = false;
            }
            return result;
        }
    }
    public class CustomRecipe : CustomEntity
    {
        public Recipe Recipe { get; }
        public bool FixReference { get; set; }
        public bool FixRequirementReferences { get; set; }
        public CustomRecipe(Recipe recipe, bool fixReference, bool fixRequirementReferences) : base(Assembly.GetCallingAssembly())
        {
            this.Recipe = recipe;
            this.FixReference = fixReference;
            this.FixRequirementReferences = fixRequirementReferences;
        }
        public CustomRecipe(RecipeConfig recipeConfig) : base(Assembly.GetCallingAssembly())
        {
            this.Recipe = recipeConfig.GetRecipe();
            this.FixReference = true;
            this.FixRequirementReferences = true;
        }
        public bool IsValid()
        {
            return this.Recipe != null && this.Recipe.IsValid() && this.Recipe.m_item != null;
        }
        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }
        public override int GetHashCode()
        {
            return this.Recipe.name.GetStableHashCode();
        }
        public override string ToString()
        {
            return this.Recipe.name;
        }
    }
    public class CustomItem : CustomEntity
    {
        public GameObject ItemPrefab { get; }
        public ItemDrop ItemDrop { get; }
        public CustomRecipe Recipe { get; set; }
        public bool FixReference { get; set; }
        internal Texture2D StyleTex { get; set; }
        internal bool FixConfig { get; set; }
        private string ItemName
        {
            get
            {
                if (!this.ItemPrefab)
                {
                    return this.fallbackItemName;
                }
                return this.ItemPrefab.name;
            }
        }
        public CustomItem(GameObject itemPrefab, bool fixReference) : base(Assembly.GetCallingAssembly())
        {
            this.ItemPrefab = itemPrefab;
            this.ItemDrop = itemPrefab.GetComponent<ItemDrop>();
            this.FixReference = fixReference;
        }
        public CustomItem(string name, bool addZNetView) : base(Assembly.GetCallingAssembly())
        {
            this.ItemPrefab = PrefabManager.Instance.CreateEmptyPrefab(name, addZNetView);
            this.ItemDrop = this.ItemPrefab.AddComponent<ItemDrop>();
            this.ItemDrop.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
            this.ItemDrop.m_itemData.m_shared.m_name = name;
        }
        public CustomItem(string name, string basePrefabName) : base(Assembly.GetCallingAssembly())
        {
            GameObject itemPrefab = PrefabManager.Instance.CreateClonedPrefab(name, basePrefabName);
            if (itemPrefab)
            {
                this.ItemPrefab = itemPrefab;
                this.ItemDrop = this.ItemPrefab.GetComponent<ItemDrop>();
            }
        }
        public bool IsValid()
        {
            bool valid = true;
            if (!this.ItemPrefab)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError(string.Format("CustomItem '{0}' has no prefab", this));
                valid = false;
            }
            if (!this.ItemPrefab)
            {
                valid = false;
            }
            if (!this.ItemDrop)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError(string.Format("CustomItem '{0}' has no ItemDrop component", this));
                valid = false;
            }
            int? num;
            if (!this.ItemDrop)
            {
                num = null;
            }
            else
            {
                ItemDrop.ItemData itemData = this.ItemDrop.m_itemData;
                if (itemData == null)
                {
                    num = null;
                }
                else
                {
                    ItemDrop.ItemData.SharedData shared = itemData.m_shared;
                    if (shared == null)
                    {
                        num = null;
                    }
                    else
                    {
                        Sprite[] icons = shared.m_icons;
                        num = ((icons != null) ? new int?(icons.Length) : null);
                    }
                }
            }
            int? iconCount = num;
            if (this.Recipe != null)
            {
                if (iconCount != null)
                {
                    int? num2 = iconCount;
                    int num3 = 0;
                    if (!(num2.GetValueOrDefault() == num3 & num2 != null))
                    {
                        return valid;
                    }
                }
                FerdsEpicEnhancementsPlugin.LogS.LogError(string.Format("CustomItem '{0}' has no icon", this));
                valid = false;
            }
            return valid;
        }
        public static bool IsCustomItem(string prefabName)
        {
            return ItemManager.Instance.Items.ContainsKey(prefabName);
        }
        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }
        public override int GetHashCode()
        {
            return this.ItemName.GetStableHashCode();
        }
        public override string ToString()
        {
            return this.ItemName;
        }
        internal void FixVariants()
        {
            Sprite[] array;
            if (!this.ItemDrop)
            {
                array = null;
            }
            else
            {
                ItemDrop.ItemData itemData = this.ItemDrop.m_itemData;
                if (itemData == null)
                {
                    array = null;
                }
                else
                {
                    ItemDrop.ItemData.SharedData shared = itemData.m_shared;
                    array = ((shared != null) ? shared.m_icons : null);
                }
            }
            Sprite[] icons = array;
            if (icons != null && icons.Length != 0 && this.StyleTex)
            {
                foreach (Renderer rend in ShaderHelper.GetRenderers(this.ItemPrefab))
                {
                    foreach (Material mat in rend.materials)
                    {
                        mat.shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Creature");
                        if (mat.HasProperty("_StyleTex"))
                        {
                            this.ItemDrop.m_itemData.m_shared.m_variants = icons.Length;
                            rend.gameObject.GetComponent<ItemStyle>();
                            mat.EnableKeyword("_USESTYLES_ON");
                            mat.SetFloat("_Style", 0f);
                            mat.SetFloat("_UseStyles", 1f);
                            mat.SetTexture("_StyleTex", this.StyleTex);
                        }
                    }
                }
            }
        }
        private string fallbackItemName;
    }
    public class CustomPrefab : CustomEntity, IModPrefab
    {
        public GameObject Prefab { get; }
        public bool FixReference { get; set; }
        private string PrefabName
        {
            get => Prefab ? Prefab.name : fallbackPrefabName;
        }
        private string fallbackPrefabName;
        internal CustomPrefab(GameObject prefab, BepInPlugin sourceMod) : base(sourceMod)
        {
            Prefab = prefab;
        }
        public CustomPrefab(GameObject prefab, bool fixReference) : base(Assembly.GetCallingAssembly())
        {
            Prefab = prefab;
            FixReference = fixReference;
        }
        public CustomPrefab(AssetBundle assetBundle, string assetName, bool fixReference) : base(Assembly.GetCallingAssembly())
        {
            fallbackPrefabName = assetName;

            if (!AssetUtils.TryLoadPrefab(SourceMod, assetBundle, assetName, out GameObject prefab))
            {
                return;
            }

            Prefab = prefab;
            FixReference = fixReference;
        }
        public bool IsValid()
        {
            bool valid = true;

            if (!Prefab)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] CustomPrefab '{this}' has no prefab");
                valid = false;
            }

            return valid;
        }
        public static bool IsCustomPrefab(string prefabName)
        {
            return PrefabManager.Instance.Prefabs.ContainsKey(prefabName);
        }
        public override string ToString()
        {
            return PrefabName;
        }
    }
    public class CustomStatusEffect : CustomEntity
    {
        public StatusEffect StatusEffect { get; }

        public bool FixReference { get; set; }
        public CustomStatusEffect(StatusEffect statusEffect, bool fixReference) : base(Assembly.GetCallingAssembly())
        {
            this.StatusEffect = statusEffect;
            this.FixReference = fixReference;
        }
        public bool IsValid()
        {
            return this.StatusEffect != null && this.StatusEffect.IsValid();
        }
        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }

        public override int GetHashCode()
        {
            return this.StatusEffect.name.GetStableHashCode();
        }
        public override string ToString()
        {
            return this.StatusEffect.name;
        }
    }
    public static class StatusEffectExtension
    {
        public static string TokenName(this StatusEffect self)
        {
            return self.m_name;
        }
        public static bool IsValid(this StatusEffect self)
        {
            bool result;
            try
            {
                string name = self.name;
                if (name.IndexOf('(') > 0)
                {
                    name = name.Substring(self.name.IndexOf('(')).Trim();
                }
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("StatusEffect must have a name !");
                }
                result = true;
            }
            catch (Exception e)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError(e);
                result = false;
            }
            return result;
        }
    }
}
