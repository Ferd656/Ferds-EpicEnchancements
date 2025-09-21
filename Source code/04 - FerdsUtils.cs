// File: 04 - FerdsUtils.cs
// Target: .NET Framework 4.7.2
using UnityEngine;

namespace FerdEpicEnhancements
{
    public class FerdsUtils
    {
        internal static void Hud(string text)
        {
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null)
            {
                return;
            }
            localPlayer.Message(MessageHud.MessageType.TopLeft, text, 0, null);
        }
        public static ItemDrop ResolveItemDrop(ObjectDB odb, string prefabName)
        {
            GameObject go = null;
            try { go = odb.GetItemPrefab(prefabName); } catch { }
            if (!go)
            {
                try { go = ZNetScene.instance?.GetPrefab(prefabName); } catch { }
            }
            return go ? go.GetComponent<ItemDrop>() : null;
        }
        public static string clean_name(string rawname)
        {
            return rawname.Replace("(Clone)", "").Trim();
        }
    }
    [DisallowMultipleComponent]
    public class InteractForwarder : MonoBehaviour, Interactable, Hoverable
    {
        public Transform SearchRoot;
        private Interactable _i;
        private Hoverable _h;
        private bool _bound;
        private void OnEnable() { StartCoroutine(BindNextFrame()); }
        private System.Collections.IEnumerator BindNextFrame()
        {
            yield return null; // 1 frame defer binding to ensure other scripts are awake
            if (!SearchRoot) SearchRoot = transform.parent;
            TryResolve(SearchRoot);
        }
        private void TryResolve(Transform start)
        {
            if (_bound) return;
            if (_i == null && _h == null)
            {
                var t = start;
                while (t && t.parent)
                {
                    t = t.parent;
                    if (TryBindFromTransform(t)) break;
                }
            }
            _bound = (_i != null || _h != null);
        }
        private bool TryBindFromTransform(Transform t)
        {
            if (!t || t == transform) return false;
            if (t.GetComponent<Tameable>() != null) return false; 
            var monosHere = t.GetComponents<MonoBehaviour>();
            for (int i = 0; i < monosHere.Length; i++)
            {
                var m = monosHere[i];
                if (!m) continue;
                if (m is InteractForwarder) continue;
                var typeName = m.GetType().Name;
                if (!string.IsNullOrEmpty(typeName))
                {
                    var n = typeName.ToLowerInvariant();
                    if (n.Contains("saddle") || n.Contains("mount"))
                    {
                        _i = m as Interactable ?? m.GetComponent<Interactable>();
                        _h = m as Hoverable ?? m.GetComponent<Hoverable>();
                        if (_i != null || _h != null) return true;
                    }
                }
            }
            return false;
        }
        // Interactable
        public bool Interact(Humanoid user, bool hold, bool alt) => _i != null && _i.Interact(user, hold, alt);
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => _i != null && _i.UseItem(user, item);
        // Hoverable
        public string GetHoverName() => _h != null ? SafeName(_h) : "Saddle";
        public string GetHoverText() => _h != null ? SafeText(_h) : "Saddle";
        private string SafeName(Hoverable h) { try { return h.GetHoverName(); } catch { return "Saddle"; } }
        private string SafeText(Hoverable h) { try { return h.GetHoverText(); } catch { return "Saddle"; } }
    }
    internal interface IManager
    {
        void Init();
    }
    public class FerdsDummyClass1
    {
        public static void Init(ZNetScene zNetScene)
        { /*Do nothing*/ }
    }
}
