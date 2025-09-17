// File: 07 - JotunnUtils.cs
// Target: .NET Framework 4.7.2
using System;
using BepInEx;
using System.IO;
using HarmonyLib;
using MonoMod.Cil;
using System.Linq;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using BepInEx.Configuration;
using UnityEngine.Rendering;
using System.Collections.Generic;
using FerdEpicEnhancements.JotunnEntities;
using FerdEpicEnhancements.JotunnManagers;

namespace FerdEpicEnhancements.JotunnUtils
{
    public interface IModPrefab
    {
        GameObject Prefab { get;}

        BepInPlugin SourceMod { get;}
    }
    public static class Mock<T> where T : Component
    {
        public static T Create(string name)
        {
            return MockManager.Instance.CreateMockedPrefab<T>(name);
        }
    }
    public static class ActionExtensions
    {
        public static void SafeInvoke(this Action action)
        {
            action?.Invoke();
        }

        public static void SafeInvoke<T>(this Action<T> action, T arg)
        {
            action?.Invoke(arg);
        }
    }
    public static class StringExtensions
    {
        public static string RemoveSuffix(this string s, string suffix)
        {
            if (s.EndsWith(suffix))
            {
                return s.Substring(0, s.Length - suffix.Length);
            }

            return s;
        }
    }
    public static class ReflectionHelper
    {
        public const BindingFlags AllBindingFlags = (BindingFlags)(-1);

        public static bool IsSameOrSubclass(this Type type, Type @base)
        {
            return type.IsSubclassOf(@base) || type == @base;
        }
        public static Type GetEnumeratedType(this Type type) =>
            type?.GetElementType() ??
            (typeof(IEnumerable).IsAssignableFrom(type) ? type.GetGenericArguments().FirstOrDefault() : null);

        public static Type GetCallingType()
        {
            return (new StackTrace().GetFrames() ?? Array.Empty<StackFrame>())
                .First(x => x.GetMethod().ReflectedType?.Assembly != typeof(FerdsEpicEnhancementsPlugin).Assembly)
                .GetMethod()
                .ReflectedType;
        }
        public static Assembly GetCallingAssembly()
        {
            return (new StackTrace().GetFrames() ?? Array.Empty<StackFrame>())
                .First(x => x.GetMethod().ReflectedType?.Assembly != typeof(FerdsEpicEnhancementsPlugin).Assembly)
                .GetMethod()
                .ReflectedType?
                .Assembly;
        }
        public static T GetPrivateProperty<T>(object instance, string name)
        {
            PropertyInfo var = instance.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);

