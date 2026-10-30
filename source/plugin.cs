using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Reptile;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Linq;
using UnityEngine.Networking;
using System.Reflection;
using CustomAppAPI;
using System.Globalization;
using System.Text;

namespace BRCDelivery
{
    [BepInPlugin("com.desdezerotapes.brcdelivery", "BRCDelivery", "1.0.0")]
    [BepInDependency("dance.tari.bombrushcyberfunk.customappapi")]
    [BepInDependency("com.Dragsun.BunchOfEmotes", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public string ModFolderPath => Path.GetDirectoryName(Info.Location);

        private readonly Vector3 HiddenMarkerPosition = new Vector3(0f, -100000f, 0f);

        // economy & stats tracking
        private string _saveFilePath;
        private string _timeTrialFilePath;

        [Serializable]
        private class TelemetryCache
        {
            public int cache_id;
        }

        [Serializable]
        private class TimeTrialStats
        {
            public int LifetimeDeliveriesDone;
            public int Best20mDeliveriesRecord;
        }

        // In-memory session tracking for UI 
        private int sessionDeliveriesDone = 0;

        // Message lock timer
        public float messageOverrideTime = 0f;

        // Config entries
        public ConfigEntry<bool> ModEnabled;
        public ConfigEntry<bool> FeeModeEnabled;
        public ConfigEntry<bool> InstakillTimerEnabled;
        public ConfigEntry<bool> InstaBrokeTimerEnable;
        public ConfigEntry<bool> Enable3DPins;
        public ConfigEntry<bool> CurrentStageOnly;
        public ConfigEntry<KeyCode> StartMissionKey;
        public ConfigEntry<KeyCode> CancelDeliveryKey;
        public ConfigEntry<KeyCode> StatsDisplayKey;
        public ConfigEntry<KeyCode> TimeTrialKey;

        public ConfigEntry<string> VictoryDanceName;
        public ConfigEntry<float> VictoryDanceDuration;

        // Audio variables
        private AudioClip customCompletionClip;
        private AudioClip customPickupClip;
        private AudioClip customIncomingClip;
        private AudioClip customCancelClip;
        private AudioClip customHyperfunkClip;
        private Dictionary<string, AudioClip> customItemClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        private AudioSource audioSource;
        private string completionAudioPath;
        private string pickupAudioPath;
        private string incomingAudioPath;
        private string cancelAudioPath;
        private string hyperfunkAudioPath;

        public Texture2D blackTexture;
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        // Delivery state
        private class DeliveryPoint
        {
            public Vector3 Position;
            public string LocationName;
            public int StageID;
        }

        private List<DeliveryPoint> loadedDeliveryPoints = new List<DeliveryPoint>();
        public bool isDelivering = false;
        private bool isPickingUp = false;
        private DeliveryPoint deliveryTargetPoint;
        private DeliveryPoint pickupTargetPoint;
        private string deliveryPackageName = "";
        private float deliveryDistanceTotal;
        private float deliveryStartTime;

        // Timing variables
        private float packageAcquiredTime;
        private bool hurryUpWarningShown = false;

        // Timer variables
        private float deliveryTimer = 0f;
        private bool isTimerActive = false;
        private const float MaxDeliveryTime = 600f; // 10 minutes in seconds

        // Time trial variables
        private bool isTimeTrialActive = false;
        private float timeTrialEndTime = 0f;
        private int timeTrialHyperBoostsEarned = 0;
        private const float TimeTrialDuration = 1200f; // 20 minutes in seconds

        // Map pin variables
        public GameObject pickupMarkerObj;
        public GameObject dropoffMarkerObj;
        private MapPin pickupMapPin;
        private MapPin dropoffMapPin;

        // 3D asset variables
        private GameObject spawnedPickupAsset;
        private GameObject spawnedDropoffAsset;

        // BOE integration variables
        private static Type _boeType = null;
        private static bool _boeSearched = false;

        private List<string> packageNames = new List<string>
        {
            "Cat Pet Supplies", "Jumbo Meal For Two", "Chopu Chopu Chop Suey", "2x Cold Brew Coffee Can",
            "Plant Based Boxed Meal", "Cyberhead Maintance Kit", "Neon Spray Paint Box Set", "Sealed Vinyl Records",
            "Bag of Tools", "Forgotten Cellphone", "Urgent Documents", "Tulp Tech Floral Gift",
            "Pack of Holographic Stickers", "Box of Buggers Shoes", "Forgotten Backpack", "Handmade Pottery Mug",
            "Destronator Comic Magazine", "Oversized Coat", "two (2) Tasty Treats", "Pack of batteries",
            "Grand Bazaar Bag", "Replacement Light Bulb", "Bullseye Dollar Store Bag", "Sport Equipment Duffle Bag",
            "ESTA Audio Plugs", "Collector Action Figure", "Pair of reading glasses", "Sealed box of cereal",
            "Simple USB Drive", "Chilled Energy Drink 6-Pack", "Forgotten Keychains", "Cyber Limbs Oil 24oz",
            "New FLIF CD Album", "3Foot Delivery Package", "Dash Rabbit Delivery Package",
            "Green Bubble Shop Bag", "Super Market Grocery Bag", "Future Fashion Shop Bag",
            "Barado Mini-Synth in its Box", "Flux Store Shop Bag", "Shrimpy Champ Spicy-Hot Big Meal",
            "Portable Videogame Console", "Taco Parade 'Parrillada' Meal", "NNILOS Records Encrypted Invoice",
            "ESTA Hi-Fi Headphones", "Chopu Chopu Noodle Mega Bowl", "Turntable Part"
        };

        private readonly Dictionary<string, string> packageSoundFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Cat Pet Supplies", "item1.wav" },
            { "Jumbo Meal For Two", "item2.wav" },
            { "Chopu Chopu Chop Suey", "item3.wav" },
            { "2x Cold Brew Coffee Can", "item4.wav" },
            { "Plant Based Boxed Meal", "item5.wav" },
            { "Cyberhead Maintance Kit", "item6.wav" },
            { "Neon Spray Paint Box Set", "item7.wav" },
            { "Sealed Vinyl Records", "item8.wav" },
            { "Bag of Tools", "item9.wav" },
            { "Forgotten Cellphone", "item10.wav" }
        };

        private FieldInfo animField;
        private FieldInfo dieAbilityField;

        private void Awake()
        {
            Instance = this;

            // Configs
            ModEnabled = Config.Bind("General", "Enabled", true, "Enable the BRC Delivery mode.");
            FeeModeEnabled = Config.Bind("BombrushDelivery", "FeeModeEnabled", true, "If True, enables time limits, late fees, cancellation fees, and speed bonuses.");
            InstakillTimerEnabled = Config.Bind("BombrushDelivery", "InstakillTimerEnabled", true, "If True, failing the delivery timer will instantly kill the player.");
            InstaBrokeTimerEnable = Config.Bind("BombrushDelivery", "InstaBrokeTimerEnable", false, "If True, failing the 10 minute mark sets your HyperBoost JSON wallet to 0.");
            Enable3DPins = Config.Bind("BombrushDelivery", "Enable3DPins", true, "If True, map pins (UI) for pickup/dropoff will be shown on the minimap/phone.");
            CurrentStageOnly = Config.Bind("BombrushDelivery", "CurrentStageOnly", false, "If True, picking up and dropping off locations will only occur within the current stage to avoid loading screens.");
            StartMissionKey = Config.Bind("BombrushDelivery", "StartKey", KeyCode.Keypad1, "Key to start a new delivery mission.");
            CancelDeliveryKey = Config.Bind("BombrushDelivery", "CancelKey", KeyCode.Keypad3, "Key to cancel the current delivery mission.");
            StatsDisplayKey = Config.Bind("BombrushDelivery", "StatsDisplayKey", KeyCode.Keypad2, "Key to display lifetime stats.");
            TimeTrialKey = Config.Bind("BombrushDelivery", "TimeTrialKey", KeyCode.Keypad4, "Key to start a 20-minute Time Trial.");

            VictoryDanceName = Config.Bind("BombrushDelivery", "VictoryDance", "dance3", "The name of the animation clip to play on delivery completion. Supports Bunch of Emotes.");
            VictoryDanceDuration = Config.Bind("BombrushDelivery", "VictoryDanceDuration", 6.0f, "Duration in seconds player is disabled while dancing.");

            string pluginRoot = BepInEx.Paths.ConfigPath;
            string deliveryRoot = Path.Combine(pluginRoot, "..", "plugins", "BRCDelivery");
            string soundsDir = Path.Combine(deliveryRoot, "Sounds");

            completionAudioPath = Path.Combine(soundsDir, "delivery_complete.wav");
            pickupAudioPath = Path.Combine(soundsDir, "pickup_complete.wav");
            incomingAudioPath = Path.Combine(soundsDir, "incoming_notification.wav");
            cancelAudioPath = Path.Combine(soundsDir, "cancel_delivery.wav");
            hyperfunkAudioPath = Path.Combine(soundsDir, "delivery_complete2.wav");

            if (ModEnabled.Value)
            {
                blackTexture = CreateBlackTexture();

                try
                {
                    Harmony.CreateAndPatchAll(typeof(MapControllerPatch));
                    Logger.LogInfo("BRCDelivery patches applied successfully.");
                }
                catch (Exception e)
                {
                    Logger.LogError($"[Harmony Error] Patch failed to apply: {e.Message}");
                }

                // Setup mod file paths
                string hyperBoostFolder = Path.Combine(Paths.PluginPath, "HyperBoostMod");
                if (!Directory.Exists(hyperBoostFolder)) Directory.CreateDirectory(hyperBoostFolder);
                _saveFilePath = Path.Combine(hyperBoostFolder, "scorecount.json");

                string brcDeliveryFolder = Path.Combine(Paths.PluginPath, "BRCDelivery");
                if (!Directory.Exists(brcDeliveryFolder)) Directory.CreateDirectory(brcDeliveryFolder);
                _timeTrialFilePath = Path.Combine(brcDeliveryFolder, "timetrial_score.json");

                LoadDeliveryAssets();

                if (pickupMarkerObj == null)
                {
                    pickupMarkerObj = new GameObject("BombrushDeliveryPickupMarker");
                    DontDestroyOnLoad(pickupMarkerObj);
                    pickupMarkerObj.transform.position = HiddenMarkerPosition;
                }

                if (dropoffMarkerObj == null)
                {
                    dropoffMarkerObj = new GameObject("BombrushDeliveryDropoffMarker");
                    DontDestroyOnLoad(dropoffMarkerObj);
                    dropoffMarkerObj.transform.position = HiddenMarkerPosition;
                }

                audioSource = gameObject.AddComponent<AudioSource>();
                StartCoroutine(LoadAudioClips());
                LoadDeliveryLocations();

                Type playerType = typeof(Player);
                animField = playerType.GetField("anim", BindingFlags.NonPublic | BindingFlags.Instance);
                dieAbilityField = playerType.GetField("dieAbility", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                Logger.LogInfo("BRCDelivery loaded with Overhauled Stats and Time Trial JSON Tracking.");
            }
        }

        // JSON helper methods
        private TelemetryCache LoadData()
        {
            if (!File.Exists(_saveFilePath)) return new TelemetryCache { cache_id = 0 };
            try
            {
                string b64 = File.ReadAllText(_saveFilePath);
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                return JsonUtility.FromJson<TelemetryCache>(json) ?? new TelemetryCache();
            }
            catch
            {
                return new TelemetryCache { cache_id = 0 };
            }
        }

        private void SaveData(TelemetryCache data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                File.WriteAllText(_saveFilePath, b64);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save cache: {ex.Message}");
            }
        }

        private TimeTrialStats LoadTimeTrialStats()
        {
            if (!File.Exists(_timeTrialFilePath)) return new TimeTrialStats { LifetimeDeliveriesDone = 0, Best20mDeliveriesRecord = 0 };
            try
            {
                string json = File.ReadAllText(_timeTrialFilePath);
                return JsonUtility.FromJson<TimeTrialStats>(json) ?? new TimeTrialStats();
            }
            catch
            {
                return new TimeTrialStats();
            }
        }

        private void SaveTimeTrialStats(TimeTrialStats stats)
        {
            try
            {
                string json = JsonUtility.ToJson(stats, true);
                File.WriteAllText(_timeTrialFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save time trial stats: {ex.Message}");
            }
        }

        // BOE HELPER METHODS
        private static Type GetBoEType()
        {
            if (!_boeSearched)
            {
                _boeSearched = true;
                if (Chainloader.PluginInfos.TryGetValue("com.Dragsun.BunchOfEmotes", out var pluginInfo))
                {
                    if (pluginInfo != null && pluginInfo.Instance != null)
                    {
                        _boeType = pluginInfo.Instance.GetType();
                    }
                }
            }
            return _boeType;
        }

        private int GetBoEStateHash(string emoteName)
        {
            Type boe = GetBoEType();
            if (boe == null) return 0;

            FieldInfo field = boe.GetField("myCustomAnims2", BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                var dict = field.GetValue(null) as System.Collections.IDictionary;
                if (dict != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        if (entry.Value is string animName && string.Equals(animName, emoteName, StringComparison.OrdinalIgnoreCase))
                        {
                            return (int)entry.Key;
                        }
                    }
                }
            }
            return 0;
        }

        private void SetupAsset(GameObject asset)
        {
            if (asset == null) return;
            asset.transform.parent = null;

            Transform authKey = asset.transform.Find("AuthKey");
            if (authKey != null)
            {
                authKey.localPosition = Vector3.zero;
                authKey.localRotation = Quaternion.identity;
            }

            var animators = asset.GetComponentsInChildren<Animator>(true);
            foreach (var anim in animators)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.enabled = true;
                anim.Update(0f);
            }

            var animations = asset.GetComponentsInChildren<Animation>(true);
            foreach (var anim in animations)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.playAutomatically = true;
                anim.enabled = true;
                if (anim.clip != null) anim.Play(anim.clip.name);
            }

