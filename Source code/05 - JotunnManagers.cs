// File: 05 - JotunnManagers.cs
// Target: .NET Framework 4.7.2
using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Reflection;
using UnityEngine.Audio;
using SoftReferenceableAssets;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using FerdEpicEnhancements.JotunnUtils;
using FerdEpicEnhancements.JotunnEntities;

namespace FerdEpicEnhancements.JotunnManagers
{
    public class AssetManager : IManager
    {
        private static AssetManager _instance;
        public static AssetManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AssetManager();
                }
                return _instance;
            }
        }
        private Dictionary<AssetID, AssetRef> assets = new Dictionary<AssetID, AssetRef>();

        private Dictionary<Type, Dictionary<string, AssetID>> mapNameToAssetID;
        internal Dictionary<Type, Dictionary<string, AssetID>> MapNameToAssetID
        {
            get
            {
                if (mapNameToAssetID == null)
                {
                    mapNameToAssetID = CreateNameToAssetID();
                }
                return mapNameToAssetID;
            }
        }

        private GameObject ResolvedAssetsContainer;
        private Dictionary<AssetID, MockResolutionContext> assetsToResolve = new Dictionary<AssetID, MockResolutionContext>();

        public static event Action OnSoftReferenceableAssetsReady;

        private AssetManager() { }

        static AssetManager()
        {
            ((IManager)Instance).Init();
        }

        void IManager.Init()
        {
            FerdsEpicEnhancementsPlugin.LogInit(nameof(AssetManager));

            ResolvedAssetsContainer = new GameObject("Resolved Assets");
            ResolvedAssetsContainer.transform.parent = FerdsEpicEnhancementsPlugin.RootObject.transform;
            ResolvedAssetsContainer.SetActive(false);

            FerdsEpicEnhancementsPlugin._harmony.PatchAll(typeof(Patches));
        }
        public bool IsReady()
        {
            var runtimeType = AccessTools.TypeByName("SoftReferenceableAssets.Runtime");
            var assetLoaderField = runtimeType.GetField("s_assetLoader", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var assetLoader = assetLoaderField.GetValue(null);
            if (assetLoader == null)
                return false;
            var assetBundleLoaderType = AccessTools.TypeByName("SoftReferenceableAssets.AssetBundleLoader");

            var initializedProp = assetBundleLoaderType.GetProperty("Initialized", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (initializedProp != null)
            {
                return (bool)initializedProp.GetValue(assetLoader);
            }
            var initializedField = assetBundleLoaderType.GetField("Initialized", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (initializedField != null)
            {
                return (bool)initializedField.GetValue(assetLoader);
            }
            throw new Exception("Error in AssetBundleLoader Initialized");
        }
        public AssetID AddAsset(Object asset, Object original)
        {
            AssetID assetID = GenerateAssetID(asset);

            if (assets.ContainsKey(assetID))
            {
                return assetID;
            }
            AssetRef assetRef = new AssetRef(FerdsEpicEnhancementsPlugin.Metadata?.Metadata, asset, original);
            assets.Add(assetID, assetRef);
            if (IsReady())
            {
                var ablType = AccessTools.TypeByName("SoftReferenceableAssets.AssetBundleLoader");
                if (ablType == null)
                    throw new Exception("Error retrieving AssetBundleLoader");
                var instanceProp = ablType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object assetBundleLoaderInstance = null;
                if (instanceProp != null)
                {
                    assetBundleLoaderInstance = instanceProp.GetValue(null);
                }
                else
                {
                    var instanceField = ablType.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (instanceField != null)
                    {
                        assetBundleLoaderInstance = instanceField.GetValue(null);
                    }
                }

                if (assetBundleLoaderInstance != null)
                {
                    AddAssetToBundleLoader(assetBundleLoaderInstance, assetID, assetRef);
                }
            }

            return assetID;
        }
        public AssetID AddAsset(Object asset)
        {
            return AddAsset(asset, null);
        }
        public void ResolveMocksOnLoad(AssetID assetID)
        {
            ResolveMocksOnLoad(assetID, null, null);
        }
        public void ResolveMocksOnLoad(AssetID assetID, Transform parent)
        {
            ResolveMocksOnLoad(assetID, parent, null);
        }
        public void ResolveMocksOnLoad<T>(SoftReference<T> softReference, Transform parent, Action<T> resolveCallback) where T : Object
        {
            ResolveMocksOnLoad(softReference.m_assetID, parent, (asset) => resolveCallback?.Invoke(asset as T));
        }
        public void ResolveMocksOnLoad(AssetID assetID, Transform parent, Action<Object> resolveCallback)
        {
            if (assetsToResolve.TryGetValue(assetID, out var context))
            {
                context.Parent = parent ?? context.Parent ?? ResolvedAssetsContainer.transform;
                context.ResolveCallback += resolveCallback;
            }
            else
            {
                assetsToResolve.Add(assetID, new MockResolutionContext(parent ?? ResolvedAssetsContainer.transform, resolveCallback));
            }
        }

        private static void AddAssetToBundleLoader(object assetBundleLoader, AssetID assetID, AssetRef assetRef)
        {
            string bundleName = $"FRD_BundleWrapper_{assetRef.asset.name}";
            string assetPath = $"{assetRef.sourceMod.GUID}/Prefabs/{assetRef.asset.name}";
            var ablType = assetBundleLoader.GetType();
            var bundleNameToLoaderIndexField = ablType.GetField("m_bundleNameToLoaderIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var bundleNameToLoaderIndex = bundleNameToLoaderIndexField?.GetValue(assetBundleLoader) as Dictionary<string, int>;
            if (bundleNameToLoaderIndex == null)
                throw new Exception("No se pudo obtener m_bundleNameToLoaderIndex como Dictionary<string, int>");
            if (bundleNameToLoaderIndex.ContainsKey(bundleName))
                return;
            var assetLocationCtor = typeof(AssetLocation).GetConstructor(new[] { typeof(string), typeof(string) });
            var location = assetLocationCtor.Invoke(new object[] { bundleName, assetPath });
            var bundleLoaderType = AccessTools.TypeByName("SoftReferenceableAssets.BundleLoader");
            var bundleLoaderCtor = bundleLoaderType.GetConstructor(new[] { typeof(string), typeof(string) });
            var bundleLoader = bundleLoaderCtor.Invoke(new object[] { bundleName, "" });
            var holdReferenceMethod = bundleLoaderType.GetMethod("HoldReference", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            holdReferenceMethod.Invoke(bundleLoader, null);
            var bundleLoadersField = ablType.GetField("m_bundleLoaders", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var bundleLoaders = bundleLoadersField.GetValue(assetBundleLoader) as Array;
            int bundleLoadersLength = bundleLoaders?.Length ?? 0;
            bundleNameToLoaderIndex.Add(bundleName, bundleLoadersLength);
            var elementType = bundleLoaders.GetType().GetElementType();
            var newBundleLoaders = Array.CreateInstance(elementType, bundleLoadersLength + 1);
            if (bundleLoadersLength > 0)
                Array.Copy(bundleLoaders, newBundleLoaders, bundleLoadersLength);
            newBundleLoaders.SetValue(bundleLoader, bundleLoadersLength);
            bundleLoadersField.SetValue(assetBundleLoader, newBundleLoaders);
            var assetLoadersField = ablType.GetField("m_assetLoaders", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var assetLoaders = assetLoadersField.GetValue(assetBundleLoader) as Array;
            int assetLoadersLength = assetLoaders?.Length ?? 0;
            int originalBundleLoaderIndex = -1;
            if (assetRef.originalID.IsValid && assetLoaders != null)
            {
                for (int i = 0; i < assetLoaders.Length; i++)
                {
                    var loader = assetLoaders.GetValue(i);
                    var assetIDField = loader.GetType().GetField("m_assetID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (assetIDField != null && assetIDField.GetValue(loader).Equals(assetRef.originalID))
                    {
                        var bundleLoaderIndexField = loader.GetType().GetField("m_bundleLoaderIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (bundleLoaderIndexField != null)
                        {
                            originalBundleLoaderIndex = (int)bundleLoaderIndexField.GetValue(loader);
                            break;
                        }
                    }
                }
            }

            if (assetRef.originalID.IsValid && originalBundleLoaderIndex > 0)
            {
                var origBundleLoader = newBundleLoaders.GetValue(originalBundleLoaderIndex);
                var indicesField = bundleLoaderType.GetField("m_bundleLoaderIndicesOfThisAndDependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var origIndices = indicesField.GetValue(origBundleLoader) as int[];
                var filtered = origIndices?.Where(i => i != originalBundleLoaderIndex).ToList() ?? new List<int>();
                filtered.Add(bundleNameToLoaderIndex[bundleName]);
                var newIndices = filtered.OrderBy(i => i).ToArray();
                indicesField.SetValue(bundleLoader, newIndices);
            }
            else
            {
                var setDependenciesMethod = bundleLoaderType.GetMethod("SetDependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                setDependenciesMethod.Invoke(bundleLoader, new object[] { Array.Empty<string>() });
            }
            newBundleLoaders.SetValue(bundleLoader, bundleNameToLoaderIndex[bundleName]);
            bundleLoadersField.SetValue(assetBundleLoader, newBundleLoaders);
            var assetLoaderType = AccessTools.TypeByName("SoftReferenceableAssets.AssetLoader");
            var assetLoaderCtor = assetLoaderType.GetConstructor(new[] { typeof(AssetID), typeof(AssetLocation) });
            var loaderInstance = assetLoaderCtor.Invoke(new object[] { assetID, location });
            var mAssetField = assetLoaderType.GetField("m_asset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mAssetField.SetValue(loaderInstance, assetRef.asset);
            var assetLoaderHoldReference = assetLoaderType.GetMethod("HoldReference", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            assetLoaderHoldReference.Invoke(loaderInstance, null);
            var assetIDToLoaderIndexField = ablType.GetField("m_assetIDToLoaderIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var assetIDToLoaderIndex = assetIDToLoaderIndexField.GetValue(assetBundleLoader) as Dictionary<AssetID, int>;
            assetIDToLoaderIndex.Add(assetID, assetLoadersLength);
            var assetLoadersElementType = assetLoaders.GetType().GetElementType();
            var newAssetLoaders = Array.CreateInstance(assetLoadersElementType, assetLoadersLength + 1);
            if (assetLoadersLength > 0)
                Array.Copy(assetLoaders, newAssetLoaders, assetLoadersLength);
            newAssetLoaders.SetValue(loaderInstance, assetLoadersLength);
            assetLoadersField.SetValue(assetBundleLoader, newAssetLoaders);
            Instance.MapNameToAssetID[assetRef.asset.GetType()][assetRef.asset.name] = assetID;
        }
        public AssetID GenerateAssetID(Object asset)
        {
            uint u = (uint)asset.name.GetStableHashCode();
            return new AssetID(u, u, u, u);
        }
        public GameObject ClonePrefab(GameObject asset, string newName, Transform parent)
        {
            GameObject clone = Object.Instantiate(asset, parent);
            clone.name = newName;

            AddAsset(clone, asset);

            return clone;
        }
        public AssetID GetAssetID(Type type, string name)
        {
            if (!IsReady())
            {
                throw new InvalidOperationException("The vanilla asset system is not initialized yet");
            }

            if (MapNameToAssetID.TryGetValue(type, out var nameToAssetID) && nameToAssetID.TryGetValue(name, out AssetID assetID))
            {
                return assetID;
            }

            if (MapNameToAssetID.TryGetValue(typeof(Object), out nameToAssetID) && nameToAssetID.TryGetValue(name, out assetID))
            {
                return assetID;
            }

            return new AssetID();
        }
        public AssetID GetAssetID<T>(string name) where T : Object
        {
            return GetAssetID(typeof(T), name);
        }
        public SoftReference<Object> GetSoftReference(Type type, string name)
        {
            AssetID assetID = GetAssetID(type, name);
            return assetID.IsValid ? new SoftReference<Object>(assetID) : default;
        }
        public SoftReference<T> GetSoftReference<T>(string name) where T : Object
        {
            AssetID assetID = GetAssetID<T>(name);
            return assetID.IsValid ? new SoftReference<T>(assetID) : default;
        }

        private static Dictionary<Type, Dictionary<string, AssetID>> CreateNameToAssetID()
        {
            if (!Instance.IsReady())
            {
                throw new InvalidOperationException("The vanilla asset system is not initialized yet");
            }

            Dictionary<Type, Dictionary<string, AssetID>> nameToAssetID = new Dictionary<Type, Dictionary<string, AssetID>>();
            Dictionary<string, string> nameToFullPath = new Dictionary<string, string>();

            foreach (var pair in Runtime.GetAllAssetPathsInBundleMappedToAssetID().ToList())
            {
                string key = pair.Key.Split('/').Last();
                string extenstion = key.Split('.').Last();
                string asset = key.RemoveSuffix($".{extenstion}");

                if (pair.Key == "Assets/UI/prefabs/radials/elements/Hammer.prefab" ||
                    pair.Key == "Assets/UI/prefabs/Radial/elements/Hammer.prefab")
                {
                    continue;
                }

                Type type = Instance.TypeFromExtension(extenstion);

                if (type == null && extenstion == "asset" && key.StartsWith("Recipe_"))
                {
                    type = typeof(Recipe);
                }

                if (type == null)
                {
                    type = typeof(Object);
                }

                if (!nameToAssetID.ContainsKey(type))
                {
                    nameToAssetID.Add(type, new Dictionary<string, AssetID>());
                }

                if (nameToAssetID[type].ContainsKey(asset) && SkipAmbiguousPath(nameToFullPath[asset], pair.Key, extenstion))
                {
                    continue;
                }

                nameToAssetID[type][asset] = pair.Value;
                nameToFullPath[asset] = pair.Key;
            }

            return nameToAssetID;
        }

        private static bool SkipAmbiguousPath(string oldPath, string newPath, string extension)
        {
            if (extension == "prefab")
            {
                if (oldPath.StartsWith("Assets/world/Locations"))
                {
                    return false;
                }
                else if (newPath.StartsWith("Assets/world/Locations"))
                {
                    return true;
                }
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Ambiguous asset name for path. old: {oldPath}, new: {newPath}, using old path");
            }

            return true;
        }
        private Type TypeFromExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case "prefab":
                    return typeof(GameObject);
                case "mat":
                    return typeof(Material);
                case "obj":
                case "fbx":
                    return typeof(Mesh);
                case "png":
                case "jpg":
                case "tga":
                case "tif":
                    return typeof(Texture2D);
                case "wav":
                case "mp3":
                    return typeof(AudioClip);
                case "controller":
                    return typeof(RuntimeAnimatorController);
                case "physicmaterial":
                    return typeof(PhysicsMaterial); 
                case "shader":
                    return typeof(Shader);
                case "anim":
                    return typeof(AnimationClip);
                case "mixer":
                    return typeof(AudioMixer);
                case "txt":
                    return typeof(TextAsset);
                case "ttf":
                case "otf":
                    return typeof(TMPro.TMP_FontAsset);
                case "rendertexture":
                    return typeof(RenderTexture);
                case "lighting":
                    return typeof(LightingSettings);
                default:
                    return null;
            }
        }

        private struct AssetRef
        {
            public BepInPlugin sourceMod;
            public Object asset;
            public AssetID originalID;

            public AssetRef(BepInPlugin sourceMod, Object asset, Object original)
            {
                this.sourceMod = sourceMod;
                this.asset = asset;
                this.originalID = original && Instance.IsReady() ? Instance.GetAssetID(original.GetType(), original.name) : default;
            }
        }

        internal class MockResolutionContext
        {
            public Object Asset { get; private set; }
            public Transform Parent { get; set; }
            public Action<Object> ResolveCallback { get; set; }

            public MockResolutionContext(Transform parent, Action<Object> resolveCallback)
            {
                this.Parent = parent;
                this.ResolveCallback += resolveCallback;
            }

            public bool IsResolved => (bool)Asset;

            public void InstantiateAndResolveAsset(object realAsset) 
            {
                var unityObject = realAsset as UnityEngine.Object;
                if (unityObject == null)
                {
                    throw new ArgumentException("realAsset must be UnityEngine.Object", nameof(realAsset));
                }
                if (IsResolved)
                {
                    return;
                }
                Asset = Object.Instantiate(unityObject, Parent);
                Asset.name = unityObject.name;
                if (Asset is GameObject gameObject)
                {
                    gameObject.FixReferences(true);
                }
                else
                {
                    Asset.FixReferences();
                }

                ResolveCallback?.SafeInvoke(Asset);
            }
            public void DestroyAsset()
            {
                if (Asset)
                {
                    Object.Destroy(Asset);
                }

                Asset = null;
            }
        }
    }
    public class PrefabManager : IManager
    {
        private static PrefabManager _instance;
        public static PrefabManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PrefabManager();
                }
                return _instance;
            }
        }
        private PrefabManager() { }
        static PrefabManager()
        {
            ((IManager)Instance).Init();
        }
        public static event Action OnVanillaPrefabsAvailable;
        public static event Action OnPrefabsRegistered;
        internal GameObject PrefabContainer { get; private set; }
        internal Dictionary<string, CustomPrefab> Prefabs { get; } = new Dictionary<string, CustomPrefab>();
        internal ObjectDB MenuObjectDB { get; private set; }
        void IManager.Init()
        {
            FerdsEpicEnhancementsPlugin.LogInit("PrefabManager");

            PrefabContainer = new GameObject("Prefabs");
            PrefabContainer.transform.parent = FerdsEpicEnhancementsPlugin.RootObject.transform;
            PrefabContainer.SetActive(false);

            FerdsEpicEnhancementsPlugin._harmony.PatchAll(typeof(Patches));
            SceneManager.sceneUnloaded += current =>
            {
                Cache.Clear();
                Instance.MenuObjectDB = null;
            };
        }
        private static class Patches
        {
            [HarmonyPatch(typeof(ZNetScene), "Awake"), HarmonyPostfix]
            private static void RegisterAllToZNetScene() => Instance.RegisterAllToZNetScene();
            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void InvokeOnVanillaObjectsAvailable(ObjectDB other)
            {
                Instance.MenuObjectDB = other;
                Instance.InvokeOnVanillaObjectsAvailable();
            }
            [HarmonyPatch(typeof(ZNetScene), "Awake"), HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void InvokeOnPrefabsRegistered() => Instance.InvokeOnPrefabsRegistered();
            [HarmonyPatch(typeof(ZoneSystem), "SetupLocations"), HarmonyPrefix, HarmonyPriority(Priority.High)]
            private static void ZoneSystem_ClearPrefabCache(ZoneSystem __instance) => Cache.Clear();
        }
        internal void Activate() { }
        internal bool AddPrefab(GameObject prefab, BepInPlugin sourceMod)
        {
            CustomPrefab customPrefab = new CustomPrefab(prefab, sourceMod);
            AddPrefab(customPrefab);
            return Prefabs.ContainsKey(prefab.name);
        }
        public void AddPrefab(GameObject prefab)
        {
            CustomPrefab customPrefab = new CustomPrefab(prefab, false);
            AddPrefab(customPrefab);
        }
        public void AddPrefab(CustomPrefab customPrefab)
        {
            if (!customPrefab.IsValid())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"[{FerdsEpicEnhancementsPlugin.PluginName}] Custom prefab {customPrefab} is not valid");
                return;
            }
            string name = customPrefab.Prefab.name;

            if (Prefabs.ContainsKey(name))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"[{FerdsEpicEnhancementsPlugin.PluginName}] Prefab '{customPrefab}' already exists");
                return;
            }
            customPrefab.Prefab.transform.SetParent(PrefabContainer.transform, false);
            Prefabs.Add(name, customPrefab);
            AssetManager.Instance.AddAsset(customPrefab.Prefab, null);
        }
        public GameObject CreateEmptyPrefab(string name, bool addZNetView = true)
        {
            if (string.IsNullOrEmpty(name))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to create prefab with invalid name: {name}");
                return null;
            }
            if (GetPrefab(name))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to create prefab, name already exists: {name}");
                return null;
            }
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = name;
            prefab.transform.parent = PrefabContainer.transform;
            if (addZNetView)
            {
                ZNetView newView = prefab.AddComponent<ZNetView>();
                newView.m_persistent = true;
            }
            return prefab;
        }
        public GameObject CreateClonedPrefab(string name, string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to clone prefab with invalid baseName: {baseName}");
                return null;
            }
            GameObject prefab = GetPrefab(baseName);
            if (!prefab)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to clone prefab, can not find base prefab with name: {baseName}");
                return null;
            }
            return CreateClonedPrefab(name, prefab);
        }
        public GameObject CreateClonedPrefab(string name, GameObject prefab)
        {
            if (string.IsNullOrEmpty(name))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to clone prefab with invalid name: {name}");
                return null;
            }
            if (!prefab)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to clone prefab, base prefab is not valid");
                return null;
            }
            if (GetPrefab(name))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Failed to clone prefab, name already exists: {name}");
                return null;
            }
            return AssetManager.Instance.ClonePrefab(prefab, name, PrefabContainer.transform);
        }
        public GameObject GetPrefab(string name)
        {
            if (Prefabs.TryGetValue(name, out var custom))
            {
                return custom.Prefab;
            }
            int hash = name.GetStableHashCode();
            if (ZNetScene.instance)
            {
                var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var namedPrefabs = namedPrefabsField?.GetValue(ZNetScene.instance) as Dictionary<int, GameObject>;
                if (namedPrefabs != null && namedPrefabs.TryGetValue(hash, out var prefab))
                {
                    return prefab;
                }
            }
            if (ObjectDB.instance)
            {
                var itemByHashField = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var itemByHash = itemByHashField?.GetValue(ObjectDB.instance) as Dictionary<int, GameObject>;
                if (itemByHash != null && itemByHash.TryGetValue(hash, out var item))
                {
                    return item;
                }
            }
            return Cache.GetPrefab<GameObject>(name);
        }
        public void RemovePrefab(string name)
        {
            Prefabs.Remove(name);
        }
        public void DestroyPrefab(string name)
        {
            if (!Prefabs.TryGetValue(name, out var custom))
            {
                return;
            }
            if (ZNetScene.instance)
            {
                int hash = name.GetStableHashCode();
                var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var namedPrefabs = namedPrefabsField?.GetValue(ZNetScene.instance) as Dictionary<int, GameObject>;

                if (namedPrefabs != null && namedPrefabs.TryGetValue(hash, out var del))
                {
                    ZNetScene.instance.m_prefabs.Remove(del);
                    ZNetScene.instance.m_nonNetViewPrefabs.Remove(del);
                    namedPrefabs.Remove(hash);
                    ZNetScene.instance.Destroy(del);
                }
            }
            if (custom.Prefab)
            {
                Object.Destroy(custom.Prefab);
            }
            Prefabs.Remove(name);
        }
        private void RegisterAllToZNetScene()
        {
            if (Prefabs.Any())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"Adding {Prefabs.Count} custom prefabs to the ZNetScene");
                List<CustomPrefab> toDelete = new List<CustomPrefab>();
                foreach (var customPrefab in Prefabs.Values)
                {
                    try
                    {
                        if (customPrefab.FixReference)
                        {
                            customPrefab.Prefab.FixReferences(true);
                            customPrefab.FixReference = false;
                        }
                        RegisterToZNetScene(customPrefab.Prefab);
                    }
                    catch (Exception ex)
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Error caught while adding prefab {customPrefab}: {ex}");
                        toDelete.Add(customPrefab);
                    }
                }
                foreach (var prefab in toDelete)
                {
                    if (prefab.Prefab)
                    {
                        DestroyPrefab(prefab.Prefab.name);
                    }
                }
            }
        }
        public void RegisterToZNetScene(GameObject gameObject)
        {
            ZNetScene znet = ZNetScene.instance;
            if (znet)
            {
                string name = gameObject.name;

                if (gameObject.name.StartsWith(MockManager.FRDMockPrefix))
                {
                    return;
                }
                int hash = name.GetStableHashCode();
                var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var namedPrefabs = namedPrefabsField?.GetValue(znet) as Dictionary<int, GameObject>;

                if (namedPrefabs.ContainsKey(hash))
                {
                    FerdsEpicEnhancementsPlugin.LogS.LogDebug($"Prefab {name} already in ZNetScene");
                }
                else
                {
                    if (gameObject.GetComponent<ZNetView>() != null)
                    {
                        znet.m_prefabs.Add(gameObject);
                    }
                    else
                    {
                        znet.m_nonNetViewPrefabs.Add(gameObject);
                    }
                    namedPrefabs.Add(hash, gameObject);
                    FerdsEpicEnhancementsPlugin.LogS.LogDebug($"Added prefab {name}");
                }
            }
        }
        private void InvokeOnVanillaObjectsAvailable()
        {
            OnVanillaPrefabsAvailable?.SafeInvoke();
        }
        private void InvokeOnPrefabsRegistered()
        {
            OnPrefabsRegistered?.SafeInvoke();
        }
        public static class Cache
        {
            private static readonly Dictionary<Type, Dictionary<string, Object>> dictionaryCache =
                new Dictionary<Type, Dictionary<string, Object>>();
            public static Object GetPrefab(Type type, string name)
            {
                if (AssetManager.Instance.IsReady())
                {
                    SoftReference<Object> asset = AssetManager.Instance.GetSoftReference(type, name);

                    if (asset.IsValid)
                    {
                        asset.Load();

                        if (asset.Asset)
                        {
                            if (asset.Asset.GetType() == type)
                            {
                                return asset.Asset;
                            }

                            if (asset.Asset is GameObject gameObject && TryFindAssetInSelfOrChildComponents(gameObject, type, out Object childAsset))
                            {
                                return childAsset;
                            }
                        }
                    }
                }
                if (GetCachedMap(type).TryGetValue(name, out var unityObject))
                {
                    return unityObject;
                }
                return null;
            }
            public static T GetPrefab<T>(string name) where T : Object
            {
                return (T)GetPrefab(typeof(T), name);
            }
            public static Dictionary<string, Object> GetPrefabs(Type type)
            {
                return GetCachedMap(type);
            }
            private static Transform GetParent(Object obj)
            {
                return obj is GameObject gameObject ? gameObject.transform.parent : null;
            }
            private static Object FindBestAsset(IDictionary<string, Object> map, Object newObject, string name)
            {
                if (!map.TryGetValue(name, out Object cached))
                {
                    return newObject;
                }
                if (name == "_NetScene" && cached is GameObject cachedGo && newObject is GameObject newGo)
                {
                    if (!cachedGo.activeInHierarchy && newGo.activeInHierarchy)
                    {
                        return newGo;
                    }
                }
                if (cached is Material cachedMat && newObject is Material newMat && FindBestMaterial(cachedMat, newMat, out var material))
                {
                    return material;
                }
                bool cachedHasParent = GetParent(cached);
                bool newHasParent = GetParent(newObject);
                if (!cachedHasParent && newHasParent)
                {
                    return cached;
                }
                if (cachedHasParent && !newHasParent)
                {
                    return newObject;
                }
                return newObject;
            }
            private static bool FindBestMaterial(Material cachedMaterial, Material newMaterial, out Object material)
            {
                string cachedShaderName = cachedMaterial.shader.name;
                string newShaderName = newMaterial.shader.name;
                if (cachedShaderName == "Hidden/InternalErrorShader" && newShaderName != "Hidden/InternalErrorShader")
                {
                    material = newMaterial;
                    return true;
                }
                if (cachedShaderName != "Hidden/InternalErrorShader" && newShaderName == "Hidden/InternalErrorShader")
                {
                    material = cachedMaterial;
                    return true;
                }
                material = null;
                return false;
            }
            private static Dictionary<string, Object> GetCachedMap(Type type)
            {
                if (dictionaryCache.TryGetValue(type, out var map))
                {
                    return map;
                }
                return InitCache(type);
            }
            private static Dictionary<string, Object> InitCache(Type type)
            {
                Dictionary<string, Object> map = new Dictionary<string, Object>();
                foreach (var unityObject in Resources.FindObjectsOfTypeAll(type))
                {
                    string name = unityObject.name;
                    map[name] = FindBestAsset(map, unityObject, name);
                }
                dictionaryCache[type] = map;
                return map;
            }
            public static void Clear()
            {
                dictionaryCache.Clear();
            }
            public static void Clear<T>() where T : Object
            {
                dictionaryCache.Remove(typeof(T));
            }
        }
        private static bool TryFindAssetOfComponent(Component unityObject, Type objectType, out Object asset)
        {
            var type = unityObject.GetType();
            ClassMember classMember = ClassMember.GetClassMember(type);
            foreach (var member in classMember.Members)
            {
                if (member.MemberType == objectType && member.HasGetMethod)
                {
                    asset = (Object)member.GetValue(unityObject);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }
            asset = null;
            return false;
        }
        internal static bool TryFindAssetInSelfOrChildComponents(GameObject unityObject, Type objectType, out Object asset)
        {
            if (!unityObject)
            {
                asset = null;
                return false;
            }
            if (objectType.IsSubclassOf(typeof(Component)))
            {
                var component = unityObject.GetComponent(objectType);

                if (component)
                {
                    asset = component;
                    return true;
                }
            }
            foreach (var component in unityObject.GetComponents<Component>())
            {
                if (!(component is Transform))
                {
                    if (TryFindAssetOfComponent(component, objectType, out asset))
                    {
                        return (bool)asset;
                    }
                }
            }
            foreach (Transform tf in unityObject.transform)
            {
                if (TryFindAssetInSelfOrChildComponents(tf.gameObject, objectType, out asset))
                {
                    return (bool)asset;
                }
            }

            asset = null;
            return false;
        }
    }
    public class ItemManager : IManager
    {
        private static ItemManager _instance;
        public static ItemManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ItemManager();
                }
                return _instance;
            }
        }
        private ItemManager() { }
        static ItemManager()
        {
            ((IManager)Instance).Init();
        }
        internal static event Action OnKitbashItemsAvailable;
        public static event Action OnItemsRegisteredFejd;
        public static event Action OnItemsRegistered;
        internal readonly Dictionary<string, CustomItem> Items = new Dictionary<string, CustomItem>();
        internal readonly HashSet<CustomRecipe> Recipes = new HashSet<CustomRecipe>();
        internal readonly HashSet<CustomStatusEffect> StatusEffects = new HashSet<CustomStatusEffect>();
        void IManager.Init()
        {
            FerdsEpicEnhancementsPlugin.LogInit("ItemManager");
            FerdsEpicEnhancementsPlugin._harmony.PatchAll(typeof(Patches));
            PrefabManager.Instance.Activate();
        }
        private static class Patches
        {
            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), HarmonyPrefix, HarmonyPriority(-100)]
            private static void RegisterCustomDataFejd(ObjectDB __instance, ObjectDB other) => Instance.RegisterCustomDataFejd(__instance, other);

            [HarmonyPatch(typeof(ObjectDB), "Awake"), HarmonyPrefix]
            private static void RegisterCustomData(ObjectDB __instance) => Instance.RegisterCustomData(__instance);

            [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned)), HarmonyPostfix]
            private static void ReloadKnownRecipes(Player __instance) => Instance.ReloadKnownRecipes(__instance);

            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void InvokeOnItemsRegisteredFejd() => Instance.InvokeOnItemsRegisteredFejd();

            [HarmonyPatch(typeof(ObjectDB), "Awake"), HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void InvokeOnItemsRegistered() => Instance.InvokeOnItemsRegistered();
        }
        public bool AddItem(CustomItem customItem)
        {
            if (!customItem.IsValid())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError( $"Custom item {customItem} is not valid");
                return false;
            }
            if (Items.ContainsKey(customItem.ItemPrefab.name))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning( $"Custom item {customItem} already added");
                return false;
            }
            if (!PrefabManager.Instance.AddPrefab(customItem.ItemPrefab, customItem.SourceMod))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"Failed adding prefab for {customItem.ItemPrefab.name}");
                return false;
            }
            if (customItem.ItemPrefab.layer == 0)
            {
                customItem.ItemPrefab.layer = LayerMask.NameToLayer("item");
            }
            Items.Add(customItem.ItemPrefab.name, customItem);
            if (customItem.Recipe != null)
            {
                AddRecipe(customItem.Recipe);
            }
            if (PrefabManager.Instance.MenuObjectDB)
            {
                RegisterItemInObjectDB(PrefabManager.Instance.MenuObjectDB, customItem.ItemPrefab, customItem.SourceMod);
            }
            return true;
        }
        public CustomItem GetItem(string itemName)
        {
            if (Items.TryGetValue(itemName, out CustomItem item))
            {
                return item;
            }
            return null;
        }
        public void RemoveItem(string itemName)
        {
            var item = GetItem(itemName);
            if (item == null)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Could not remove item {itemName}: Not found");
                return;
            }
            RemoveItem(item);
        }
        public void RemoveItem(CustomItem item)
        {
            Items.Remove(item.ItemPrefab.name);
            if (ObjectDB.instance && item.ItemPrefab)
            {
                ObjectDB.instance.m_items.Remove(item.ItemPrefab);
                var itemByHashField = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var itemByHash = itemByHashField?.GetValue(ObjectDB.instance) as Dictionary<int, GameObject>;
                if (itemByHash != null)
                {
                    itemByHash.Remove(item.ItemPrefab.name.GetStableHashCode());
                }
            }
            if (item.ItemPrefab)
            {
                PrefabManager.Instance.RemovePrefab(item.ItemPrefab.name);
            }
            if (item.Recipe != null)
            {
                RemoveRecipe(item.Recipe);
            }
        }
        public bool AddRecipe(CustomRecipe customRecipe)
        {
            if (!customRecipe.IsValid())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"Custom recipe {customRecipe} is not valid");
                return false;
            }
            if (Recipes.Contains(customRecipe))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Custom recipe {customRecipe} already added");
                return false;
            }
            Recipes.Add(customRecipe);
            return true;
        }
        public CustomRecipe GetRecipe(string recipeName)
        {
            return Recipes.FirstOrDefault(x => x.Recipe.name.Equals(recipeName));
        }
        public void RemoveRecipe(string recipeName)
        {
            var recipe = GetRecipe(recipeName);
            if (recipe == null)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Could not remove recipe {recipeName}: Not found");
                return;
            }

            RemoveRecipe(recipe);
        }
        public void RemoveRecipe(CustomRecipe recipe)
        {
            Recipes.Remove(recipe);
            if (ObjectDB.instance && recipe.Recipe)
            {
                ObjectDB.instance.m_recipes.Remove(recipe.Recipe);
            }
        }
        public bool AddStatusEffect(CustomStatusEffect customStatusEffect)
        {
            if (!customStatusEffect.IsValid())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Custom status effect {customStatusEffect} is not valid");
                return false;
            }
            if (StatusEffects.Contains(customStatusEffect))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Custom status effect {customStatusEffect} already added");
                return false;
            }
            StatusEffects.Add(customStatusEffect);
            return true;
        }
        private void RegisterCustomItems(ObjectDB objectDB)
        {
            if (Items.Count > 0)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"Adding {Items.Count} custom items to the ObjectDB");
                List<CustomItem> toDelete = new List<CustomItem>();
                foreach (var customItem in Items.Values)
                {
                    try
                    {
                        var itemDrop = customItem.ItemDrop;
                        if (customItem.FixReference || customItem.FixConfig)
                        {
                            customItem.ItemPrefab.FixReferences(customItem.FixReference);
                            itemDrop.m_itemData.m_shared.FixReferences();
                            customItem.FixVariants();
                            customItem.FixReference = false;
                            customItem.FixConfig = false;
                        }
                        if (!itemDrop.m_itemData.m_dropPrefab)
                        {
                            itemDrop.m_itemData.m_dropPrefab = customItem.ItemPrefab;
                        }
                        RegisterItemInObjectDB(objectDB, customItem.ItemPrefab, customItem.SourceMod);
                    }
                    catch (Exception ex)
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Error caught while adding item {customItem}: {ex}");
                        toDelete.Add(customItem);
                    }
                }
                foreach (var item in toDelete)
                {
                    if (item.ItemPrefab)
                    {
                        PrefabManager.Instance.DestroyPrefab(item.ItemPrefab.name);
                    }
                    RemoveItem(item);
                }
            }
        }
        public void RegisterItemInObjectDB(GameObject prefab) => RegisterItemInObjectDB(ObjectDB.instance, prefab, FerdsEpicEnhancementsPlugin.Metadata?.Metadata);
        private void RegisterItemInObjectDB(ObjectDB objectDB, GameObject prefab, BepInPlugin sourceMod)
        {
            var itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                throw new Exception($"Prefab {prefab.name} has no ItemDrop component attached");
            }
            var name = prefab.name;
            var hash = name.GetStableHashCode();
            var itemByHashField = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var itemByHash = itemByHashField?.GetValue(objectDB) as Dictionary<int, GameObject>;
            if (itemByHash != null && itemByHash.ContainsKey(hash))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogDebug($"Already added item {prefab.name}");
            }
            else
            {
                if (!PrefabManager.Instance.Prefabs.ContainsKey(name))
                {
                    PrefabManager.Instance.AddPrefab(prefab, sourceMod);
                }
                Dictionary<int, GameObject> namedPrefabs = null;
                if (ZNetScene.instance != null)
                {
                    var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    namedPrefabs = namedPrefabsField?.GetValue(ZNetScene.instance) as Dictionary<int, GameObject>;
                }
                if (namedPrefabs != null && !namedPrefabs.ContainsKey(hash))
                {
                    PrefabManager.Instance.RegisterToZNetScene(prefab);
                }
                objectDB.m_items.Add(prefab);
                if (itemByHash != null)
                {
                    itemByHash.Add(hash, prefab);
                }
            }
            FerdsEpicEnhancementsPlugin.LogS.LogInfo($"Added item {prefab.name} | Token: {itemDrop.name}");
        }
        private void RegisterCustomRecipes(ObjectDB objectDB)
        {
            if (Recipes.Any())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"Adding {Recipes.Count} custom recipes to the ObjectDB");
                List<CustomRecipe> toDelete = new List<CustomRecipe>();
                foreach (var customRecipe in Recipes)
                {
                    try
                    {
                        var recipe = customRecipe.Recipe;

                        if (customRecipe.FixReference || customRecipe.FixRequirementReferences)
                        {
                            recipe.FixReferences();
                            customRecipe.FixReference = false;
                            customRecipe.FixRequirementReferences = false;
                        }
                        objectDB.m_recipes.Add(recipe);

                        FerdsEpicEnhancementsPlugin.LogS.LogDebug($"Added recipe for {recipe.m_item}");
                    }
                    catch (Exception ex)
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Error caught while adding recipe {customRecipe}: {ex}");
                        toDelete.Add(customRecipe);
                    }
                }
                foreach (var recipe in toDelete)
                {
                    Recipes.Remove(recipe);
                }
            }
        }
        private void RegisterCustomStatusEffects(ObjectDB objectDB)
        {
            if (StatusEffects.Any())
            {
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"Adding {StatusEffects.Count} custom status effects to the ObjectDB");
                List<CustomStatusEffect> toDelete = new List<CustomStatusEffect>();
                foreach (var customStatusEffect in StatusEffects)
                {
                    try
                    {
                        var statusEffect = customStatusEffect.StatusEffect;
                        if (customStatusEffect.FixReference)
                        {
                            statusEffect.FixReferences();
                            customStatusEffect.FixReference = false;
                        }
                        objectDB.m_StatusEffects.Add(statusEffect);
                        FerdsEpicEnhancementsPlugin.LogS.LogDebug($"Added status effect {customStatusEffect}");
                    }
                    catch (Exception ex)
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Error caught while adding status effect {customStatusEffect}: {ex}");
                        toDelete.Add(customStatusEffect);
                    }
                }
                foreach (var statusEffect in toDelete)
                {
                    StatusEffects.Remove(statusEffect);
                }
            }
        }
        private void RegisterCustomDataFejd(ObjectDB self, ObjectDB other)
        {
            InvokeOnKitbashItemsAvailable();
            UpdateRegistersSafe(other);
            RegisterCustomItems(other);
        }
        private void InvokeOnKitbashItemsAvailable()
        {
            OnKitbashItemsAvailable?.SafeInvoke();
        }
        private void InvokeOnItemsRegisteredFejd()
        {
            OnItemsRegisteredFejd?.SafeInvoke();
        }
        private void RegisterCustomData(ObjectDB self)
        {
            if (SceneManager.GetActiveScene().name == "main")
            {
                UpdateRegistersSafe(self);
                RegisterCustomItems(self);
                RegisterCustomRecipes(self);
                RegisterCustomStatusEffects(self);
            }
        }
        private void InvokeOnItemsRegistered()
        {
            if (SceneManager.GetActiveScene().name == "main")
            {
                OnItemsRegistered?.SafeInvoke();
            }
        }
        private void ReloadKnownRecipes(Player self)
        {
            if (Items.Count > 0 || Recipes.Count > 0)
            {
                try
                {
                    var method = typeof(Player).GetMethod("UpdateKnownRecipesList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (method != null)
                    {
                        method.Invoke(self, null);
                    }
                    else
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning("No se pudo encontrar el método UpdateKnownRecipesList en Player.");
                    }
                }
                catch (Exception ex)
                {
                    FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Exception caught while reloading player recipes: {ex}");
                }
            }
        }
        private static void UpdateRegistersSafe(ObjectDB objectDB)
        {
            var itemByHashField = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var itemByDataField = typeof(ObjectDB).GetField("m_itemByData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var itemByHash = itemByHashField?.GetValue(objectDB) as Dictionary<int, GameObject>;
            var itemByData = itemByDataField?.GetValue(objectDB) as Dictionary<object, GameObject>;
            itemByHash?.Clear();
            itemByData?.Clear();
            foreach (GameObject item in objectDB.m_items)
            {
                if (!item)
                {
                    FerdsEpicEnhancementsPlugin.LogS.LogWarning("Found null item in ObjectDB.m_items");
                    continue;
                }
                string name = item.name;
                int hash = name.GetStableHashCode();
                if (itemByHash != null && itemByHash.ContainsKey(hash))
                {
                    var mod = ModQuery.GetPrefab(name)?.SourceMod;
                    FerdsEpicEnhancementsPlugin.LogS.LogWarning($"Found duplicate item '{name}' ({hash}) in ObjectDB.m_items");
                    continue;
                }
                itemByHash?.Add(hash, item);
                ItemDrop component = item.GetComponent<ItemDrop>();
                if (component != null && itemByData != null)
                {
                    itemByData[component.m_itemData.m_shared] = item;
                }
            }
        }
    }
    internal class MockManager : IManager
    {
        public static MockManager Instance
        {
            get
            {
                MockManager result;
                if ((result = MockManager._instance) == null)
                {
                    result = (MockManager._instance = new MockManager());
                }
                return result;
            }
        }
        private static MethodInfo Object_IsPersistent { get; } = AccessTools.Method(typeof(UnityEngine.Object), "IsPersistent", null, null);
        private MockManager()
        {
            ((IManager)this).Init();
        }
        void IManager.Init()
        {
            FerdsEpicEnhancementsPlugin.LogInit("MockManager");
            this.MockPrefabContainer = new GameObject("MockPrefabs");
            this.MockPrefabContainer.transform.parent = FerdsEpicEnhancementsPlugin.RootObject.transform;
            this.MockPrefabContainer.SetActive(false);
            FerdsEpicEnhancementsPlugin._harmony.PatchAll(typeof(MockManager.Patches));
        }
        public GameObject CreateMockedGameObject(string prefabName)
        {
            string name = "FRDmock_" + prefabName;
            GameObject mock;
            if (this.mockedPrefabs.TryGetValue(name, out mock) && mock)
            {
                return mock;
            }
            GameObject g = new GameObject(name);
            g.transform.parent = this.MockPrefabContainer.transform;
            g.SetActive(false);
            this.mockedPrefabs[name] = g;
            return g;
        }
        public T CreateMockedPrefab<T>(string prefabName) where T : Component
        {
            GameObject g = this.CreateMockedGameObject(prefabName);
            string name = g.name;
            T mock = g.GetComponent<T>();
            if (!mock)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning(string.Format("Could not create mock for prefab {0} of type {1}", prefabName, typeof(T)));
                return default(T);
            }
            mock.name = name;
            return mock;
        }
        public static T GetRealPrefabFromMock<T>(UnityEngine.Object unityObject) where T : UnityEngine.Object
        {
            return (T)((object)MockManager.GetRealPrefabFromMock(unityObject, typeof(T)));
        }
        public static UnityEngine.Object GetRealPrefabFromMock(UnityEngine.Object unityObject, Type mockObjectType)
        {
            if (!unityObject)
            {
                return null;
            }
            if (GUIUtils.IsHeadless && mockObjectType == typeof(Texture))
            {
                return null;
            }
            string assetName;
            List<string> childNames;
            if (!MockManager.IsMockName(MockManager.GetCleanedName(mockObjectType, unityObject.name), out assetName, out childNames))
            {
                return null;
            }
            UnityEngine.Object asset;
            if (childNames.Count == 0 && MockManager.TryGetAsset(mockObjectType, assetName, out asset))
            {
                return asset;
            }
            GameObject prefab = PrefabManager.Cache.GetPrefab<GameObject>(assetName);
            if (!prefab)
            {
                if (childNames.Count > 0)
                {
                    MockResolveFailure.MockResolveFailures.Add(new MockResolveFailure("GameObject with name '" + assetName + "' was not found.", assetName, childNames, mockObjectType));
                }
                else
                {
                    MockResolveFailure.MockResolveFailures.Add(new MockResolveFailure("", assetName, "", mockObjectType));
                }
                return null;
            }
            if (PrefabManager.TryFindAssetInSelfOrChildComponents(prefab, mockObjectType, out asset))
            {
                return asset;
            }
            if (childNames.Count > 0)
            {
                string usedPath = prefab.transform.GetPath().TrimStart(new char[]
                {
                    '/'
                });
                MockResolveFailure.MockResolveFailures.Add(new MockResolveFailure(mockObjectType.Name + " not found at child '" + usedPath + "'.", assetName, childNames, mockObjectType));
            }
            else
            {
                MockResolveFailure.MockResolveFailures.Add(new MockResolveFailure(mockObjectType.Name + " not found at prefab '" + assetName + "'.", assetName, "", mockObjectType));
            }
            return null;
        }
        private static bool TryGetAsset(Type mockObjectType, string assetName, out UnityEngine.Object asset)
        {
            asset = PrefabManager.Cache.GetPrefab(mockObjectType, assetName);
            return asset;
        }
        internal static void FixReferences(object objectToFix, int depth)
        {
            if (depth == 5 || objectToFix == null)
            {
                return;
            }
            Type type = objectToFix.GetType();
            ClassMember classMember = ClassMember.GetClassMember(type);
            foreach (MemberBase member in classMember.Members)
            {
                MockManager.FixMemberReferences(member, objectToFix, depth + 1);
            }
        }
        internal static void ReplaceMockGameObject(Transform child, GameObject realPrefab, GameObject parent)
        {
            if (MockManager.IsPersistent(parent))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning(string.Concat(new string[]
                {
                    "Cannot replace mock child ",
                    child.name,
                    " in persistent prefab ",
                    parent.name,
                    ". Clone the prefab before replacing mocks, i.e. with PrefabManager.Instance.CreateClonedPrefab or ZoneManager.Instance.CreateLocationContainer for locations"
                }));
                return;
            }
            GameObject newObject = UnityEngine.Object.Instantiate<GameObject>(realPrefab, parent.transform);
            newObject.name = realPrefab.name;
            newObject.SetActive(child.gameObject.activeSelf);
            newObject.transform.position = child.gameObject.transform.position;
            newObject.transform.rotation = child.gameObject.transform.rotation;
            newObject.transform.localScale = child.gameObject.transform.localScale;
            int siblingIndex = child.GetSiblingIndex();
            UnityEngine.Object.DestroyImmediate(child.gameObject);
            newObject.transform.SetSiblingIndex(siblingIndex);
        }
        private static bool IsPersistent(GameObject parent)
        {
            return (bool)MockManager.Object_IsPersistent.Invoke(null, new object[]
            {
                parent
            });
        }
        private static bool IsMockName(string name, out string assetName, out List<string> childNames)
        {
            name = name.Trim();
            if (name.StartsWith("FRDmock_", StringComparison.Ordinal))
            {
                string mockTarget = name.Substring("FRDmock_".Length);
                string[] splitNames = mockTarget.Split(new string[]
                {
                    "__"
                }, StringSplitOptions.RemoveEmptyEntries);
                assetName = splitNames[0];
                childNames = (from splitName in splitNames.Skip(1)
                              select splitName.Trim()).ToList<string>();
                return true;
            }
            if (name.StartsWith("VLmock_", StringComparison.Ordinal))
            {
                assetName = name.Substring("VLmock_".Length);
                childNames = new List<string>();
                return true;
            }
            assetName = name;
            childNames = new List<string>();
            return false;
        }
        private static string GetCleanedName(Type objectType, string name)
        {
            if (objectType == typeof(Material))
            {
                return name.RemoveSuffix("(Instance)");
            }
            if (objectType == typeof(Mesh))
            {
                return name.RemoveSuffix("Instance");
            }
            return name.Trim();
        }
        private static void FixMemberReferences(MemberBase member, object objectToFix, int depth)
        {
            if (member.MemberType == typeof(DropTable))
            {
                MockManager.FixDropTable(member, objectToFix);
                return;
            }
            if (member.IsUnityObject && member.HasGetMethod)
            {
                UnityEngine.Object target = (UnityEngine.Object)member.GetValue(objectToFix);
                UnityEngine.Object realPrefab = MockManager.GetRealPrefabFromMock(target, member.MemberType);
                if (realPrefab)
                {
                    member.SetValue(objectToFix, realPrefab);
                    return;
                }
                Material material = target as Material;
                if (material != null)
                {
                    MockManager.TryFixMaterial(material);
                    return;
                }
            }
            else if (member.IsEnumeratedClass && member.IsEnumerableOfUnityObjects)
            {
                bool isArray = member.MemberType.IsArray;
                bool isList = member.MemberType.IsGenericType && member.MemberType.GetGenericTypeDefinition() == typeof(List<>);
                bool isHashSet = member.MemberType.IsGenericType && member.MemberType.GetGenericTypeDefinition() == typeof(HashSet<>);
                bool isDictionary = member.MemberType.IsGenericType && member.MemberType.GetGenericTypeDefinition() == typeof(Dictionary<,>);
                if (!isArray && !isList && !isHashSet)
                {
                    return;
                }
                else
                {
                    IEnumerable<UnityEngine.Object> currentValues = (IEnumerable<UnityEngine.Object>)member.GetValue(objectToFix);
                    if (currentValues == null)
                    {
                        return;
                    }
                    List<UnityEngine.Object> list = new List<UnityEngine.Object>();
                    bool hasAnyMockResolved = false;
                    foreach (UnityEngine.Object unityObject in currentValues)
                    {
                        UnityEngine.Object realPrefab2 = MockManager.GetRealPrefabFromMock(unityObject, member.EnumeratedType);
                        list.Add(realPrefab2 ? realPrefab2 : unityObject);
                        hasAnyMockResolved = (hasAnyMockResolved || realPrefab2);
                        if (!realPrefab2)
                        {
                            Material material2 = unityObject as Material;
                            if (material2 != null)
                            {
                                MockManager.TryFixMaterial(material2);
                            }
                        }
                    }
                    if (list.Count > 0 && hasAnyMockResolved)
                    {
                        MethodInfo cast = ReflectionHelper.Cache.EnumerableCast;
                        MethodInfo castT = cast.MakeGenericMethod(new Type[]
                        {
                            member.EnumeratedType
                        });
                        object correctTypeList = castT.Invoke(null, new object[]
                        {
                            list
                        });
                        if (isArray)
                        {
                            MethodInfo toArray = ReflectionHelper.Cache.EnumerableToArray;
                            MethodInfo toArrayT = toArray.MakeGenericMethod(new Type[]
                            {
                                member.EnumeratedType
                            });
                            object array = toArrayT.Invoke(null, new object[]
                            {
                                correctTypeList
                            });
                            member.SetValue(objectToFix, array);
                            return;
                        }
                        if (isList)
                        {
                            MethodInfo toList = ReflectionHelper.Cache.EnumerableToList;
                            MethodInfo toListT = toList.MakeGenericMethod(new Type[]
                            {
                                member.EnumeratedType
                            });
                            object newList = toListT.Invoke(null, new object[]
                            {
                                correctTypeList
                            });
                            member.SetValue(objectToFix, newList);
                            return;
                        }
                        if (isHashSet)
                        {
                            Type hash = typeof(HashSet<>).MakeGenericType(new Type[]
                            {
                                member.EnumeratedType
                            });
                            object newHash = Activator.CreateInstance(hash, new object[]
                            {
                                correctTypeList
                            });
                            member.SetValue(objectToFix, newHash);
                            return;
                        }
                    }
                }
            }
            else
            {
                if (member.IsEnumeratedClass)
                {
                    bool isDict = member.MemberType.IsGenericType && member.MemberType.GetGenericTypeDefinition() == typeof(Dictionary<,>);
                    if (isDict)
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning("Not fixing potential mock references for field " + member.MemberType.Name + " : Dictionary is not supported.");
                        return;
                    }
                    IEnumerable<object> currentValues2 = (IEnumerable<object>)member.GetValue(objectToFix);
                    if (currentValues2 == null)
                    {
                        return;
                    }
                    using (IEnumerator<object> enumerator2 = currentValues2.GetEnumerator())
                    {
                        while (enumerator2.MoveNext())
                        {
                            object value = enumerator2.Current;
                            MockManager.FixReferences(value, depth);
                        }
                        return;
                    }
                }
                if (member.IsClass && member.HasGetMethod)
                {
                    MockManager.FixReferences(member.GetValue(objectToFix), depth);
                }
            }
        }
        private static void FixDropTable(MemberBase member, object objectToFix)
        {
            List<DropTable.DropData> drops = ((DropTable)member.GetValue(objectToFix)).m_drops;
            for (int i = 0; i < drops.Count; i++)
            {
                DropTable.DropData drop = drops[i];
                GameObject realPrefab = MockManager.GetRealPrefabFromMock<GameObject>(drop.m_item);
                if (realPrefab)
                {
                    drop.m_item = realPrefab;
                }
                drops[i] = drop;
            }
        }
        private static void TryFixMaterial(Material material)
        {
            if (GUIUtils.IsHeadless)
            {
                return;
            }
            if (!material || MockManager.fixedMaterials.Contains(material) || MockManager.queuedToFixMaterials.Contains(material))
            {
                return;
            }
            MockManager.FixMaterial(material);
        }
        private static void FixQueuedMaterials()
        {
            MockResolveFailure.MockResolveFailures.Clear();
            PrefabManager.Cache.Clear<Texture>();
            MockManager.allVanillaObjectsAvailable = true;
            foreach (Material material in new HashSet<Material>(MockManager.queuedToFixMaterials))
            {
                MockManager.queuedToFixMaterials.Remove(material);
                MockManager.FixMaterial(material);
            }
            MockResolveFailure.PrintMockResolveFailures(string.Empty);
        }
        private static void FixMaterial(Material material)
        {
            if (!material || MockManager.fixedMaterials.Contains(material))
            {
                return;
            }
            bool fixedTextures = MockManager.FixTextures(material);
            bool fixedShader = true;
            if (!GUIUtils.IsHeadless)
            {
                fixedShader = MockManager.FixShader(material);
            }
            if (fixedTextures && fixedShader)
            {
                MockManager.fixedMaterials.Add(material);
                return;
            }
            MockManager.queuedToFixMaterials.Add(material);
        }
        private static bool FixTextures(Material material)
        {
            bool everythingFixed = true;
            int currentFailures = MockResolveFailure.MockResolveFailures.Count;
            foreach (int prop in material.GetTexturePropertyNameIDs())
            {
                Texture texture = material.GetTexture(prop);
                if (texture)
                {
                    Texture realTexture = MockManager.GetRealPrefabFromMock<Texture>(texture);
                    if (MockResolveFailure.MockResolveFailures.Count > currentFailures)
                    {
                        everythingFixed = false;
                        currentFailures = MockResolveFailure.MockResolveFailures.Count;
                    }
                    else if (realTexture)
                    {
                        material.SetTexture(prop, realTexture);
                    }
                }
            }
            return everythingFixed;
        }
        private static bool FixShader(Material material)
        {
            Shader usedShader = material.shader;
            string cleanedShaderName;
            List<string> childNames;
            if (!usedShader || !MockManager.IsMockName(usedShader.name, out cleanedShaderName, out childNames))
            {
                return true;
            }
            Shader realShader = PrefabManager.Cache.GetPrefab<Shader>(cleanedShaderName);
            if (realShader)
            {
                material.shader = realShader;
                return true;
            }
            if (MockManager.allVanillaObjectsAvailable)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning("Could not find shader " + usedShader.name);
            }
            return false;
        }
        private static MockManager _instance;
        public const string FRDMockPrefix = "FRDmock_";
        public const string FRDMockSeparator = "__";
        internal GameObject MockPrefabContainer;
        private Dictionary<string, GameObject> mockedPrefabs = new Dictionary<string, GameObject>();
        private static HashSet<Material> fixedMaterials = new HashSet<Material>();
        private static HashSet<Material> queuedToFixMaterials = new HashSet<Material>();
        private static bool allVanillaObjectsAvailable;
        private static class Patches
        {
            [HarmonyPatch(typeof(ZoneSystem), "Start")]
            [HarmonyPostfix]
            private static void ZoneSystem_Start(ZoneSystem __instance)
            {
                MockManager.FixQueuedMaterials();
            }
        }
    }
}