            if (var == null)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError("Property " + name + " does not exist on type: " + instance.GetType());
                return default(T);
            }
            return (T)var.GetValue(instance);
        }
        public static class Cache
        {
            private static MethodInfo _enumerableToArray;
            public static MethodInfo EnumerableToArray
            {
                get
                {
                    if (_enumerableToArray == null)
                    {
                        _enumerableToArray = typeof(Enumerable).GetMethod("ToArray", AllBindingFlags);
                    }

                    return _enumerableToArray;
                }
            }
            private static MethodInfo _enumerableToList;
            public static MethodInfo EnumerableToList
            {
                get
                {
                    if (_enumerableToList == null)
                    {
                        _enumerableToList = typeof(Enumerable).GetMethod("ToList", AllBindingFlags);
                    }

                    return _enumerableToList;
                }
            }
            private static MethodInfo _enumerableCast;
            public static MethodInfo EnumerableCast
            {
                get
                {
                    if (_enumerableCast == null)
                    {
                        _enumerableCast = typeof(Enumerable).GetMethod("Cast", AllBindingFlags);
                    }

                    return _enumerableCast;
                }
            }
        }
    }
    internal class ExtEquipment : MonoBehaviour
    {
        private static bool Enabled;

        private static readonly Dictionary<VisEquipment, ExtEquipment> Instances =
            new Dictionary<VisEquipment, ExtEquipment>();

        public static void Enable()
        {
            if (!Enabled)
            {
                Enabled = true;
                FerdsEpicEnhancementsPlugin._harmony.PatchAll(typeof(ExtEquipment));
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "Awake"), HarmonyPostfix]
        private static void VisEquipment_Awake(VisEquipment __instance)
        {
            if (!__instance.gameObject.TryGetComponent(out ExtEquipment _))
            {
                __instance.gameObject.AddComponent<ExtEquipment>();
            }
        }
        [HarmonyPatch(typeof(VisEquipment), "UpdateEquipmentVisuals"), HarmonyPrefix]
        private static void VisEquipment_UpdateEquipmentVisuals(VisEquipment __instance)
        {
            var nview = (ZNetView)typeof(VisEquipment)
            .GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);

            if (nview && nview.GetZDO() is ZDO zdo)
            {
                if (Instances.TryGetValue(__instance, out var instance))
                {
                    instance.NewRightItemVariant = zdo.GetInt("RightItemVariant");
                    instance.NewChestVariant = zdo.GetInt("ChestItemVariant");
                    instance.NewRightBackItemVariant = zdo.GetInt("RightBackItemVariant");
                }
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetRightHandEquipped"), HarmonyILManipulator]
        private static void VisEquipment_SetRightHandEquiped(ILContext il)
        {
            ExtEquipment instance = null;

            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(1)))
            {
                c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, VisEquipment, int>>((hash, self) =>
                {
                    if (Instances.TryGetValue(self, out instance) && hash != 0 &&
                        instance.CurrentRightItemVariant != instance.NewRightItemVariant)
                    {
                        instance.CurrentRightItemVariant = instance.NewRightItemVariant;
                        return hash + instance.CurrentRightItemVariant;
                    }
                    return hash;
                });

            }

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchLdcI4(0)))
            {
                c.EmitDelegate<Func<int, int>>(variant => (instance != null) ? instance.CurrentRightItemVariant : variant);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetBackEquipped"), HarmonyILManipulator]
        private static void VisEquipment_SetBackEquiped(ILContext il)
        {
            ExtEquipment instance = null;

            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(2)))
            {
                c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, VisEquipment, int>>((hash, self) =>
                {
                    if (Instances.TryGetValue(self, out instance) && hash != 0 &&
                        instance.CurrentRightBackItemVariant != instance.NewRightBackItemVariant)
                    {
                        instance.CurrentRightBackItemVariant = instance.NewRightBackItemVariant;
                        return hash + instance.CurrentRightBackItemVariant;
                    }
                    return hash;
                });
            }

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(2),
                x => x.MatchLdcI4(0)))
            {
                c.EmitDelegate<Func<int, int>>(variant => (instance != null) ? instance.CurrentRightBackItemVariant : variant);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetChestEquipped"), HarmonyILManipulator]
        private static void VisEquipment_SetChestEquiped(ILContext il)
        {
            ExtEquipment instance = null;

            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(1)))
            {
                c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, VisEquipment, int>>((hash, self) =>
                {
                    if (Instances.TryGetValue(self, out instance) && hash != 0 &&
                        instance.CurrentChestVariant != instance.NewChestVariant)
                    {
                        instance.CurrentChestVariant = instance.NewChestVariant;
                        return hash + instance.CurrentChestVariant;
                    }
                    return hash;
                });
            }
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchLdcI4(-1)))
            {
                c.EmitDelegate<Func<int, int>>(variant => (instance != null) ? instance.CurrentChestVariant : variant);
            }
        }
        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetRightItem)), HarmonyPrefix]
        private static void VisEquipment_SetRightItem(VisEquipment __instance, string name)
        {
            string rightItem = (string)typeof(VisEquipment)
            .GetField("m_rightItem", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);

            var nview = (ZNetView)typeof(VisEquipment)
            .GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);

            if (Instances.TryGetValue(__instance, out var instance) &&
                instance.MyHumanoid && instance.MyHumanoid.CallGetRightItem() != null &&
                !(rightItem == name && instance.MyHumanoid.CallGetRightItem().m_variant == instance.CurrentRightItemVariant))
            {
                instance.NewRightItemVariant = instance.MyHumanoid.CallGetRightItem().m_variant;
                if (nview && nview.GetZDO() is ZDO zdo)
                {
                    zdo.Set("RightItemVariant", (!string.IsNullOrEmpty(name)) ? instance.NewRightItemVariant : 0);
                }
            }
        }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetRightBackItem)), HarmonyPrefix]
        private static void VisEquipment_SetRightBackItem(VisEquipment __instance, string name)
        {
            var i = Instances.TryGetValue(__instance, out var instance);

            var nview = (ZNetView)typeof(VisEquipment)
            .GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);

            ItemDrop.ItemData hiddenRightItem = (ItemDrop.ItemData)typeof(Humanoid)
            .GetField("m_hiddenRightItem", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(instance.MyHumanoid);

            string rightBackItem = (string)typeof(VisEquipment)
            .GetField("m_rightBackItem", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);
            
            if (i &&
                instance.MyHumanoid && hiddenRightItem != null &&
                !(rightBackItem == name && hiddenRightItem.m_variant == instance.CurrentRightBackItemVariant))
            {
                instance.NewRightBackItemVariant = hiddenRightItem.m_variant;
                if (nview && nview.GetZDO() is ZDO zdo)
                {
                    zdo.Set("RightBackItemVariant", (!string.IsNullOrEmpty(name)) ? instance.NewRightBackItemVariant : 0);
                }
            }
        }
        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetChestItem)), HarmonyPrefix]
        private static void VisEquipment_SetChestItem(VisEquipment __instance, string name)
        {
            var i = Instances.TryGetValue(__instance, out var instance);

            var nview = (ZNetView)typeof(VisEquipment)
            .GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);

            string ChestItem = (string)typeof(VisEquipment)
            .GetField("m_chestItem", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(__instance);

            ItemDrop.ItemData humanoidChestItem = (ItemDrop.ItemData)typeof(Humanoid)
            .GetField("m_chestItem", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(instance.MyHumanoid);

            if (i &&
                instance.MyHumanoid && humanoidChestItem != null &&
                !(ChestItem == name && humanoidChestItem.m_variant == instance.CurrentChestVariant))
            {
                instance.NewChestVariant = humanoidChestItem.m_variant;
                if (nview && nview.GetZDO() is ZDO zdo)
                {
                    zdo.Set("ChestItemVariant", (!string.IsNullOrEmpty(name)) ? instance.NewChestVariant : 0);
                }
            }
        }

        private Humanoid MyHumanoid;

        private int NewRightItemVariant;
        private int CurrentRightItemVariant;
        private int NewRightBackItemVariant;
        private int CurrentRightBackItemVariant;
        private int NewChestVariant;
        private int CurrentChestVariant;

        private void Awake()
        {
            MyHumanoid = gameObject.GetComponent<Humanoid>();
            Instances.Add(gameObject.GetComponent<VisEquipment>(), this);
        }

        private void OnDestroy()
        {
            Instances.Remove(gameObject.GetComponent<VisEquipment>());
        }
    }
    internal static class HumanoidReflectionExtensions
    {
        public static ItemDrop.ItemData CallGetRightItem(this Humanoid humanoid)
        {
            var method = typeof(Humanoid).GetMethod("GetRightItem", BindingFlags.Instance | BindingFlags.NonPublic);
            return (ItemDrop.ItemData)method.Invoke(humanoid, null);
        }
    }
    public static class AssetUtils
    {
        public const char AssetBundlePathSeparator = '$';


        public static Mesh LoadMesh(string meshPath)
        {
            string path = Path.Combine(BepInEx.Paths.PluginPath, meshPath);

            if (!File.Exists(path))
            {
                return null;
            }

            return ObjImporter.ImportFile(path);
        }

        public static AssetBundle LoadAssetBundle(string bundlePath)
        {
            string path = Path.Combine(BepInEx.Paths.PluginPath, bundlePath);

            if (!File.Exists(path))
            {
                return null;
            }

            return AssetBundle.LoadFromFile(path);
        }

        public static AssetBundle LoadAssetBundleFromResources(string bundleName, Assembly resourceAssembly)
        {
            if (resourceAssembly == null)
            {
                throw new ArgumentNullException("Parameter resourceAssembly can not be null.");
            }

            string resourceName = null;
            try
            {
                resourceName = resourceAssembly.GetManifestResourceNames().Single(str => str.EndsWith(bundleName));
            }
            catch (Exception) { }

            if (resourceName == null)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"AssetBundle {bundleName} not found in assembly manifest");
                return null;
            }

            AssetBundle ret;
            using (var stream = resourceAssembly.GetManifestResourceStream(resourceName))
            {
                ret = AssetBundle.LoadFromStream(stream);
            }

            return ret;
        }

        public static AssetBundle LoadAssetBundleFromResources(string bundleName)
        {
            return LoadAssetBundleFromResources(bundleName, ReflectionHelper.GetCallingAssembly());
        }
        public static string LoadTextFromResources(string fileName, Assembly resourceAssembly)
        {
            if (resourceAssembly == null)
            {
                throw new ArgumentNullException("Parameter resourceAssembly can not be null.");
            }

            string resourceName = null;
            try
            {
                resourceName = resourceAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            }
            catch (Exception) { }

            if (resourceName == null)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"File {fileName} not found in assembly manifest");
                return null;
            }

            string ret;
            using (var stream = resourceAssembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    ret = reader.ReadToEnd();
                }
            }

            return ret;
        }

        public static string LoadTextFromResources(string fileName)
        {
            return LoadTextFromResources(fileName, ReflectionHelper.GetCallingAssembly());
        }

        public static string LoadText(string path)
        {
            string absPath = Path.Combine(BepInEx.Paths.PluginPath, path);

            if (!File.Exists(absPath))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"Error, failed to load contents from non-existant path: ${absPath}");
                return null;
            }

            return File.ReadAllText(absPath);
        }
        public static Sprite LoadSprite(string assetPath)
        {
            string path = Path.Combine(BepInEx.Paths.PluginPath, assetPath);

            if (!File.Exists(path))
            {
                return null;
            }

            if (path.Contains(AssetBundlePathSeparator.ToString()))
            {
                string[] parts = path.Split(AssetBundlePathSeparator);
                string bundlePath = parts[0];
                string assetName = parts[1];

                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                Sprite ret = bundle.LoadAsset<Sprite>(assetName);
                bundle.Unload(false);
                return ret;
            }
            return null;

        }
        internal static bool TryLoadPrefab(BepInPlugin sourceMod, AssetBundle assetBundle, string assetName, out GameObject prefab)
        {
            try
            {
                prefab = assetBundle.LoadAsset<GameObject>(assetName);
            }
            catch (Exception e)
            {
                prefab = null;
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] Failed to load prefab '{assetName}' from AssetBundle {assetBundle}:\n{e}");
                return false;
            }

            if (!prefab)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] Failed to load prefab '{assetName}' from AssetBundle {assetBundle}");
                return false;
            }

            return true;
        }
    }
    public static class ObjImporter
    {
        private struct meshStruct
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public Vector2[] uv1;
            public Vector2[] uv2;
            public int[] triangles;
            public int[] faceVerts;
            public int[] faceUVs;
            public Vector3[] faceData;
            public string name;
            public string fileName;
        }
        public static Mesh ImportFile(string filePath)
        {
            meshStruct newMesh = CreateMeshStruct(filePath);
            PopulateMeshStruct(ref newMesh);

            Vector3[] newVerts = new Vector3[newMesh.faceData.Length];
            Vector2[] newUVs = new Vector2[newMesh.faceData.Length];
            Vector3[] newNormals = new Vector3[newMesh.faceData.Length];
            int i = 0;
            foreach (Vector3 v in newMesh.faceData)
            {
                newVerts[i] = newMesh.vertices[(int)v.x - 1];
                if (v.y >= 1)
                    newUVs[i] = newMesh.uv[(int)v.y - 1];

                if (v.z >= 1)
                    newNormals[i] = newMesh.normals[(int)v.z - 1];
                i++;
            }

            Mesh mesh = new Mesh();

            mesh.vertices = newVerts;
            mesh.uv = newUVs;
            mesh.normals = newNormals;
            mesh.triangles = newMesh.triangles;

            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }
        private static meshStruct CreateMeshStruct(string filename)
        {
            int triangles = 0;
            int vertices = 0;
            int vt = 0;
            int vn = 0;
            int face = 0;
            meshStruct mesh = new meshStruct();
            mesh.fileName = filename;
            StreamReader stream = File.OpenText(filename);
            string entireText = stream.ReadToEnd();
            stream.Close();
            using (StringReader reader = new StringReader(entireText))
            {
                string currentText = reader.ReadLine();
                char[] splitIdentifier = { ' ' };
                string[] brokenString;
                while (currentText != null)
                {
                    if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ")
                        && !currentText.StartsWith("vn "))
                    {
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();                           
                        brokenString = currentText.Split(splitIdentifier, 50);      
                        switch (brokenString[0])
                        {
                            case "v":
                                vertices++;
                                break;
                            case "vt":
                                vt++;
                                break;
                            case "vn":
                                vn++;
                                break;
                            case "f":
                                face = face + brokenString.Length - 1;
                                triangles = triangles + 3 * (brokenString.Length - 2); 

                                break;
                        }
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                }
            }
            mesh.triangles = new int[triangles];
            mesh.vertices = new Vector3[vertices];
            mesh.uv = new Vector2[vt];
            mesh.normals = new Vector3[vn];
            mesh.faceData = new Vector3[face];
            return mesh;
        }
        private static void PopulateMeshStruct(ref meshStruct mesh)
        {
            StreamReader stream = File.OpenText(mesh.fileName);
            string entireText = stream.ReadToEnd();
            stream.Close();
            using (StringReader reader = new StringReader(entireText))
            {
                string currentText = reader.ReadLine();

                char[] splitIdentifier = { ' ' };
                char[] splitIdentifier2 = { '/' };
                string[] brokenString;
                string[] brokenBrokenString;
                int f = 0;
                int f2 = 0;
                int v = 0;
                int vn = 0;
                int vt = 0;
                int vt1 = 0;
                int vt2 = 0;
                while (currentText != null)
                {
                    if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ") &&
                        !currentText.StartsWith("vn ") && !currentText.StartsWith("g ") && !currentText.StartsWith("usemtl ") &&
                        !currentText.StartsWith("mtllib ") && !currentText.StartsWith("vt1 ") && !currentText.StartsWith("vt2 ") &&
                        !currentText.StartsWith("vc ") && !currentText.StartsWith("usemap "))
                    {
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();
                        brokenString = currentText.Split(splitIdentifier, 50);
                        switch (brokenString[0])
                        {
                            case "g":
                                break;
                            case "usemtl":
                                break;
                            case "usemap":
                                break;
                            case "mtllib":
                                break;
                            case "v":
                                mesh.vertices[v] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                         System.Convert.ToSingle(brokenString[3]));
                                v++;
                                break;
                            case "vt":
                                mesh.uv[vt] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt++;
                                break;
                            case "vt1":
                                mesh.uv[vt1] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt1++;
                                break;
                            case "vt2":
                                mesh.uv[vt2] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt2++;
                                break;
                            case "vn":
                                mesh.normals[vn] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                        System.Convert.ToSingle(brokenString[3]));
                                vn++;
                                break;
                            case "vc":
                                break;
                            case "f":

                                int j = 1;
                                List<int> intArray = new List<int>();
                                while (j < brokenString.Length && ("" + brokenString[j]).Length > 0)
                                {
                                    Vector3 temp = new Vector3();
                                    brokenBrokenString = brokenString[j].Split(splitIdentifier2, 3);    
                                    temp.x = System.Convert.ToInt32(brokenBrokenString[0]);
                                    if (brokenBrokenString.Length > 1)                                 
                                    {
                                        if (brokenBrokenString[1] != "")                                   
                                        {
                                            temp.y = System.Convert.ToInt32(brokenBrokenString[1]);
                                        }
                                        temp.z = System.Convert.ToInt32(brokenBrokenString[2]);
                                    }
                                    j++;

                                    mesh.faceData[f2] = temp;
                                    intArray.Add(f2);
                                    f2++;
                                }
                                j = 1;
                                while (j + 2 < brokenString.Length)     
                                {
                                    mesh.triangles[f] = intArray[0];
                                    f++;
                                    mesh.triangles[f] = intArray[j];
                                    f++;
                                    mesh.triangles[f] = intArray[j + 1];
                                    f++;

                                    j++;
                                }
                                break;
                        }
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                }
            }
        }
    } 
    public static class PrefabExtension
    {
        public static void FixReferences(this object objectToFix)
        {
            MockResolveFailure.MockResolveFailures.Clear();
            MockManager.FixReferences(objectToFix, 0);
            UnityEngine.Object unityObject = objectToFix as UnityEngine.Object;
            string assetName = unityObject ? unityObject.name : objectToFix.ToString();
            MockResolveFailure.PrintMockResolveFailures(assetName);
        }

        public static void FixReferences(this GameObject gameObject)
        {
            MockResolveFailure.MockResolveFailures.Clear();
            gameObject.FixReferencesInternal(false);
            string assetName = gameObject ? gameObject.name : string.Empty;
            MockResolveFailure.PrintMockResolveFailures(assetName);
        }

        public static void FixReferences(this GameObject gameObject, bool recursive)
        {
            MockResolveFailure.MockResolveFailures.Clear();
            gameObject.FixReferencesInternal(recursive);
            string assetName = gameObject ? gameObject.name : string.Empty;
            MockResolveFailure.PrintMockResolveFailures(assetName);
        }

        private static void FixReferencesInternal(this GameObject gameObject, bool recursive)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!(component is Transform))
                {
                    MockManager.FixReferences(component, 0);
                }
            }
            if (!recursive)
            {
                return;
            }
            List<Tuple<Transform, GameObject>> mockChildren = new List<Tuple<Transform, GameObject>>();
            foreach (object obj in gameObject.transform)
            {
                Transform child = (Transform)obj;
                GameObject realPrefab = MockManager.GetRealPrefabFromMock<GameObject>(child.gameObject);
                if (realPrefab)
                {
                    mockChildren.Add(new Tuple<Transform, GameObject>(child, realPrefab));
                }
                else
                {
                    child.gameObject.FixReferencesInternal(true);
                }
            }
            foreach (Tuple<Transform, GameObject> mockChild in mockChildren)
            {
                MockManager.ReplaceMockGameObject(mockChild.Item1, mockChild.Item2, gameObject);
            }
        }
        public static void CloneFields(this GameObject gameObject, GameObject objectToClone)
        {
            Dictionary<FieldInfo, object> fieldValues = new Dictionary<FieldInfo, object>();
            Component[] origComponents = objectToClone.GetComponentsInChildren<Component>();
            foreach (Component origComponent in origComponents)
            {
                foreach (FieldInfo fieldInfo in origComponent.GetType().GetFields((BindingFlags)(-1)))
                {
                    if (!fieldInfo.IsLiteral && !fieldInfo.IsInitOnly)
                    {
                        fieldValues.Add(fieldInfo, fieldInfo.GetValue(origComponent));
                    }
                }
                if (!gameObject.GetComponent(origComponent.GetType()))
                {
                    gameObject.AddComponent(origComponent.GetType());
                }
            }
            Component[] clonedComponents = gameObject.GetComponentsInChildren<Component>();
            foreach (Component clonedComponent in clonedComponents)
            {
                foreach (FieldInfo fieldInfo2 in clonedComponent.GetType().GetFields((BindingFlags)(-1)))
                {
                    object fieldValue;
                    if (fieldValues.TryGetValue(fieldInfo2, out fieldValue))
                    {
                        fieldInfo2.SetValue(clonedComponent, fieldValue);
                    }
                }
            }
        }
    }
    public class MockResolveFailure
    {
        public static List<MockResolveFailure> MockResolveFailures { get; } = new List<MockResolveFailure>();

        public MockResolveFailure(string message, string failedMockName, string failedMockPath, Type mockType)
        {
            this.Message = message;
            this.FailedMockName = failedMockName;
            this.FailedMockPath = failedMockPath;
            this.MockType = mockType;
        }
        public MockResolveFailure(string message, string failedMockName, IEnumerable<string> failedMockPath, Type mockType)
        {
            this.Message = message;
            this.FailedMockName = failedMockName;
            this.FailedMockPath = string.Join<string>("->", failedMockPath);
            this.MockType = mockType;
        }
        public string Message { get; private set; }
        public string FailedMockName { get; private set; }
        public string FailedMockPath { get; private set; }
        public Type MockType { get; private set; }
        private string ConstructMessage()
        {
            if (string.IsNullOrEmpty(this.FailedMockPath))
            {
                return string.Concat(new string[]
                {
                    "Mock '",
                    this.FailedMockName,
                    "' ",
                    this.MockType.Name,
                    " could not be resolved. ",
                    this.Message
                }).Trim();
            }
            return string.Concat(new string[]
            {
                "Mock ",
                this.MockType.Name,
                " at '",
                this.FailedMockName,
                "' with child path '",
                this.FailedMockPath,
                "' could not be resolved. ",
                this.Message
            }).Trim();
        }
        public static void PrintMockResolveFailures(string prefabName)
        {
            if (MockResolveFailure.MockResolveFailures.Count == 0)
            {
                return;
            }
            IModPrefab prefab = ModQuery.GetPrefab(prefabName);
            BepInPlugin sourceMod = (prefab != null) ? prefab.SourceMod : null;
            int maximumPrinted = Math.Min(5, MockResolveFailure.MockResolveFailures.Count);
            string prefabNameMessage = string.IsNullOrEmpty(prefabName) ? "" : ("for '" + prefabName + "'");
            string truncatedMessage = (MockResolveFailure.MockResolveFailures.Count > maximumPrinted) ? string.Format("(logging first {0} issues)", maximumPrinted) : "";
            FerdsEpicEnhancementsPlugin.LogS.LogWarning(string.Format("{0} mocks {1} could not be resolved. {2}", MockResolveFailure.MockResolveFailures.Count, prefabNameMessage, truncatedMessage).Replace("  ", " ").Trim());
            foreach (MockResolveFailure failure in MockResolveFailure.MockResolveFailures.GetRange(0, maximumPrinted))
            {
                FerdsEpicEnhancementsPlugin.LogS.LogWarning(failure.ConstructMessage());
            }
        }
    }
    public class ModQuery
    {
        internal static void Init()
        {
            FerdsEpicEnhancementsPlugin.LogInit("ModQuery");
            FerdsEpicEnhancementsPlugin._harmony.PatchAll(typeof(ModQuery));
        }

        public static void Enable()
        {
            if (!ModQuery.enabled)
            {
                ModQuery.Init();
            }
            ModQuery.enabled = true;
        }
        public static IEnumerable<IModPrefab> GetPrefabs()
        {
            List<IModPrefab> prefabs = new List<IModPrefab>();
            foreach (KeyValuePair<string, Dictionary<int, ModQuery.ModPrefab>> prefab in ModQuery.Prefabs)
            {
                prefabs.AddRange(prefab.Value.Values);
            }
            prefabs.AddRange(PrefabManager.Instance.Prefabs.Values);
            return prefabs;
        }
        public static IEnumerable<IModPrefab> GetPrefabs(string modGuid)
        {
            List<IModPrefab> prefabs = new List<IModPrefab>();
            prefabs.AddRange(ModQuery.Prefabs[modGuid].Values);
            prefabs.AddRange(from x in PrefabManager.Instance.Prefabs.Values
                             where x.SourceMod.GUID.Equals(modGuid)
                             select x);
            return prefabs;
        }
        public static IModPrefab GetPrefab(string name)
        {
            int hash = name.GetStableHashCode();
            CustomPrefab customPrefab;
            if (PrefabManager.Instance.Prefabs.TryGetValue(name, out customPrefab))
            {
                return customPrefab;
            }
            foreach (KeyValuePair<string, Dictionary<int, ModQuery.ModPrefab>> prefab in ModQuery.Prefabs)
            {
                if (prefab.Value.ContainsKey(hash))
                {
                    return prefab.Value[hash];
                }
            }
            return null;
        }
        [HarmonyPatch(typeof(ZNetScene), "OnDestroy")]
        [HarmonyPostfix]
        private static void ZNetSceneOnDestroy()
        {
            ModQuery.Prefabs.Clear();
            ModQuery.Recipes.Clear();
        }
        [HarmonyPatch(typeof(FejdStartup), "Awake")]
        [HarmonyPostfix]
        private static void FejdStartup_Awake_Postfix()
        {
            ModQuery.FindAndPatchPatches(AccessTools.Method(typeof(ZNetScene), "Awake", null, null));
            ModQuery.FindAndPatchPatches(AccessTools.Method(typeof(ObjectDB), "Awake", null, null));
            ModQuery.FindAndPatchPatches(AccessTools.Method(typeof(ObjectDB), "CopyOtherDB", null, null));
            ModQuery.FindAndPatchPatches(AccessTools.Method(typeof(ObjectDB), "UpdateRegisters", null, null));
        }
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        [HarmonyPrefix]
        [HarmonyPriority(1000)]
        private static void ObjectDBAwake(ObjectDB __instance)
        {
            var method = __instance.GetType().GetMethod("UpdateRegisters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(__instance, null);
            }
        }
        private static void FindAndPatchPatches(MethodBase methodInfo)
        {
            Patches patchInfo = Harmony.GetPatchInfo(methodInfo);
            ModQuery.PatchPatches((patchInfo != null) ? patchInfo.Prefixes : null);
            Patches patchInfo2 = Harmony.GetPatchInfo(methodInfo);
            ModQuery.PatchPatches((patchInfo2 != null) ? patchInfo2.Postfixes : null);
            Patches patchInfo3 = Harmony.GetPatchInfo(methodInfo);
            ModQuery.PatchPatches((patchInfo3 != null) ? patchInfo3.Finalizers : null);
        }
        private static void PatchPatches(ICollection<Patch> patches)
        {
            if (patches == null)
            {
                return;
            }
            foreach (Patch patch in patches)
            {
                if (!(patch.owner == "com.jotunn.jotunn") && !ModQuery.PatchedMethods.Contains(patch.PatchMethod))
                {
                    ModQuery.PatchedMethods.Add(patch.PatchMethod);
                    try
                    {
                        FerdsEpicEnhancementsPlugin._harmony.Patch(patch.PatchMethod, ModQuery.PrePatch, ModQuery.PostPatch, null, null, null);
                    }
                    catch (Exception e)
                    {
                        FerdsEpicEnhancementsPlugin.LogS.LogWarning(string.Format("Failed to patch {0} from {1}", patch.PatchMethod, patch.owner));
                    }
                }
            }
        }
        private static void BeforePatch(object[] __args)
        {
            ObjectDB objectDB = ModQuery.GetObjectDB(__args);
            ZNetScene zNetScene = ModQuery.GetZNetScene(__args);
            ModQuery.state = new Tuple<ModQuery.ZNetSceneState, ModQuery.ObjectDBState>(new ModQuery.ZNetSceneState(zNetScene), new ModQuery.ObjectDBState(objectDB));
        }
        private static void AddPrefabs(IEnumerable<GameObject> before, IEnumerable<GameObject> after, BepInPlugin plugin)
        {
            ModQuery.AddPrefabs(new HashSet<GameObject>(before), new HashSet<GameObject>(after), plugin);
        }

        private static void AddPrefabs(Dictionary<int, GameObject> before, Dictionary<int, GameObject> after, BepInPlugin plugin)
        {
            ModQuery.AddPrefabs(new HashSet<GameObject>(before.Values), new HashSet<GameObject>(after.Values), plugin);
        }
        private static void AddPrefabs(HashSet<GameObject> before, HashSet<GameObject> after, BepInPlugin plugin)
        {
            if (!ModQuery.Prefabs.ContainsKey(plugin.GUID))
            {
                ModQuery.Prefabs.Add(plugin.GUID, new Dictionary<int, ModQuery.ModPrefab>());
            }
            foreach (GameObject prefab in after)
            {
                if (prefab && !before.Contains(prefab))
                {
                    int hash = prefab.name.GetStableHashCode();
                    if (!ModQuery.Prefabs[plugin.GUID].ContainsKey(hash))
                    {
                        ModQuery.Prefabs[plugin.GUID].Add(hash, new ModQuery.ModPrefab(prefab, plugin));
                    }
                }
            }
        }
        private static void AddRecipes(IEnumerable<Recipe> before, IEnumerable<Recipe> after, BepInPlugin plugin)
        {
            ModQuery.AddRecipes(new HashSet<Recipe>(before), new HashSet<Recipe>(after), plugin);
        }
        private static void AddRecipes(HashSet<Recipe> before, HashSet<Recipe> after, BepInPlugin plugin)
        {
            if (!ModQuery.Recipes.ContainsKey(plugin.GUID))
            {
                ModQuery.Recipes.Add(plugin.GUID, new List<Recipe>());
            }
            foreach (Recipe recipe in after)
            {
                if (!before.Contains(recipe) && !ModQuery.Recipes[plugin.GUID].Contains(recipe))
                {
                    ModQuery.Recipes[plugin.GUID].Add(recipe);
                }
            }
        }
        private static ZNetScene GetZNetScene(object[] __args)
        {
            foreach (object arg in __args)
            {
                ZNetScene zNetScene = arg as ZNetScene;
                if (zNetScene != null)
                {
                    return zNetScene;
                }
            }
            return ZNetScene.instance;
        }
        private static ObjectDB GetObjectDB(object[] __args)
        {
            foreach (object arg in __args)
            {
                ObjectDB objectDB = arg as ObjectDB;
                if (objectDB != null)
                {
                    return objectDB;
                }
            }
            return ObjectDB.instance;
        }
        private static readonly Dictionary<string, Dictionary<int, ModQuery.ModPrefab>> Prefabs = new Dictionary<string, Dictionary<int, ModQuery.ModPrefab>>();
        private static readonly Dictionary<string, List<Recipe>> Recipes = new Dictionary<string, List<Recipe>>();
        private static Tuple<ModQuery.ZNetSceneState, ModQuery.ObjectDBState> state;
        private static readonly HashSet<MethodInfo> PatchedMethods = new HashSet<MethodInfo>();
        private static readonly HarmonyMethod PrePatch = new HarmonyMethod(AccessTools.Method(typeof(ModQuery), "BeforePatch", null, null));
        private static readonly HarmonyMethod PostPatch = new HarmonyMethod(AccessTools.Method(typeof(ModQuery), "AfterPatch", null, null));
        private static bool enabled = false;
        private class ModPrefab : IModPrefab
        {
            public GameObject Prefab { get; }

            public BepInPlugin SourceMod { get; }
            public ModPrefab(GameObject prefab, BepInPlugin mod)
            {
                this.Prefab = prefab;
                this.SourceMod = mod;
            }
        }
        private class ZNetSceneState
        {
            public ZNetSceneState(ZNetScene zNetScene)
            {
                this.valid = zNetScene;
                if (!this.valid)
                {
                    return;
                }
                var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var namedPrefabs = namedPrefabsField?.GetValue(zNetScene) as Dictionary<int, GameObject>;
                this.namedPrefabs = namedPrefabs != null
                    ? new Dictionary<int, GameObject>(namedPrefabs)
                    : new Dictionary<int, GameObject>();
                var prefabsField = typeof(ZNetScene).GetField("m_prefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var prefabs = prefabsField?.GetValue(zNetScene) as List<GameObject>;
                this.prefabs = prefabs != null
                    ? new List<GameObject>(prefabs)
                    : new List<GameObject>();
            }
            public void AddNewPrefabs(ZNetScene zNetScene, PluginInfo plugin)
            {
                if (!this.valid || !zNetScene)
                {
                    return;
                }
                var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var namedPrefabs = namedPrefabsField?.GetValue(zNetScene) as Dictionary<int, GameObject>;
                ModQuery.AddPrefabs(this.namedPrefabs, namedPrefabs, plugin.Metadata);

                var prefabsField = typeof(ZNetScene).GetField("m_prefabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var prefabs = prefabsField?.GetValue(zNetScene) as List<GameObject>;
                ModQuery.AddPrefabs(this.prefabs, prefabs, plugin.Metadata);
            }
            public bool valid;
            public readonly Dictionary<int, GameObject> namedPrefabs;
            public readonly List<GameObject> prefabs;
        }
        private class ObjectDBState
        {
            public ObjectDBState(ObjectDB objectDB)
            {
                this.valid = objectDB;
                if (!this.valid)
                {
                    return;
                }
                var itemsField = typeof(ObjectDB).GetField("m_items", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var items = itemsField?.GetValue(objectDB) as List<GameObject>;
                this.items = items != null ? new List<GameObject>(items) : new List<GameObject>();
                var recipesField = typeof(ObjectDB).GetField("m_recipes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var recipes = recipesField?.GetValue(objectDB) as List<Recipe>;
                this.recipes = recipes != null ? new List<Recipe>(recipes) : new List<Recipe>();
                var itemByHashField = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var itemByHash = itemByHashField?.GetValue(objectDB) as Dictionary<int, GameObject>;
                this.itemByHash = itemByHash != null ? new Dictionary<int, GameObject>(itemByHash) : new Dictionary<int, GameObject>();
            }
            public void AddNewPrefabs(ObjectDB objectDB, PluginInfo plugin)
            {
                if (!this.valid || !objectDB)
                {
                    return;
                }
                var itemsField = typeof(ObjectDB).GetField("m_items", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var items = itemsField?.GetValue(objectDB) as List<GameObject>;
                ModQuery.AddPrefabs(this.items, items, plugin.Metadata);

                var itemByHashField = typeof(ObjectDB).GetField("m_itemByHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var itemByHash = itemByHashField?.GetValue(objectDB) as Dictionary<int, GameObject>;
                ModQuery.AddPrefabs(this.itemByHash, itemByHash, plugin.Metadata);

                var recipesField = typeof(ObjectDB).GetField("m_recipes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var recipes = recipesField?.GetValue(objectDB) as List<Recipe>;
                ModQuery.AddRecipes(this.recipes, recipes, plugin.Metadata);
            }
            public bool valid;
            public List<GameObject> items;
            public List<Recipe> recipes;
            public Dictionary<int, GameObject> itemByHash;
        }
    }
    internal class ClassMember
    {
        public List<MemberBase> Members { get; private set; } = new List<MemberBase>();

        public Type Type { get; private set; }

        private ClassMember(Type type, IEnumerable<FieldInfo> fieldInfos, IEnumerable<PropertyInfo> propertyInfos)
        {
            this.Type = type;
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                this.AddMember(new FieldMember(fieldInfo));
            }
            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                this.AddMember(new PropertyMember(propertyInfo));
            }
        }
        private void AddMember(MemberBase member)
        {
            if (!member.IsClass || member.MemberType == typeof(string))
            {
                return;
            }
            if (member.EnumeratedType != null && (!member.IsEnumeratedClass || member.EnumeratedType == typeof(string)))
            {
                return;
            }
            if (member.HasCustomAttribute<NonSerializedAttribute>())
            {
                return;
            }
            this.Members.Add(member);
        }
        private static T[] GetMembersFromType<T>(Type type, Func<Type, T[]> getMembers)
        {
            T[] members = getMembers(type);
            Type baseType = type.BaseType;
            while (baseType != null)
            {
                T[] parentMembers = getMembers(baseType);
                members = members.Union(parentMembers).ToArray<T>();
                baseType = baseType.BaseType;
            }
            return members;
        }
        public static ClassMember GetClassMember(Type type)
        {
            ClassMember classMember;
            if (ClassMember.CachedClassMembers.TryGetValue(type, out classMember))
            {
                return classMember;
            }
            FieldInfo[] fields = ClassMember.GetMembersFromType<FieldInfo>(type, (Type t) => t.GetFields(~BindingFlags.Static));
            PropertyInfo[] properties = ClassMember.GetMembersFromType<PropertyInfo>(type, (Type t) => t.GetProperties(~BindingFlags.Static));
            classMember = new ClassMember(type, fields, properties);
            ClassMember.CachedClassMembers[type] = classMember;
            return classMember;
        }
        private static readonly Dictionary<Type, ClassMember> CachedClassMembers = new Dictionary<Type, ClassMember>();
    }
    internal class FieldMember : MemberBase
    {
        public FieldMember(FieldInfo fieldInfo)
        {
            this.fieldInfo = fieldInfo;
            base.MemberType = fieldInfo.FieldType;
            base.IsUnityObject = base.MemberType.IsSameOrSubclass(typeof(UnityEngine.Object));
            base.IsClass = base.MemberType.IsClass;
            base.HasGetMethod = true;
            base.EnumeratedType = base.MemberType.GetEnumeratedType();
            Type enumeratedType = base.EnumeratedType;
            base.IsEnumerableOfUnityObjects = (enumeratedType != null && enumeratedType.IsSameOrSubclass(typeof(UnityEngine.Object)));
            Type enumeratedType2 = base.EnumeratedType;
            base.IsEnumeratedClass = (enumeratedType2 != null && enumeratedType2.IsClass);
        }
        public override object GetValue(object obj)
        {
            object result;
            try
            {
                result = this.fieldInfo.GetValue(obj);
            }
            catch
            {
                result = null;
            }
            return result;
        }
        public override void SetValue(object obj, object value)
        {
            this.fieldInfo.SetValue(obj, value);
        }
        public override bool HasCustomAttribute<T>()
        {
            return this.fieldInfo.GetCustomAttribute<T>() != null;
        }
        private readonly FieldInfo fieldInfo;
    }
    internal class PropertyMember : MemberBase
    {
        public PropertyMember(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
            base.MemberType = propertyInfo.PropertyType;
            base.IsUnityObject = base.MemberType.IsSameOrSubclass(typeof(UnityEngine.Object));
            base.IsClass = base.MemberType.IsClass;
            base.HasGetMethod = (propertyInfo.GetIndexParameters().Length == 0 && propertyInfo.GetMethod != null);
            base.EnumeratedType = base.MemberType.GetEnumeratedType();
            Type enumeratedType = base.EnumeratedType;
            base.IsEnumerableOfUnityObjects = (enumeratedType != null && enumeratedType.IsSameOrSubclass(typeof(UnityEngine.Object)));
            Type enumeratedType2 = base.EnumeratedType;
            base.IsEnumeratedClass = (enumeratedType2 != null && enumeratedType2.IsClass);
        }
        public override object GetValue(object obj)
        {
            object result;
            try
            {
                result = this.propertyInfo.GetValue(obj);
            }
            catch
            {
                result = null;
            }
            return result;
        }
        public override void SetValue(object obj, object value)
        {
            this.propertyInfo.SetValue(obj, value);
        }
        public override bool HasCustomAttribute<T>()
        {
            return this.propertyInfo.GetCustomAttribute<T>() != null;
        }
        private readonly PropertyInfo propertyInfo;
    }
    internal abstract class MemberBase
    {
        public bool HasGetMethod { get; protected set; }
        public Type MemberType { get; protected set; }
        public Type EnumeratedType { get; protected set; }
        public bool IsUnityObject { get; protected set; }
        public bool IsClass { get; protected set; }
        public bool IsEnumerableOfUnityObjects { get; protected set; }
        public bool IsEnumeratedClass { get; protected set; }
        public abstract object GetValue(object obj);
        public abstract void SetValue(object obj, object value);
        public abstract bool HasCustomAttribute<T>() where T : Attribute;
    }
    public static class CraftingStations
    {
        public static string None
        {
            get
            {
                return string.Empty;
            }
        }
        public static string Workbench
        {
            get
            {
                return "piece_workbench";
            }
        }
        public static string Forge
        {
            get
            {
                return "forge";
            }
        }
        public static string Stonecutter
        {
            get
            {
                return "piece_stonecutter";
            }
        }
        public static string Cauldron
        {
            get
            {
                return "piece_cauldron";
            }
        }
        public static string ArtisanTable
        {
            get
            {
                return "piece_artisanstation";
            }
        }
        public static string BlackForge
        {
            get
            {
                return "blackforge";
            }
        }
        public static string GaldrTable
        {
            get
            {
                return "piece_magetable";
            }
        }
        public static string MeadKetill
        {
            get
            {
                return "piece_MeadCauldron";
            }
        }
        public static string FoodPreparationTable
        {
            get
            {
                return "piece_preptable";
            }
        }
        public static Dictionary<string, string> GetNames()
        {
            return CraftingStations.NamesMap;
        }
        public static AcceptableValueList<string> GetAcceptableValueList()
        {
            return CraftingStations.AcceptableValues;
        }
        public static string GetInternalName(string craftingStation)
        {
            if (string.IsNullOrEmpty(craftingStation))
            {
                return CraftingStations.None;
            }
            string internalName;
            if (CraftingStations.NamesMap.TryGetValue(craftingStation, out internalName))
            {
                return internalName;
            }
            return craftingStation;
        }
        private static readonly Dictionary<string, string> NamesMap = new Dictionary<string, string>
        {
            {
                "None",
                CraftingStations.None
            },
            {
                "Workbench",
                CraftingStations.Workbench
            },
            {
                "Forge",
                CraftingStations.Forge
            },
            {
                "Stonecutter",
                CraftingStations.Stonecutter
            },
            {
                "Cauldron",
                CraftingStations.Cauldron
            },
            {
                "ArtisanTable",
                CraftingStations.ArtisanTable
            },
            {
                "BlackForge",
                CraftingStations.BlackForge
            },
            {
                "GaldrTable",
                CraftingStations.GaldrTable
            },
            {
                "MeadKetill",
                CraftingStations.MeadKetill
            },
            {
                "FoodPreparationTable",
                CraftingStations.FoodPreparationTable
            }
        };
        private static readonly AcceptableValueList<string> AcceptableValues = new AcceptableValueList<string>(CraftingStations.NamesMap.Keys.ToArray<string>());
    }
    public class RequirementConfig
    {
        public string Item { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;
        public int AmountPerLevel { get; set; }
        public bool Recover { get; set; } = true;
        public RequirementConfig()
        {
        }
        public RequirementConfig(string item, int amount, int amountPerLevel = 0, bool recover = true)
        {
            this.Item = item;
            this.Amount = amount;
            this.AmountPerLevel = amountPerLevel;
            this.Recover = recover;
        }
        public Piece.Requirement GetRequirement()
        {
            return new Piece.Requirement
            {
                m_resItem = Mock<ItemDrop>.Create(this.Item),
                m_amount = this.Amount,
                m_amountPerLevel = this.AmountPerLevel,
                m_recover = this.Recover
            };
        }
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(this.Item) && (this.Amount > 0 || this.AmountPerLevel > 0);
        }
    }
    public class RecipeConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;
        public bool Enabled { get; set; } = true;
        public string CraftingStation
        {
            get
            {
                return this.craftingStation;
            }
            set
            {
                this.craftingStation = CraftingStations.GetInternalName(value);
            }
        }
        public string RepairStation
        {
            get
            {
                return this.repairStation;
            }
            set
            {
                this.repairStation = CraftingStations.GetInternalName(value);
            }
        }
        public int MinStationLevel { get; set; } = 1;
        public bool RequireOnlyOneIngredient { get; set; }
        public int QualityResultAmountMultiplier { get; set; } = 1;
        public RequirementConfig[] Requirements { get; set; } = Array.Empty<RequirementConfig>();
        public Piece.Requirement[] GetRequirements()
        {
            List<Piece.Requirement> reqs = new List<Piece.Requirement>();
            foreach (RequirementConfig requirement in this.Requirements)
            {
                if (requirement != null && requirement.IsValid())
                {
                    reqs.Add(requirement.GetRequirement());
                }
            }
            return reqs.ToArray();
        }
        public Recipe GetRecipe()
        {
            if (this.Item == null)
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError("No item set in recipe config");
                return null;
            }
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            string name = this.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = "Recipe_" + this.Item;
            }
            recipe.name = name;
            recipe.m_item = Mock<ItemDrop>.Create(this.Item);
            recipe.m_amount = this.Amount;
            recipe.m_enabled = this.Enabled;
            if (!string.IsNullOrEmpty(this.CraftingStation))
            {
                recipe.m_craftingStation = Mock<global::CraftingStation>.Create(this.CraftingStation);
            }
            if (!string.IsNullOrEmpty(this.RepairStation))
            {
                recipe.m_repairStation = Mock<global::CraftingStation>.Create(this.RepairStation);
            }
            recipe.m_minStationLevel = this.MinStationLevel;
            recipe.m_resources = this.GetRequirements();
            recipe.m_requireOnlyOneIngredient = this.RequireOnlyOneIngredient;
            recipe.m_qualityResultAmountMultiplier = (float)this.QualityResultAmountMultiplier;
            return recipe;
        }
        public void AddRequirement(RequirementConfig requirementConfig)
        {
            if (requirementConfig != null && requirementConfig.IsValid())
            {
                this.Requirements = this.Requirements.AddToArray(requirementConfig);
            }
        }
        public void AddRequirement(string item, int amount, int amountPerLevel = 0)
        {
            this.AddRequirement(new RequirementConfig(item, amount, amountPerLevel, true));
        }
        private string craftingStation = string.Empty;
        private string repairStation = string.Empty;
    }
    public static class ShaderHelper
    {
        public static List<Renderer> GetRenderers(GameObject gameObject)
        {
            List<Renderer> result = new List<Renderer>();
            result.AddRange(gameObject.GetComponentsInChildren<MeshRenderer>(true));
            result.AddRange(gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            return result;
        }
        public static List<Material> GetRendererMaterials(GameObject gameObject)
        {
            List<Material> result = new List<Material>();
            foreach (Renderer randy in ShaderHelper.GetRenderers(gameObject))
            {
                result.AddRange(randy.materials);
            }
            return result;
        }
        public static List<Material> GetRendererSharedMaterials(GameObject gameObject)
        {
            List<Material> result = new List<Material>();
            foreach (Renderer randy in ShaderHelper.GetRenderers(gameObject))
            {
                result.AddRange(randy.sharedMaterials);
            }
            return result;
        }
        public static List<Material> GetAllRendererMaterials(GameObject gameObject)
        {
            List<Material> result = new List<Material>();
            foreach (Renderer randy in ShaderHelper.GetRenderers(gameObject))
            {
                result.AddRange(randy.materials);
                result.AddRange(randy.sharedMaterials);
            }
            return result;
        }
        public static Texture2D CreateScaledTexture(Texture2D texture, int width)
        {
            Texture2D copyTexture = new Texture2D(texture.width, texture.height, texture.format, false);
            copyTexture.SetPixels(texture.GetPixels());
            copyTexture.Apply();
            ShaderHelper.ScaleTexture(copyTexture, width);
            return copyTexture;
        }
        public static void ScaleTexture(Texture2D texture, int width)
        {
            Texture2D copyTexture = new Texture2D(texture.width, texture.height, texture.format, false);
            copyTexture.SetPixels(texture.GetPixels());
            copyTexture.Apply();
            int height = (int)Math.Round((double)((float)width * (float)texture.height / (float)texture.width));
            texture.Reinitialize(width, height);
            texture.Apply();
            Color[] rpixels = texture.GetPixels(0);
            float incX = 1f / (float)width;
            float incY = 1f / (float)height;
            for (int px = 0; px < rpixels.Length; px++)
            {
                rpixels[px] = copyTexture.GetPixelBilinear(incX * ((float)px % (float)width), incY * Mathf.Floor((float)px / (float)width));
            }
            texture.SetPixels(rpixels, 0);
            texture.Apply();
            UnityEngine.Object.Destroy(copyTexture);
        }
    }
    internal static class GUIUtils
    {
        public static bool IsHeadless { get; } = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    }
    public static class TransformExtensions
    {
        public static Transform FindDeepChild(this Transform transform, string childName, Utils.IterativeSearchType searchType = Utils.IterativeSearchType.BreadthFirst)
        {
            return Utils.FindChild(transform, childName, searchType);
        }
    }

}