            var particleSystems = asset.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.playOnAwake = true;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                ps.Play();
            }
        }

        private GameObject LoadAndInstantiateBundle(string bundlePath, string instanceName)
        {
            if (!File.Exists(bundlePath)) return null;

            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null) return null;

            GameObject prefab = null;
            string[] assetNames = bundle.GetAllAssetNames();
            string prefabName = assetNames.FirstOrDefault(x => x.EndsWith(".prefab"));

            if (!string.IsNullOrEmpty(prefabName))
            {
                prefab = bundle.LoadAsset<GameObject>(prefabName);
            }
            else
            {
                var assets = bundle.LoadAllAssets<GameObject>();
                prefab = assets.FirstOrDefault(a => a.transform.parent == null && (a.GetComponentInChildren<Animator>() != null || a.GetComponentInChildren<Animation>() != null || a.GetComponentInChildren<Light>() != null))
                         ?? (assets.Length > 0 ? assets[0] : null);
            }

            GameObject instance = null;
            if (prefab != null)
            {
                instance = Instantiate(prefab);
                instance.name = instanceName;
                DontDestroyOnLoad(instance);
                SetupAsset(instance);
                instance.SetActive(false);
            }
            bundle.Unload(false);
            return instance;
        }

        private void LoadDeliveryAssets()
        {
            string assetFolder = Path.Combine(Paths.BepInExRootPath, "BRCDeliveryAssets");
            string pickupBundlePath = Path.Combine(assetFolder, "BRCDeliveryBox", "brcdeliverybox.cbb");
            string dropoffBundlePath = Path.Combine(assetFolder, "BRCDeliveryDropoff", "brcdeliverydropoff.cbb");

            if (spawnedPickupAsset == null) spawnedPickupAsset = LoadAndInstantiateBundle(pickupBundlePath, "BRCPickupAsset_Instance");
            if (spawnedDropoffAsset == null) spawnedDropoffAsset = LoadAndInstantiateBundle(dropoffBundlePath, "BRCDropoffAsset_Instance");
        }

        private IEnumerator LoadAudioClips()
        {
            yield return StartCoroutine(LoadSingleAudioClip(completionAudioPath, clip => customCompletionClip = clip, "completion"));
            yield return StartCoroutine(LoadSingleAudioClip(pickupAudioPath, clip => customPickupClip = clip, "pickup"));
            yield return StartCoroutine(LoadSingleAudioClip(incomingAudioPath, clip => customIncomingClip = clip, "incoming notification"));
            yield return StartCoroutine(LoadSingleAudioClip(cancelAudioPath, clip => customCancelClip = clip, "cancel delivery"));
            yield return StartCoroutine(LoadSingleAudioClip(hyperfunkAudioPath, clip => customHyperfunkClip = clip, "hyperfunk bonus"));

            foreach (var kvp in packageSoundFileMap)
            {
                string soundFileName = kvp.Value;
                string soundPath = Path.Combine(Path.GetDirectoryName(completionAudioPath), soundFileName);
                yield return StartCoroutine(LoadSingleAudioClip(soundPath, clip => { if (clip != null) customItemClips[kvp.Key] = clip; }, soundFileName));
            }
        }

        private IEnumerator LoadSingleAudioClip(string path, Action<AudioClip> setter, string clipName)
        {
            if (!File.Exists(path)) yield break;

            string uri = new Uri(path).AbsoluteUri;
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    setter(DownloadHandlerAudioClip.GetContent(www));
                }
            }
        }

        [HarmonyPatch(typeof(Mapcontroller), "CreatePins")]
        public static class MapControllerPatch
        {
            public static void Postfix(Mapcontroller __instance)
            {
                if (Plugin.Instance != null && Plugin.Instance.ModEnabled.Value)
                {
                    Plugin.Instance.CreateBombrushDeliveryMapPins(__instance);
                }
            }
        }

        public void CreateBombrushDeliveryMapPins(Mapcontroller mapController)
        {
            try
            {
                FieldInfo pinPrefabField = typeof(Mapcontroller).GetField("m_GraffitiPinPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo pinsGroupField = typeof(Mapcontroller).GetField("m_PinsGroup", BindingFlags.NonPublic | BindingFlags.Instance);
                if (pinPrefabField == null || pinsGroupField == null) return;

                GameObject graffitiPrefab = (GameObject)pinPrefabField.GetValue(mapController);
                Transform pinsGroup = (Transform)pinsGroupField.GetValue(mapController);

                if (graffitiPrefab != null && pinsGroup != null)
                {
                    if (pickupMapPin == null)
                    {
                        GameObject pObj = Instantiate(graffitiPrefab, pinsGroup);
                        pickupMapPin = pObj.GetComponent<MapPin>();
                        if (pickupMapPin != null)
                        {
                            pickupMapPin.SetMapController(mapController);
                            pickupMapPin.AssignGameplayEvent(pickupMarkerObj);
                            pickupMapPin.InitMapPin(MapPin.PinType.StoryObjectivePin);
                            pObj.SetActive(false);
                        }
                    }

                    if (dropoffMapPin == null)
                    {
                        GameObject dObj = Instantiate(graffitiPrefab, pinsGroup);
                        dropoffMapPin = dObj.GetComponent<MapPin>();
                        if (dropoffMapPin != null)
                        {
                            dropoffMapPin.SetMapController(mapController);
                            dropoffMapPin.AssignGameplayEvent(dropoffMarkerObj);
                            dropoffMapPin.InitMapPin(MapPin.PinType.StoryObjectivePin);
                            dObj.SetActive(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[MapPatch] Error creating custom pins: {e.Message}");
            }
        }

        private void Update()
        {
            if (!ModEnabled.Value) return;

            var player = WorldHandler.instance?.GetCurrentPlayer();

            if (Input.GetKeyDown(StartMissionKey.Value))
            {
                if (player != null) StartDeliveryMission();
                return;
            }

            if (isDelivering && Input.GetKeyDown(CancelDeliveryKey.Value))
            {
                if (player != null) CancelDelivery(player);
                return;
            }

            if (Input.GetKeyDown(StatsDisplayKey.Value))
            {
                if (player != null) ShowStats(player);
                return;
            }

            if (Input.GetKeyDown(TimeTrialKey.Value))
            {
                if (player != null)
                {
                    if (!isTimeTrialActive)
                    {
                        isTimeTrialActive = true;
                        timeTrialEndTime = Time.time + TimeTrialDuration;
                        timeTrialHyperBoostsEarned = 0;
                        sessionDeliveriesDone = 0;

                        if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);

                        ShowMessage("<color=#FFEE00>20 MIN TIME TRIAL STARTED!</color> Go Fast!", 3f);
                        messageOverrideTime = Time.time + 3.0f;
                    }
                    else
                    {
                        float remaining = timeTrialEndTime - Time.time;
                        int minutes = Mathf.FloorToInt(remaining / 60F);
                        if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);

                        ShowMessage($"Time Trial Active: {minutes} minutes remaining. Current Deliveries: {sessionDeliveriesDone}", 3f);
                        messageOverrideTime = Time.time + 3.0f;
                    }
                }
                return;
            }

            if (player == null) return;

            // Timer logic combining InstaKill and InstaBroke
            if (isDelivering && !isPickingUp && isTimerActive && (InstakillTimerEnabled.Value || InstaBrokeTimerEnable.Value))
            {
                if (Reptile.Core.Instance != null && Reptile.Core.Instance.BaseModule != null && !Reptile.Core.Instance.BaseModule.IsLoading)
                {
                    if (!player.IsDead())
                    {
                        deliveryTimer -= Time.deltaTime;
                        if (deliveryTimer <= 0f)
                        {
                            deliveryTimer = 0f;
                            isTimerActive = false;

                            if (InstakillTimerEnabled.Value && dieAbilityField != null)
                            {
                                Reptile.Ability dieAb = dieAbilityField.GetValue(player) as Reptile.Ability;
                                if (dieAb != null) player.ActivateAbility(dieAb);
                            }

                            if (InstaBrokeTimerEnable.Value)
                            {
                                TelemetryCache data = LoadData();
                                data.cache_id = 0;
                                SaveData(data);
                            }

                            CancelDelivery(player);
                            ShowMessage("<color=#FF0000>TIME OVER! CLIENT CANCELLED THE DELIVERY!</color>", 4f);
                            return;
                        }
                    }
                }
            }

            if (isTimeTrialActive && Time.time >= timeTrialEndTime)
            {
                isTimeTrialActive = false;
                TimeTrialStats stats = LoadTimeTrialStats();
                string finishMsg = $"<color=#FFEE00>TIME TRIAL FINISHED!</color> Result: <color=#76EE00>{sessionDeliveriesDone}</color> Deliveries Completed!";

                if (sessionDeliveriesDone > stats.Best20mDeliveriesRecord)
                {
                    stats.Best20mDeliveriesRecord = sessionDeliveriesDone;
                    SaveTimeTrialStats(stats);
                    finishMsg += " <color=#FFD700>NEW DELIVERIES RECORD!</color>";
                }
                if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);

                ShowMessage(finishMsg, 10f);
                messageOverrideTime = Time.time + 10.0f;
                return;
            }

            if (Time.time < messageOverrideTime || !isDelivering) return;

            // Hurry Up! warning at 7.5 minutes elapsed (450 seconds)
            if (FeeModeEnabled.Value && !isPickingUp && !hurryUpWarningShown)
            {
                if ((Time.time - packageAcquiredTime) >= 450f)
                {
                    hurryUpWarningShown = true;
                    if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);
                    DisplayDistance($"<mark=#48225C88><color=#C8A2C8>HURRY UP! 7.5 MINS ELAPSED! LATE FEE IMMINENT!</color></mark>", 4f);
                    messageOverrideTime = Time.time + 4.0f;
                    return;
                }
            }

            Reptile.Stage currentStage = Reptile.Core.Instance?.BaseModule?.CurrentStage ?? Reptile.Stage.NONE;
            int currentStageId = (int)currentStage;
            DeliveryPoint currentTarget = isPickingUp ? pickupTargetPoint : deliveryTargetPoint;
            if (currentTarget == null) return;

            string timerUI = "";
            if (!isPickingUp && isTimerActive && (InstakillTimerEnabled.Value || InstaBrokeTimerEnable.Value))
            {
                int mins = Mathf.FloorToInt(deliveryTimer / 60f);
                int secs = Mathf.FloorToInt(deliveryTimer % 60f);
                string timeStr = string.Format("{0:00}:{1:00}", mins, secs);

                string hurryStr = ((Time.time - packageAcquiredTime) >= 450f) ? " <mark=#48225C88><color=#C8A2C8>HURRY UP!</color></mark>" : "";
                timerUI = $"\n<mark=#00000088><color=#FFA500>{timeStr} min</color></mark>{hurryStr}";
            }

            if (currentStageId == currentTarget.StageID)
            {
                float dist = Vector3.Distance(player.transform.position, currentTarget.Position);
                if (isPickingUp)
                    DisplayDistance($"<color=#33CCFF>PICKUP:</color> <color=#AAFFDD>{currentTarget.LocationName}</color>. Distance: {Mathf.RoundToInt(dist)}m", 0.1f);
                else
                    DisplayDistance($"<color=#33CCFF>DELIVERING</color> <color=#FFFFAD>{deliveryPackageName}</color>! <color=#33CCFF>Dropoff:</color> <color=#AAFFDD>{currentTarget.LocationName}</color>. Distance: {Mathf.RoundToInt(dist)}m{timerUI}", 0.1f);

                if (dist < 0.52f)
                {
                    if (isPickingUp) CompletePickup(player);
                    else CompleteDelivery(player);
                }
            }
            else
            {
                string phase = isPickingUp ? "<color=#33CCFF>PICKUP</color>" : $"<color=#33CCFF>DROPOFF</color> (<color=#FFFFAD>{deliveryPackageName}</color>)";
                DisplayDistance($"{phase}: Go to <color=#AAFFDD>{currentTarget.LocationName}</color>{timerUI}", 1.0f);
            }
        }

        public void ShowStats(Player player)
        {
            if (player == null) return;

            TimeTrialStats stats = LoadTimeTrialStats();
            string message = "";

            if (isTimeTrialActive)
            {
                float remaining = timeTrialEndTime - Time.time;
                int mins = Mathf.FloorToInt(remaining / 60F);
                int secs = Mathf.FloorToInt(remaining % 60F);
                string timeStr = string.Format("{0:00}:{1:00}", mins, secs);

                message = $"<color=#5555FF>Stats:</color> DONE IN SESSION: <color=#AAFFDD>{sessionDeliveriesDone}</color> | Session HB Earned: <color=#AAFFDD>{timeTrialHyperBoostsEarned}</color> | Time Remaining: <color=#FFA500>{timeStr}</color> | TIME TRIAL DELIVERIES DONE RECORD: <color=#FFD700>{stats.Best20mDeliveriesRecord}</color>";
            }
            else
            {
                message = $"<color=#5555FF>Stats:</color> DONE DELIVERIES LIFETIME: <color=#AAFFDD>{stats.LifetimeDeliveriesDone}</color> | TIME TRIAL DELIVERIES DONE RECORD: <color=#FFD700>{stats.Best20mDeliveriesRecord}</color>";
            }

            if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);

            messageOverrideTime = Time.time + 3.0f;
            player.ShowInformation(message, 3f);
        }

        public void CancelDelivery(Player player)
        {
            if (!isDelivering)
            {
                player.ShowInformation("No active delivery to cancel.", 2f);
                return;
            }
            isDelivering = false;
            isPickingUp = false;
            isTimerActive = false;
            pickupTargetPoint = null;
            deliveryTargetPoint = null;
            ResetMapMarkers();

            string cancelMsg = "<color=#FF4444>DELIVERY CANCELLED.</color>";
            if (FeeModeEnabled.Value)
            {
                int penalty = -200;
                TelemetryCache data = LoadData();
                data.cache_id += penalty;
                if (data.cache_id < 0) data.cache_id = 0;
                SaveData(data);

                cancelMsg += " | <color=#FF0000>—200 HyperBoosts Discounted</color>";
                if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);
            }
            else
            {
                if (customCancelClip != null && audioSource != null) audioSource.PlayOneShot(customCancelClip);
            }

            messageOverrideTime = Time.time + 4.0f;
            player.ShowInformation(cancelMsg, 4f);
        }

        private void LateUpdate()
        {
            if (ModEnabled.Value)
            {
                UpdateMapPinVisibility();
                UpdateAssetVisibility();
            }
        }

        private void UpdateMapPinVisibility()
        {
            Reptile.Stage currentStage = Reptile.Core.Instance?.BaseModule?.CurrentStage ?? Reptile.Stage.NONE;
            int currentStageId = (int)currentStage;
            bool globalPinsEnabled = Enable3DPins.Value;

            if (pickupMapPin != null && pickupMapPin.gameObject != null)
            {
                bool shouldShow = globalPinsEnabled && isDelivering && isPickingUp && pickupTargetPoint != null && pickupTargetPoint.StageID == currentStageId;
                if (pickupMapPin.gameObject.activeSelf != shouldShow) pickupMapPin.gameObject.SetActive(shouldShow);
            }

            if (dropoffMapPin != null && dropoffMapPin.gameObject != null)
            {
                bool shouldShow = globalPinsEnabled && isDelivering && !isPickingUp && deliveryTargetPoint != null && deliveryTargetPoint.StageID == currentStageId;
                if (dropoffMapPin.gameObject.activeSelf != shouldShow) dropoffMapPin.gameObject.SetActive(shouldShow);
            }
        }

        private void UpdateAssetVisibility()
        {
            Reptile.Stage currentStage = Reptile.Core.Instance?.BaseModule?.CurrentStage ?? Reptile.Stage.NONE;
            int currentStageId = (int)currentStage;

            if (spawnedPickupAsset != null)
            {
                bool shouldShow = isDelivering && isPickingUp && pickupTargetPoint != null && pickupTargetPoint.StageID == currentStageId;
                if (shouldShow)
                {
                    spawnedPickupAsset.transform.position = pickupTargetPoint.Position;
                    if (!spawnedPickupAsset.activeSelf) spawnedPickupAsset.SetActive(true);
                }
                else
                {
                    if (spawnedPickupAsset.activeSelf) spawnedPickupAsset.SetActive(false);
                }
            }

            if (spawnedDropoffAsset != null)
            {
                bool shouldShow = isDelivering && !isPickingUp && deliveryTargetPoint != null && deliveryTargetPoint.StageID == currentStageId;
                if (shouldShow)
                {
                    spawnedDropoffAsset.transform.position = deliveryTargetPoint.Position;
                    if (!spawnedDropoffAsset.activeSelf) spawnedDropoffAsset.SetActive(true);
                }
                else
                {
                    if (spawnedDropoffAsset.activeSelf) spawnedDropoffAsset.SetActive(false);
                }
            }
        }

        private void ResetMapMarkers()
        {
            if (pickupMarkerObj != null) pickupMarkerObj.transform.position = HiddenMarkerPosition;
            if (dropoffMarkerObj != null) dropoffMarkerObj.transform.position = HiddenMarkerPosition;
            if (pickupMapPin != null && pickupMapPin.gameObject != null) pickupMapPin.gameObject.SetActive(false);
            if (dropoffMapPin != null && dropoffMapPin.gameObject != null) dropoffMapPin.gameObject.SetActive(false);

            if (spawnedPickupAsset != null) spawnedPickupAsset.SetActive(false);
            if (spawnedDropoffAsset != null) spawnedDropoffAsset.SetActive(false);
        }

        private int GetStageIDFromFilename(string filename)
        {
            string upperFilename = filename.ToUpper();
            if (upperFilename.Contains("[MILLENNIUMSQUARE]") || upperFilename.Contains("[MILLENIUMSQUARE]") || upperFilename.Contains("[SQUARE]")) return (int)Reptile.Stage.square;
            if (upperFilename.Contains("[VERSUMHILL]") || upperFilename.Contains("[VERSUM]") || upperFilename.Contains("[DOWNHILL]")) return (int)Reptile.Stage.downhill;
            if (upperFilename.Contains("[HIDEOUT]")) return (int)Reptile.Stage.hideout;
            if (upperFilename.Contains("[MILLENIUMMALL]") || upperFilename.Contains("[MALL]")) return 6;
            if (upperFilename.Contains("[MATAAN]") || upperFilename.Contains("[BASE]")) return 7;
            if (upperFilename.Contains("[PRELUDE]")) return 8;
            if (upperFilename.Contains("[PYRAMIDISLAND]") || upperFilename.Contains("[PYRAMIDS]")) return (int)Reptile.Stage.pyramid;
            if (upperFilename.Contains("[BRINKTERMINAL]") || upperFilename.Contains("[BRINK]") || upperFilename.Contains("[TOWER]")) return (int)Reptile.Stage.tower;

            if (upperFilename.Contains("[ID4]")) return 4;
            if (upperFilename.Contains("[ID3]")) return 3;
            if (upperFilename.Contains("[ID5]")) return 5;
            if (upperFilename.Contains("[ID6]")) return 6;
            if (upperFilename.Contains("[ID7]")) return 7;
            if (upperFilename.Contains("[ID8]")) return 8;
            if (upperFilename.Contains("[ID9]")) return 9;
            if (upperFilename.Contains("[ID11]")) return 11;
            if (upperFilename.Contains("[ID12]")) return 12;

            return -1;
        }

        private void LoadDeliveryLocations()
        {
            loadedDeliveryPoints.Clear();
            string locationsRoot = Path.Combine(Paths.ConfigPath, "Locations");
            if (!Directory.Exists(locationsRoot)) return;

            string[] files = Directory.GetFiles(locationsRoot, "*.txt", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                try
                {
                    string locationName = Path.GetFileNameWithoutExtension(file);
                    int stageId = GetStageIDFromFilename(locationName);
                    if (stageId == -1) continue;

                    string[] lines = File.ReadAllLines(file);
                    if (lines.Length > 0)
                    {
                        string[] parts = lines[0].Split('|');
                        if (parts.Length > 0)
                        {
                            string[] posParts = parts[0].Split(',');
                            if (posParts.Length == 3 && float.TryParse(posParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                                float.TryParse(posParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                                float.TryParse(posParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                            {
                                loadedDeliveryPoints.Add(new DeliveryPoint { Position = new Vector3(x, y, z), LocationName = locationName, StageID = stageId });
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to parse location file {file}: {e.Message}");
                }
            }
        }

        public void StartDeliveryMission()
        {
            if (loadedDeliveryPoints.Count < 2)
            {
                ShowMessage("Need at least 2 locations to start a two-path mission!", 3f);
                return;
            }

            var player = WorldHandler.instance?.GetCurrentPlayer();
            if (player == null) return;

            if (isDelivering) ResetMapMarkers();

            Reptile.Stage currentStage = Reptile.Core.Instance?.BaseModule?.CurrentStage ?? Reptile.Stage.NONE;
            int currentStageId = (int)currentStage;

            List<DeliveryPoint> potentialPickups;
            if (CurrentStageOnly.Value && currentStageId != (int)Reptile.Stage.NONE)
            {
                potentialPickups = loadedDeliveryPoints.Where(p => p.StageID == currentStageId).ToList();
            }
            else
            {
                potentialPickups = loadedDeliveryPoints.ToList();
            }

            if (potentialPickups.Count == 0)
            {
                ShowMessage("No valid pickup locations found for current stage constraints!", 3f);
                return;
            }

            pickupTargetPoint = potentialPickups[UnityEngine.Random.Range(0, potentialPickups.Count)];

            List<DeliveryPoint> potentialDropoffs;
            if (CurrentStageOnly.Value && currentStageId != (int)Reptile.Stage.NONE)
            {
                potentialDropoffs = loadedDeliveryPoints.Where(p => p.StageID == currentStageId && p.Position != pickupTargetPoint.Position).ToList();
            }
            else
            {
                potentialDropoffs = loadedDeliveryPoints.Where(p => p.Position != pickupTargetPoint.Position || p.StageID != pickupTargetPoint.StageID).ToList();
            }

            if (potentialDropoffs.Count == 0)
            {
                ShowMessage("No valid dropoff locations found for current stage constraints!", 3f);
                return;
            }

            deliveryTargetPoint = potentialDropoffs[UnityEngine.Random.Range(0, potentialDropoffs.Count)];
            deliveryPackageName = packageNames[UnityEngine.Random.Range(0, packageNames.Count)];
            isDelivering = true;
            isPickingUp = true;
            deliveryStartTime = Time.time;
            hurryUpWarningShown = false;

            deliveryDistanceTotal = Vector3.Distance(player.transform.position, pickupTargetPoint.Position) + Vector3.Distance(pickupTargetPoint.Position, deliveryTargetPoint.Position);

            if (pickupMarkerObj != null && pickupMapPin != null)
            {
                pickupMarkerObj.transform.position = pickupTargetPoint.Position;
                pickupMapPin.InitMapPin(MapPin.PinType.StoryObjectivePin);
                pickupMapPin.AssignGameplayEvent(pickupMarkerObj);
                pickupMapPin.enabled = false;
                pickupMapPin.enabled = true;
            }

            string message = (currentStageId == pickupTargetPoint.StageID) ?
                $"<color=#33CCFF>NEW ORDER (Local Pickup):</color> Go to <color=#33CCFF>PICKUP:</color> <color=#AAFFDD>{pickupTargetPoint.LocationName}</color>" :
                $"<color=#33CCFF>NEW ORDER (Remote Pickup):</color> Go to <color=#33CCFF>PICKUP:</color> <color=#AAFFDD>{pickupTargetPoint.LocationName}</color>";

            if (customIncomingClip != null && audioSource != null) audioSource.PlayOneShot(customIncomingClip);
            messageOverrideTime = Time.time + 5.0f;
            player.ShowInformation(message, 5f);
        }

        private void CompletePickup(Player player)
        {
            isPickingUp = false;
            packageAcquiredTime = Time.time;

            deliveryTimer = MaxDeliveryTime;
            isTimerActive = true;

            if (customPickupClip != null && audioSource != null) audioSource.PlayOneShot(customPickupClip);
            if (pickupMarkerObj != null) pickupMarkerObj.transform.position = HiddenMarkerPosition;
            if (pickupMapPin != null && pickupMapPin.gameObject != null) pickupMapPin.gameObject.SetActive(false);

            if (spawnedPickupAsset != null) spawnedPickupAsset.SetActive(false);

            if (dropoffMarkerObj != null && dropoffMapPin != null)
            {
                dropoffMarkerObj.transform.position = deliveryTargetPoint.Position;
                dropoffMapPin.InitMapPin(MapPin.PinType.StoryObjectivePin);
                dropoffMapPin.AssignGameplayEvent(dropoffMarkerObj);
                dropoffMapPin.enabled = false;
                dropoffMapPin.enabled = true;
            }

            Reptile.Stage currentStage = Reptile.Core.Instance?.BaseModule?.CurrentStage ?? Reptile.Stage.NONE;
            string message = ((int)currentStage == deliveryTargetPoint.StageID) ?
                $"PACKAGE <color=#FFFFAD>{deliveryPackageName}</color> <color=#76EE00>ACQUIRED!</color> Local Dropoff: <color=#AAFFDD>{deliveryTargetPoint.LocationName}</color>" :
                $"PACKAGE <color=#FFFFAD>{deliveryPackageName}</color> <color=#76EE00>ACQUIRED!</color> Go to <color=#33CCFF>DROPOFF:</color> <color=#AAFFDD>{deliveryTargetPoint.LocationName}</color>";

            messageOverrideTime = Time.time + 5.0f;
            ShowMessage(message, 5f);
        }

        private void CompleteDelivery(Player player)
        {
            isDelivering = false;
            isPickingUp = false;
            isTimerActive = false;
            pickupTargetPoint = null;
            deliveryTargetPoint = null;
            ResetMapMarkers();

            int baseReward = Mathf.RoundToInt(deliveryDistanceTotal * 0.2f);
            int finalReward = baseReward;
            string extraMessage = "";
            AudioClip extraClip = null;

            if (FeeModeEnabled.Value)
            {
                float timeTaken = Time.time - packageAcquiredTime;

                if (timeTaken >= 540f && timeTaken <= 598f)
                {
                    finalReward -= 90;
                    extraMessage = "<color=#FF0000>Too slow! Late Delivery! | -90 HyperBoosts</color>";
                    extraClip = customIncomingClip;
                }
                else if (timeTaken < 60f)
                {
                    finalReward += 100;
                    extraMessage = RainbowString("Hyperfunk Delivery! Delivery Under 1 minute?!") + " | <color=#FFEE00>+ 100 Extra HyperBoosts</color>";
                    extraClip = customHyperfunkClip;
                }
                else if (timeTaken < 240f)
                {
                    finalReward += 50;
                    extraMessage = "<color=#4DE94C>Bombrush Delivery! Delivery under 4 minutes</color> | <color=#FFEE00>+ 50 Extra HyperBoosts!</color>";
                    extraClip = customIncomingClip;
                }
            }

            // Write to HyperBoost JSON file (Wallet)
            TelemetryCache data = LoadData();
            data.cache_id += finalReward;
            if (data.cache_id < 0) data.cache_id = 0;
            SaveData(data);

            // Write to Time Trial Stats JSON
            TimeTrialStats stats = LoadTimeTrialStats();
            stats.LifetimeDeliveriesDone++;
            SaveTimeTrialStats(stats);

            sessionDeliveriesDone++;
            if (isTimeTrialActive) timeTrialHyperBoostsEarned += finalReward;

            StartCoroutine(PlayAnimationAndResume(player, finalReward, deliveryPackageName, extraMessage, extraClip));
            deliveryPackageName = "";
        }

        private string RainbowString(string text)
        {
            StringBuilder sb = new StringBuilder();
            int length = text.Length;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / (length > 1 ? length - 1 : 1);
                Color c = Color.HSVToRGB(t, 0.55f, 1f);
                string hex = ColorUtility.ToHtmlStringRGB(c);
                sb.Append($"<color=#{hex}>{text[i]}</color>");
            }
            return sb.ToString();
        }

        private IEnumerator PlayAnimationAndResume(Player player, int reward, string packageName, string extraMsg, AudioClip extraSound)
        {
            string animationName = VictoryDanceName.Value;
            float clipLength = VictoryDanceDuration.Value;
            bool success = false;
            Animator animator = null;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            Vector3 lockPosition = player.transform.position;

            TimeTrialStats stats = LoadTimeTrialStats();
            string statsSuffix = "";

            if (isTimeTrialActive)
            {
                statsSuffix = $" | <color=#5555FF>DONE IN SESSION: {sessionDeliveriesDone}</color>";
            }
            else
            {
                statsSuffix = $" | <color=#5555FF>DONE LIFETIME: {stats.LifetimeDeliveriesDone}</color>";
            }

            string finalMessage = $"<color=#76EE00>DELIVERY COMPLETE!</color> Delivered <color=#FFFFAD>{packageName}</color>! Reward: <color=#76EE00>{reward}</color> HyperBoosts. {statsSuffix}";

            customItemClips.TryGetValue(packageName, out AudioClip customItemClip);

            if (animField != null && rb != null)
            {
                try
                {
                    animator = (Animator)animField.GetValue(player);
                    if (animator != null)
                    {
                        player.SwitchToEquippedMovestyle(false);
                        player.enabled = false;
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;

                        if (customCompletionClip != null && audioSource != null) audioSource.PlayOneShot(customCompletionClip);

                        int boeHash = GetBoEStateHash(animationName);
                        if (boeHash != 0)
                        {
                            animator.Play(boeHash, -1, 0f);
                        }
                        else
                        {
                            animator.Play(animationName, -1, 0f);
                        }

                        ShowMessage(finalMessage, clipLength);
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"BRCDelivery: Animation handling failed: {ex.Message}");
                }
            }

            if (success)
            {
                float timer = 0f;
                while (timer < clipLength)
                {
                    player.transform.position = lockPosition;
                    rb.velocity = Vector3.zero;

                    if (customItemClip != null && audioSource != null && timer >= 1.0f && timer - Time.deltaTime < 1.0f)
                    {
                        audioSource.PlayOneShot(customItemClip);
                        customItemClip = null;
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }
                player.enabled = true;
            }
            else
            {
                if (player != null && !player.enabled) player.enabled = true;
                ShowMessage(finalMessage, 5f);
            }

            if (!string.IsNullOrEmpty(extraMsg))
            {
                yield return new WaitForSeconds(0.2f);
                if (extraSound != null && audioSource != null) audioSource.PlayOneShot(extraSound);
                messageOverrideTime = Time.time + 4.0f;
                ShowMessage(extraMsg, 4f);
            }
        }

        private void ShowMessage(string text, float duration = 3f)
        {
            var player = WorldHandler.instance?.GetCurrentPlayer();
            if (player != null) player.ShowInformation(text, duration);
        }

        private void DisplayDistance(string text, float duration)
        {
            var player = WorldHandler.instance?.GetCurrentPlayer();
            if (player != null) player.ShowInformation(text, duration);
        }

        public Texture2D LoadTexture(string filePath)
        {
            if (textureCache.ContainsKey(filePath)) return textureCache[filePath];

            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    textureCache[filePath] = texture;
                    return texture;
                }
            }
            return null;
        }

        private Texture2D CreateBlackTexture()
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            return tex;
        }
    }

    public class BombrushDeliveryApp : CustomApp
    {
        public override string DisplayName => "New Delivery";

        public override Texture2D Icon
        {
            get
            {
                if (Plugin.Instance == null) return null;
                Texture2D icon = Plugin.Instance.LoadTexture(Path.Combine(Plugin.Instance.ModFolderPath, "BombrushDeliveryIcon.png"));
                return icon != null ? icon : Plugin.Instance.blackTexture;
            }
        }

        public override void OnAppEnable()
        {
            if (Plugin.Instance != null && Plugin.Instance.ModEnabled.Value)
            {
                Plugin.Instance.StartDeliveryMission();
            }
            this.MyPhone.CloseCurrentApp();
        }
    }
}