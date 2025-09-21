// File: 03 - FerdsFireworksLab.cs
// Target: .NET Framework 4.7.2
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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
                float blunt = 50;
                float slash = 0f;
                float chop = 75;
                float pickaxe = 75;
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
                            frost = 300f;
                            array = new string[] { "SE_Frozen_Special_Frd" };
                        }
                    }
                    else
                    {
                        value = 7f;
                        lightning = 350;
                        fire = 150f;
                        array = new string[] { "SE_TJBurnShock" };
                    }
                }
                else
                {
                    value = 4.5f;
                    fire = 500f;
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
        public class SE_Frozen_Special_Frd : SE_Stats
        {
            public static readonly HashSet<string> ImmunityExceptions = new HashSet<string>
            {
                "BLV_OverlordVikingT1", "BLV_OverlordVikingT2", "BLV_OverlordVikingT3", "BLV_OverlordVikingT4", "BLV_OverlordVikingT5",
                "BLV_OverlordVikingT6", "BLV_OverlordVikingT7", "BLV_OverlordVikingT8","BLV_OverlordVikingT6Summoner",
                "BLV_OverlordVikingT7Summoner","BLV_OverlordVikingT8Summoner"
            };
            public static readonly HashSet<string> ImmunePrefabs = new HashSet<string>
            {
                "BlobLava", "BlobFrost", "Troll_Summoned", "Hatchling", "StoneGolem",
                "DModer_Ygg2", "DModer_Ygg3", "DModer_Ygg4_Elder"
            };
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_ttl = 1f;
                // Inmune creatures
                if (character == null || (!ImmunityExceptions.Contains(FerdsUtils.clean_name(character.name)) && (character.IsBoss() || ImmunePrefabs.Contains(FerdsUtils.clean_name(character.name)))))
                {
                    m_ttl = 0f;
                    return;
                }
                var modfs = character.m_damageModifiers;
                if ( // Do not apply for very resistant creatures
                    modfs.m_frost == HitData.DamageModifier.VeryResistant ||
                    modfs.m_frost == HitData.DamageModifier.Immune)
                {
                    m_ttl = 0f;
                    return;
                }
                var seMan = character.GetSEMan();
                var frostSE = ObjectDB.instance.GetStatusEffect("Frost".GetStableHashCode());
                frostSE.m_ttl = 6f;
                var frozenSolidSE = ObjectDB.instance.GetStatusEffect("SE_FrozenSolid".GetStableHashCode());
                frozenSolidSE.m_ttl = 12f;

                if (seMan.GetStatusEffect("SE_FrozenSolid".GetStableHashCode()) == null)
                {
                    if (UnityEngine.Random.value < 0.20f)
                    {
                        character.GetSEMan().AddStatusEffect(frozenSolidSE, false, 0, 0f);
                    }
                    else
                    {
                        character.GetSEMan().AddStatusEffect(frostSE, true, 0, 0f);
                    }
                }
            }
        }
        public class SE_FrozenSolid : SE_Stats
        {
            private GameObject _fxFrozenSolid;
            private GameObject _fxFreezing;
            private SE_Stats _immobilizedSE;
            public static readonly HashSet<string> bigmonsters = new HashSet<string>
            {
                "Lox", "StoneGolem", "Troll", "Troll_Summoned", "FallenValkyrie","Abomination", "GoblinBrute",
                "Gjall", "SeekerBrute", "Morgen", "Serpent", "BonemawSerpent"
            };
            public override void Setup(Character character)
            {
                base.Setup(character);
                m_ttl = 12f;
                var seMan = character.GetSEMan();
                GameObject frozenPrefab = FerdsEpicEnhancementsPlugin.FrozenSolidPrefab;
                Vector3 charPos = character.transform.position;
                Vector3 spawnPos = new Vector3(charPos.x, charPos.y, charPos.z);

                AudioSource.PlayClipAtPoint(FerdsEpicEnhancementsPlugin.FrozenSolidSfx, character.transform.position, 1f);

                _fxFrozenSolid = UnityEngine.Object.Instantiate(
                    frozenPrefab,
                    spawnPos,
                    character.transform.rotation
                );
                _fxFrozenSolid.transform.localScale = _fxFrozenSolid.transform.localScale * 0.1f;
                _fxFrozenSolid.transform.SetParent(character.transform, true);

                string prefabName = FerdsUtils.clean_name(character.name);
                if (bigmonsters.Contains(prefabName))
                {
                    character.StartCoroutine(ScaleOverTime(_fxFrozenSolid, _fxFrozenSolid.transform.localScale * 8f, 1f));
                }
                else
                {
                    character.StartCoroutine(ScaleOverTime(_fxFrozenSolid, _fxFrozenSolid.transform.localScale * 5f, 1f));
                }
                var immobilizedSE = ObjectDB.instance.GetStatusEffect("Immobilized".GetStableHashCode()).Clone();
                immobilizedSE.m_ttl = m_ttl;
                if (immobilizedSE != null)
                {
                    _immobilizedSE = character.GetSEMan().AddStatusEffect(immobilizedSE, false) as SE_Stats;
                }
            }
            private IEnumerator ScaleOverTime(GameObject obj, Vector3 targetScale, float duration)
            {
                if (obj == null) yield break;
                Vector3 initialScale = obj.transform.localScale;
                float timer = 0f;
                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float t = Mathf.Clamp01(timer / duration);
                    obj.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
                    yield return null;
                }
                obj.transform.localScale = targetScale;
            }
            public override void Stop()
            {
                base.Stop();
                if (_fxFrozenSolid != null) UnityEngine.Object.Destroy(_fxFrozenSolid);
                if (_fxFreezing != null) UnityEngine.Object.Destroy(_fxFreezing);
                if (_immobilizedSE != null) _immobilizedSE.Stop();
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
    // Test patch
    //[HarmonyPatch]
    //public static class Patch_Character_Damage_FrozenSolid
    //{
    //    static MethodBase TargetMethod()
    //    {
    //        var type = AccessTools.TypeByName("Character");
    //        return type != null ? AccessTools.Method(type, "Damage", new Type[] { typeof(HitData) }) : null;
    //    }
    //    static void Prefix(Character __instance, HitData hit)
    //    {
    //        if (__instance == null || hit == null) return;
    //        var attackerZDO = hit.m_attacker;
    //        if (attackerZDO == null) return;
    //        var attackerObj = ZDOMan.instance.GetZDO(attackerZDO);
    //        if (attackerObj == null) return;
    //        if (attackerObj.GetPrefab() == "Player".GetStableHashCode())
    //        {
    //            var seMan = __instance.GetSEMan();
    //            var frozenSE = ObjectDB.instance.GetStatusEffect("SE_Frozen_Special_Frd".GetStableHashCode());
    //            if (frozenSE != null)
    //            {
    //                seMan.AddStatusEffect(frozenSE, false, 0, 12f);
    //            }
    //        }
    //    }
    //}
}
