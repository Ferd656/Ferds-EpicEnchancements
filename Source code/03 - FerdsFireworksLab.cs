// File: 03 - FerdsFireworksLab.cs
// Target: .NET Framework 4.7.2
using System;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace FerdEpicEnhancements
{
    public class FerdsFireworksLab
    {
        public class EldrFadmr_Explosion : SE_Stats
        {
            private GameObject _fxInstance;
            FadmrExplosion _explosion = null;
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_ttl = 6f;
                m_tickInterval = 1f;
                m_name = "EldrFadmr_Explosion";
                var vfx = ZNetScene.instance?.GetPrefab("sfx_UndeadBurn_Start");
                if (vfx != null && character != null)
                {
                    var parent = character.GetVisual() != null ? character.GetVisual().transform : character.transform;
                    _fxInstance = UnityEngine.Object.Instantiate(vfx, parent);
                    _fxInstance.transform.localPosition = UnityEngine.Vector3.zero;
                    _fxInstance.transform.localRotation = UnityEngine.Quaternion.identity;
                }
                if (_explosion == null) _explosion = new FadmrExplosion("vfx_Dred_TailSlam");
                _explosion.TrySpawn();
            }
            public override void Stop() { base.Stop(); if (_fxInstance != null) { UnityEngine.Object.Destroy(_fxInstance); } }
        }
        public class VetrFadmr_Explosion : SE_Stats
        {
            private GameObject _fxInstance;
            FadmrExplosion _explosion = null;
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_ttl = 6f;
                m_tickInterval = 1f;
                m_name = "VetrFadmr_Explosion";
                var vfx = ZNetScene.instance?.GetPrefab("vfx_Cold");
                if (vfx != null && character != null)
                {
                    var parent = character.GetVisual() != null ? character.GetVisual().transform : character.transform;
                    _fxInstance = UnityEngine.Object.Instantiate(vfx, parent);
                    _fxInstance.transform.localPosition = UnityEngine.Vector3.zero;
                    _fxInstance.transform.localRotation = UnityEngine.Quaternion.identity;
                }
                if (_explosion == null) _explosion = new FadmrExplosion("vfx_slammajamma_ygg");
                _explosion.TrySpawn();
            }
            public override void Stop() { base.Stop(); if (_fxInstance != null) { UnityEngine.Object.Destroy(_fxInstance); } }
        }
        public class StormrFadmr_Explosion : SE_Stats
        {
            private GameObject _fxInstance;
            FadmrExplosion _explosion = null;
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_ttl = 6f;
                m_tickInterval = 1f;
                m_name = "StormrFadmr_Explosion";
                m_tooltip = "";
                var vfx = ZNetScene.instance?.GetPrefab("fx_Lightning");
                if (vfx != null && character != null)
                {
                    var parent = character.GetVisual() != null ? character.GetVisual().transform : character.transform;
                    _fxInstance = UnityEngine.Object.Instantiate(vfx, parent);
                    _fxInstance.transform.localPosition = UnityEngine.Vector3.zero;
                    _fxInstance.transform.localRotation = UnityEngine.Quaternion.identity;
                }
                if (_explosion == null) _explosion = new FadmrExplosion("fx_DJaw_stomp_Ygg");
                _explosion.TrySpawn();
            }
            public override void Stop() { base.Stop(); if (_fxInstance != null) { UnityEngine.Object.Destroy(_fxInstance); } }
        }
        public class FadmrExplosion
        {
            private readonly string _explosionType;
            private static float AutoDestroyIfLocal = 3f;
            private static readonly int GroundMask = LayerMask.GetMask(new string[] { "Default", "Default_small", "piece", "terrain" });
            public FadmrExplosion(string explosionType)
            {
                _explosionType = explosionType;
            }
            public void TrySpawn()
            {
                ZNetScene instance = ZNetScene.instance;
                if (!instance)
                {
                    FerdsUtils.Hud("ZNetScene not ready.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(_explosionType))
                {
                    FerdsUtils.Hud("PrefabName is empty.");
                    return;
                }
                GameObject prefab = instance.GetPrefab(_explosionType);
                if (!prefab)
                {
                    FerdsEpicEnhancementsPlugin.LogS.LogError($"[FerdsFireworksLab] {prefab} Not found!");
                    return;
                }
                Player localPlayer = Player.m_localPlayer;
                if (!localPlayer)
                {
                    FerdsUtils.Hud("No local player.");
                    return;
                }
                // Camera forward configuration
                Vector3 forward = localPlayer.transform.forward;
                if (Camera.main)
                {
                    forward = Camera.main.transform.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.01f)
                    {
                        forward = localPlayer.transform.forward;
                    }
                    forward.Normalize();
                }
                Vector3 vector = localPlayer.transform.position + forward * 0;
                Quaternion quaternion = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.z).normalized);
                RaycastHit raycastHit;
                if (Physics.Raycast(vector + Vector3.up * 50f, Vector3.down, out raycastHit, 100f, GroundMask))
                {
                    vector = raycastHit.point;
                }
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(prefab, vector, quaternion);
                EffectList component = gameObject.GetComponent<EffectList>();
                if (component != null)
                {
                    try
                    {
                        component.Create(vector, quaternion, null, 1f, -1);
                    }
                    catch
                    {
                    }
                }
                TriggerStartEffects(vector);
                if (AutoDestroyIfLocal > 0f && !gameObject.GetComponent<ZNetView>())
                {
                    UnityEngine.Object.Destroy(gameObject, AutoDestroyIfLocal);
                }
                FerdsUtils.Hud(string.Format("Triggered '{0}' at {1}", _explosionType, vector));
                //FerdsEpicEnhancementsPlugin.LogS.LogInfo(string.Format("Triggered '{0}' at {1}", _explosionType, vector));
            }

            private void TriggerStartEffects(Vector3 center)
            {
                float value = 4f;
                float staggerMultiplier = 2.5f;
                float pushForce = 200f;
                float backstabBonus = 2f;
                float blunt = 70f;
                float slash = 0f;
                float chop = 100f;
                float pickaxe = 100f;
                float fire = 0f;
                float lightning = 0f;
                float frost = 0f;
                string[] array = Array.Empty<string>();
                if (!(_explosionType == "vfx_Dred_TailSlam"))
                {
                    if (!(_explosionType == "fx_DJaw_stomp_Ygg"))
                    {
                        if (_explosionType == "vfx_slammajamma_ygg")
                        {
                            value = 4.5f;
                            frost = 325f;
                            slash = 75;
                            array = new string[] { "Frost" };
                        }
                    }
                    else
                    {
                        value = 7f;
                        lightning = 300;
                        fire = 100f;
                        array = new string[] { "SE_TJBurnShock" };
                    }
                }
                else
                {
                    value = 4.5f;
                    fire = 400f;
                    array = new string[] { "Burning" };
                }
                foreach (Collider collider in Physics.OverlapSphere(center, value))
                {
                    Character component = collider.GetComponent<Character>();
                    if (component != null && component.m_faction != Player.m_localPlayer.m_faction)
                    {
                        HitData hitData = new HitData( pickaxe);
                        hitData.m_damage.m_blunt = blunt;
                        hitData.m_damage.m_slash = slash;
                        hitData.m_damage.m_chop = chop;
                        hitData.m_damage.m_pickaxe = pickaxe;
                        hitData.m_damage.m_fire = fire;
                        hitData.m_damage.m_lightning = lightning;
                        hitData.m_damage.m_frost = frost;
                        hitData.m_staggerMultiplier = staggerMultiplier;
                        hitData.m_pushForce = pushForce;
                        hitData.m_backstabBonus = backstabBonus;
                        hitData.m_point = collider.transform.position;
                        hitData.m_dir = (collider.transform.position - center).normalized;
                        component.Damage(hitData);
                        foreach (string str in array)
                        {
                            StatusEffect statusEffect = ObjectDB.instance.GetStatusEffect(str.GetStableHashCode());
                            if (statusEffect != null)
                            {
                                component.GetSEMan().AddStatusEffect(statusEffect, false, 0, 0f);
                            }
                        }
                    }
                }
            }
        }
        public class SE_TJBurnShock : SE_Stats
        {
            private GameObject _fxInstance;
            private float m_tickTimer = 0f;
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_ttl = 6f;
                m_tickInterval = 1f;
                m_name = "BurnShock";
                var vfx = ZNetScene.instance?.GetPrefab("fx_Lightning");
                if (vfx != null && character != null)
                {
                    var parent = character.GetVisual() != null ? character.GetVisual().transform : character.transform;
                    _fxInstance = UnityEngine.Object.Instantiate(vfx, parent);
                    _fxInstance.transform.localPosition = UnityEngine.Vector3.zero;
                    _fxInstance.transform.localRotation = UnityEngine.Quaternion.identity;
                }
            }
            public override void UpdateStatusEffect(float dt)
            {
                base.UpdateStatusEffect(dt);
                m_tickTimer += dt;
                if (m_tickTimer >= m_tickInterval)
                {
                    m_tickTimer = 0f;
                    if (m_character != null)
                    {
                        HitData hit = new HitData
                        {
                            m_damage = new HitData.DamageTypes { m_fire = 10f, m_lightning = 10f },
                            m_point = m_character.transform.position,
                            m_attacker = m_character.GetZDOID()
                        };
                        m_character.Damage(hit);
                    }
                }
            }
            public override void Stop() { base.Stop(); if (_fxInstance != null) { UnityEngine.Object.Destroy(_fxInstance); } }
        }
        public class BeastLord : SE_Stats
        {
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_name = "BeastLord";
                m_ttl = 0f;
            }
        }
    }
    public class RenegadeBossMusicController : MonoBehaviour
    {
        private bool musicStarted = false;
        void Start()
        {
            // Asegura que MusicManager está instanciado
            if (MusicManager.instance == null)
            {
                var go = new GameObject("MusicManager");
                go.AddComponent<MusicManager>();
                UnityEngine.Object.DontDestroyOnLoad(go);
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] MusicManager inscantced");
            }
            FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController Start in: {gameObject.name}");
        }
        public void TriggerBossMusic()
        {
            if (musicStarted) return;
            musicStarted = true;

            var musicManager = MusicManager.instance;
            if (musicManager != null && FerdsEpicEnhancementsPlugin.RenegadeBossMusic != null)
            {
                //musicManager.StopMusic();
                //musicManager.m_musicSource.clip = FerdsEpicEnhancementsPlugin.RenegadeBossMusic;
                //musicManager.m_musicSource.loop = true;
                //musicManager.m_musicSource.Play();
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] Boss music started!");
            }
            else
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] Couldn't initialize boss music (no MusicManager or AudioClip).");
            }
        }
        void OnDestroy()
        {
            FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] Boss music stopped (MusicMan)");
        }
    }
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager instance;
        public AudioSource m_musicSource;
        private Dictionary<string, AudioClip> musicClips = new Dictionary<string, AudioClip>();
        private Coroutine fadeOutCoroutine;
        void Awake()
        {
            instance = this;
            if (m_musicSource == null)
            {
                m_musicSource = gameObject.AddComponent<AudioSource>();
            }
            m_musicSource.loop = false;
            m_musicSource.playOnAwake = false;

            if (FerdsEpicEnhancementsPlugin.RenegadeBossMusic != null)
            {
                RegisterMusicClip("renegade_boss", FerdsEpicEnhancementsPlugin.RenegadeBossMusic);
            }
            //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] ♫ Music manager instanced!");
        }
        public void RegisterMusicClip(string name, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(name) && clip != null)
            {
                musicClips[name] = clip;
            }
        }
        public void StopMusic(float fadeDuration = 1.5f)
        {
            if (m_musicSource != null && m_musicSource.isPlaying)
            {
                if (fadeOutCoroutine != null)
                    StopCoroutine(fadeOutCoroutine);
                fadeOutCoroutine = StartCoroutine(FadeOutAndStop(fadeDuration));
            }
        }
        private IEnumerator FadeOutAndStop(float duration)
        {
            float startVolume = m_musicSource.volume;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                m_musicSource.volume = Mathf.Lerp(startVolume, 0f, time / duration);
                yield return null;
            }
            m_musicSource.Stop();
            m_musicSource.volume = startVolume; 
            fadeOutCoroutine = null;
        }
        public void TriggerMusic(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (musicClips.TryGetValue(name, out var clip) && clip != null)
            {
                m_musicSource.clip = clip;
                m_musicSource.loop = true; 
                m_musicSource.Play();
                FerdsEpicEnhancementsPlugin.LogS.LogInfo($"Playing: {name}");
            }
            else
            {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"'{name}' not found");
            }
        }
    }
    /*
    ╔════════════════════════════════════╗
    ║ Harmony patches                    ║
    ╚════════════════════════════════════╝
    */
    [HarmonyPatch]
    public static class Patch_OverlordVikingT1_Init
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded ? "OverlordVikingT1" : "FerdsDummyClass1");
            return type != null ? AccessTools.Method(type, "Init") : null;
        }
        static void Postfix(ZNetScene zNetScene)
        {
            if (FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded)
            {
                var method = zNetScene.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var go = method?.Invoke(zNetScene, new object[] { "BLV_OverlordVikingT1" }) as GameObject;
                if (go != null && go.GetComponent<RenegadeBossMusicController>() == null)
                {
                    go.AddComponent<RenegadeBossMusicController>();
                    //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController added to BLV_OverlordVikingT1");
                }
            }
        }
    }
    [HarmonyPatch]
    public static class Patch_OverlordVikingT2_Init
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded ? "OverlordVikingT2" : "FerdsDummyClass1");
            return type != null ? AccessTools.Method(type, "Init") : null;
        }
        static void Postfix(ZNetScene zNetScene)
        {
            if (FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded)
            {
                var method = zNetScene.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var go = method?.Invoke(zNetScene, new object[] { "BLV_OverlordVikingT2" }) as GameObject;
                if (go != null && go.GetComponent<RenegadeBossMusicController>() == null)
                {
                    go.AddComponent<RenegadeBossMusicController>();
                    //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController added to BLV_OverlordVikingT2");
                }
            }
        }
    }
    [HarmonyPatch]
    public static class Patch_OverlordVikingT3_Init
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded ? "OverlordVikingT3" : "FerdsDummyClass1");
            return type != null ? AccessTools.Method(type, "Init") : null;
        }
        static void Postfix(ZNetScene zNetScene)
        {
            if (FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded)
            {
                var method = zNetScene.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var go = method?.Invoke(zNetScene, new object[] { "BLV_OverlordVikingT3" }) as GameObject;
                if (go != null && go.GetComponent<RenegadeBossMusicController>() == null)
                {
                    go.AddComponent<RenegadeBossMusicController>();
                    //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController added to BLV_OverlordVikingT3");
                }
            }
        }
    }
    [HarmonyPatch]
    public static class Patch_OverlordVikingT4_Init
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded ? "OverlordVikingT4" : "FerdsDummyClass1");
            return type != null ? AccessTools.Method(type, "Init") : null;
        }
        static void Postfix(ZNetScene zNetScene)
        {
            if (FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded)
            {
                var method = zNetScene.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var go = method?.Invoke(zNetScene, new object[] { "BLV_OverlordVikingT4" }) as GameObject;
                if (go != null && go.GetComponent<RenegadeBossMusicController>() == null)
                {
                    go.AddComponent<RenegadeBossMusicController>();
                    //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController added to BLV_OverlordVikingT4");
                }
            }
        }
    }
    [HarmonyPatch]
    public static class Patch_OverlordVikingT5_Init
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded ? "OverlordVikingT5" : "FerdsDummyClass1");
            return type != null ? AccessTools.Method(type, "Init") : null;
        }
        static void Postfix(ZNetScene zNetScene)
        {
            if (FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded)
            {
                var method = zNetScene.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var go = method?.Invoke(zNetScene, new object[] { "BLV_OverlordVikingT5" }) as GameObject;
                if (go != null && go.GetComponent<RenegadeBossMusicController>() == null)
                {
                    go.AddComponent<RenegadeBossMusicController>();
                    //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController added to BLV_OverlordVikingT5");
                }
            }
        }
    }
    [HarmonyPatch]
    public static class Patch_OverlordVikingT6_Init
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName(FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded ? "OverlordVikingT6" : "FerdsDummyClass1");
            return type != null ? AccessTools.Method(type, "Init") : null;
        }
        static void Postfix(ZNetScene zNetScene)
        {
            if (FerdsEpicEnhancementsPlugin._wasRenegadeVikingsLoaded)
            {
                var method = zNetScene.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var go = method?.Invoke(zNetScene, new object[] { "BLV_OverlordVikingT6" }) as GameObject;
                if (go != null && go.GetComponent<RenegadeBossMusicController>() == null)
                {
                    go.AddComponent<RenegadeBossMusicController>();
                    //FerdsEpicEnhancementsPlugin.LogS.LogInfo($"[{FerdsEpicEnhancementsPlugin.PluginName}] RenegadeBossMusicController added to BLV_OverlordVikingT6");
                }
            }
        }
    }
    [HarmonyPatch(typeof(MusicMan), "UpdateCurrentMusic")]
    public static class Patch_MusicMan_UpdateCurrentMusic
    {
        private static bool playing_boss_theme = false;
        private static bool renegadebossactive = false;
        static bool Prefix(MusicMan __instance, float dt)
        {
            try
            {
                renegadebossactive = IsRenegadeBossActive();
                if (renegadebossactive == true)
                {
                    var stopMusic = __instance.GetType().GetMethod("StopMusic", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    stopMusic?.Invoke(__instance, null);
                    if  (playing_boss_theme == false)
                    {
                        MusicManager.instance.TriggerMusic("renegade_boss");
                        playing_boss_theme = true;
                    }
                    return false;
                } else if (renegadebossactive == false && playing_boss_theme == true)
                {
                    MusicManager.instance.StopMusic();
                    playing_boss_theme = false;
                }
                return true;
            } catch (Exception e) {
                FerdsEpicEnhancementsPlugin.LogS.LogError($"[{FerdsEpicEnhancementsPlugin.PluginName}] Error handling MusicMan patch: {e}");
                return true;
            }
        }
        static bool IsRenegadeBossActive()
        {
            var localPlayer = Player.m_localPlayer;
            if (localPlayer == null) return false;
            Vector3 playerPos = localPlayer.transform.position;
            const float radius = 35f;
            foreach (var humanoid in UnityEngine.Object.FindObjectsByType<Humanoid>(UnityEngine.FindObjectsSortMode.None))
            {
                if (humanoid == null) continue;
                // Filtra por distancia
                if (Vector3.Distance(humanoid.transform.position, playerPos) > radius) continue;

                // Verifica que el grupo existe y empieza por "OverlordViking"
                if (!string.IsNullOrEmpty(humanoid.m_group) &&
                    humanoid.m_group.StartsWith("OverlordViking", StringComparison.OrdinalIgnoreCase))
                {
                    // Busca el componente BaseAI (o subclase) en el GameObject
                    var baseAI = humanoid.GetComponent("BaseAI");
                    if (baseAI != null)
                    {
                        var isAlertedMethod = baseAI.GetType().GetMethod("IsAlerted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (isAlertedMethod != null)
                        {
                            try
                            {
                                if ((bool)isAlertedMethod.Invoke(baseAI, null))
                                {
                                    var musicController = humanoid.GetComponent("RenegadeBossMusicController");
                                    if (musicController != null)
                                    {
                                        var triggerMethod = musicController.GetType().GetMethod("TriggerBossMusic", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        if (triggerMethod != null)
                                        {
                                            try
                                            {
                                                triggerMethod.Invoke(musicController, null);
                                            }
                                            catch (Exception ex)
                                            {
                                                FerdsEpicEnhancementsPlugin.LogS.LogError($"Error invoking TriggerBossMusic: {ex}");
                                            }
                                        }
                                    }
                                    return true;
                                }
                            }
                            catch {}
                        }
                    }
                }
            }
            return false;
        }
    }
}
