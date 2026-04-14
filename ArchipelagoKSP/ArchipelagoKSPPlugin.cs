using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Upgradeables;
using KSP.UI.Screens;

namespace ArchipelagoKSP
{
    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    internal static class Log
    {
        private const string Tag =
            "<color=#89CFF0>[</color>" +
            "<color=#89CFF0>A</color>" +
            "<color=#FF9EBB>P</color>" +
            "<color=#FFFFFF>K</color>" +
            "<color=#FF9EBB>S</color>" +
            "<color=#89CFF0>P]</color>";

        internal static void Info(string msg)  => Debug.Log($"{Tag} {msg}");
        internal static void Warn(string msg)  => Debug.LogWarning($"{Tag} {msg}");
        internal static void Error(string msg) => Debug.LogError($"{Tag} {msg}");
    }

    // -------------------------------------------------------------------------
    // Shared state - static class persists for the lifetime of the loaded DLL,
    // i.e. one full KSP session. Scene changes do not reset this.
    // -------------------------------------------------------------------------

    internal static class APState
    {
        internal const string BridgeUrl = "http://127.0.0.1:52420";

        // SOI enforcement: set of body names the player is permitted to enter.
        // Kerbin and Sun (transition zone) are always permitted.
        internal static readonly HashSet<string> PermittedBodies = new HashSet<string>
        {
            "Kerbin", "Sun",
        };

        // AP location IDs we have already POSTed to the bridge this session.
        internal static readonly HashSet<long> SentChecks = new HashSet<long>();

        // Items applied so far this session (index into items_received list).
        internal static int LastAppliedIndex = 0;

        // Tech IDs for which we have received the AP part-bundle item this session.
        // The Editor addon uses this set to drive the VAB ExcludeFilter.
        internal static readonly HashSet<string> ReceivedPartBundles = new HashSet<string>();

        // AvailablePart.name for the three always-available starting parts.
        // Empty string = AP not connected yet.
        internal static string StartingPod   = "";
        internal static string StartingChute = "";
        internal static string StartingSRB   = "";

        // experimentID from AP slot_data; identifies the one starting science part.
        // Empty string = AP not connected yet.
        internal static string StartingExperimentID = "";

        // Cached AvailablePart.name for the starting experiment part (lazily set).
        internal static string StartingExperimentPartName = "";

        // Tech IDs currently being unlocked by AP item receipt (not player research).
        // Set immediately before UnlockProtoTechNode, cleared immediately after.
        // Guards OnTechResearched from posting a spurious AP check.
        internal static readonly HashSet<string> APUnlockedTechs = new HashSet<string>();

        // Part names for the 3 seed-selected starting parts.
        // Populated whenever ApplyEditorFilter runs or ComputeStartingParts is called.
        internal static readonly HashSet<string> StartingPartNames = new HashSet<string>();

        // Items received from the bridge but not yet applied.
        // Tuple: (AP items-list index, item name).
        internal static readonly Queue<(int index, string name)> PendingItems =
            new Queue<(int, string)>();

        // RDTech.techID -> AP location ID (mirrors Locations.py TECH_LOCATIONS order)
        internal static readonly Dictionary<string, long> TechIDToLocationID =
            new Dictionary<string, long>
        {
            // Tier 2
            { "basicRocketry",              7772000 },
            { "engineering101",             7772001 },
            // Tier 3
            { "survivability",              7772002 },
            { "stability",                  7772003 },
            { "generalRocketry",            7772004 },
            { "aviation",                   7772005 },
            { "basicScience",               7772006 },
            { "flightControl",              7772007 },
            // Tier 4
            { "advRocketry",                7772008 },
            { "generalConstruction",        7772009 },
            { "propulsionSystems",          7772010 },
            { "spaceExploration",           7772011 },
            { "advFlightControl",           7772012 },
            { "landing",                    7772013 },
            { "aerodynamicSystems",         7772014 },
            { "electrics",                  7772015 },
            // Tier 5
            { "heavyRocketry",              7772016 },
            { "fuelSystems",                7772017 },
            { "advConstruction",            7772018 },
            { "miniaturization",            7772019 },
            { "actuators",                  7772020 },
            { "commandModules",             7772021 },
            { "heavierRocketry",            7772022 },
            { "precisionEngineering",       7772023 },
            { "advExploration",             7772024 },
            { "specializedControl",         7772025 },
            { "advLanding",                 7772026 },
            // Tier 6
            { "supersonicFlight",           7772027 },
            { "advFuelSystems",             7772028 },
            { "advElectrics",               7772029 },
            { "specializedConstruction",    7772030 },
            { "precisionPropulsion",        7772031 },
            { "advAerodynamics",            7772032 },
            { "heavyLanding",               7772033 },
            { "scienceTech",                7772034 },
            { "unmannedTech",               7772035 },
            // Tier 7
            { "nuclearPropulsion",          7772036 },
            { "advMetalworks",              7772037 },
            { "fieldScience",               7772038 },
            { "highAltitudeFlight",         7772039 },
            { "largeVolumeContainment",     7772040 },
            { "composites",                 7772041 },
            { "electronics",               7772042 },
            { "largeElectrics",             7772043 },
            { "heavyAerodynamics",          7772044 },
            { "ionPropulsion",              7772045 },
            { "hypersonicFlight",           7772046 },
            // Tier 8
            { "nanolathing",                7772047 },
            { "advUnmanned",                7772048 },
            { "metaMaterials",              7772049 },
            { "veryHeavyRocketry",          7772050 },
            { "advScienceTech",             7772051 },
            { "advancedMotors",             7772052 },
            { "specializedElectrics",       7772053 },
            { "highPerformanceFuelSystems", 7772054 },
            { "experimentalAerodynamics",   7772055 },
            // Tier 9
            { "automation",                 7772056 },
            { "aerospaceTech",              7772057 },
            { "largeUnmanned",              7772058 },
            // Tier 10
            { "experimentalScience",        7772059 },
            { "experimentalMotors",         7772060 },
            { "experimentalElectrics",      7772061 },
        };

        // "UpgradeableFacility.id:newLevel" -> AP location ID
        internal static readonly Dictionary<string, long> FacilityToLocationID =
            new Dictionary<string, long>
        {
            { "SpaceCenter/VehicleAssemblyBuilding:1", 7773000 },
            { "SpaceCenter/VehicleAssemblyBuilding:2", 7773001 },
            { "SpaceCenter/SpacePlaneHangar:1",        7773002 },
            { "SpaceCenter/SpacePlaneHangar:2",        7773003 },
            { "SpaceCenter/ResearchAndDevelopment:1",  7773004 },
            { "SpaceCenter/ResearchAndDevelopment:2",  7773005 },
            { "SpaceCenter/MissionControl:1",          7773006 },
            { "SpaceCenter/MissionControl:2",          7773007 },
            { "SpaceCenter/TrackingStation:1",         7773008 },
            { "SpaceCenter/TrackingStation:2",         7773009 },
            { "SpaceCenter/AstronautComplex:1",        7773010 },
            { "SpaceCenter/AstronautComplex:2",        7773011 },
            { "SpaceCenter/LaunchPad:1",               7773012 },
            { "SpaceCenter/LaunchPad:2",               7773013 },
            { "SpaceCenter/Runway:1",                  7773014 },
            { "SpaceCenter/Runway:2",                  7773015 },
            { "SpaceCenter/Administration:1",          7773016 },
            { "SpaceCenter/Administration:2",          7773017 },
        };

        // CelestialBody.name -> AP location ID for flag plants
        internal static readonly Dictionary<string, long> BodyToFlagLocationID =
            new Dictionary<string, long>
        {
            // Kerbin system
            { "Kerbin",  7774000 },
            { "Mun",     7774001 },
            { "Minmus",  7774002 },
            // Inner planets
            { "Moho",    7774003 },
            { "Eve",     7774004 },
            { "Gilly",   7774005 },
            // Duna system
            { "Duna",    7774006 },
            { "Ike",     7774007 },
            // Dres
            { "Dres",    7774008 },
            // Jool moons (Jool itself is a gas giant - no flag location)
            { "Laythe",  7774009 },
            { "Vall",    7774010 },
            { "Tylo",    7774011 },
            { "Bop",     7774012 },
            { "Pol",     7774013 },
            // Eeloo
            { "Eeloo",   7774014 },
        };

        // AP item name -> KSP RDTech.techID (mirrors Items.py PART_BUNDLE_ITEMS)
        internal static readonly Dictionary<string, string> PartBundleToTechID =
            new Dictionary<string, string>
        {
            // Tier 1 (start node)
            { "Parts: Start",                         "start" },
            // Tier 2
            { "Parts: Basic Rocketry",                "basicRocketry" },
            { "Parts: Engineering 101",               "engineering101" },
            // Tier 3
            { "Parts: Survivability",                 "survivability" },
            { "Parts: Stability",                     "stability" },
            { "Parts: General Rocketry",              "generalRocketry" },
            { "Parts: Aviation",                      "aviation" },
            { "Parts: Basic Science",                 "basicScience" },
            { "Parts: Flight Control",                "flightControl" },
            // Tier 4
            { "Parts: Advanced Rocketry",             "advRocketry" },
            { "Parts: General Construction",          "generalConstruction" },
            { "Parts: Propulsion Systems",            "propulsionSystems" },
            { "Parts: Space Exploration",             "spaceExploration" },
            { "Parts: Advanced Flight Control",       "advFlightControl" },
            { "Parts: Landing",                       "landing" },
            { "Parts: Aerodynamics",                  "aerodynamicSystems" },
            { "Parts: Electrics",                     "electrics" },
            // Tier 5
            { "Parts: Heavy Rocketry",                "heavyRocketry" },
            { "Parts: Fuel Systems",                  "fuelSystems" },
            { "Parts: Advanced Construction",         "advConstruction" },
            { "Parts: Miniaturization",               "miniaturization" },
            { "Parts: Actuators",                     "actuators" },
            { "Parts: Command Modules",               "commandModules" },
            { "Parts: Heavier Rocketry",              "heavierRocketry" },
            { "Parts: Precision Engineering",         "precisionEngineering" },
            { "Parts: Advanced Exploration",          "advExploration" },
            { "Parts: Specialized Control",           "specializedControl" },
            { "Parts: Advanced Landing",              "advLanding" },
            // Tier 6
            { "Parts: Supersonic Flight",             "supersonicFlight" },
            { "Parts: Adv. Fuel Systems",             "advFuelSystems" },
            { "Parts: Advanced Electrics",            "advElectrics" },
            { "Parts: Specialized Construction",      "specializedConstruction" },
            { "Parts: Precision Propulsion",          "precisionPropulsion" },
            { "Parts: Advanced Aerodynamics",         "advAerodynamics" },
            { "Parts: Heavy Landing",                 "heavyLanding" },
            { "Parts: Scanning Tech",                 "scienceTech" },
            { "Parts: Unmanned Tech",                 "unmannedTech" },
            // Tier 7
            { "Parts: Nuclear Propulsion",            "nuclearPropulsion" },
            { "Parts: Advanced MetalWorks",           "advMetalworks" },
            { "Parts: Field Science",                 "fieldScience" },
            { "Parts: High Altitude Flight",          "highAltitudeFlight" },
            { "Parts: Large Volume Containment",      "largeVolumeContainment" },
            { "Parts: Composites",                    "composites" },
            { "Parts: Electronics",                   "electronics" },
            { "Parts: High-Power Electrics",          "largeElectrics" },
            { "Parts: Heavy Aerodynamics",            "heavyAerodynamics" },
            { "Parts: Ion Propulsion",                "ionPropulsion" },
            { "Parts: Hypersonic Flight",             "hypersonicFlight" },
            // Tier 8
            { "Parts: Nanolathing",                   "nanolathing" },
            { "Parts: Advanced Unmanned Tech",        "advUnmanned" },
            { "Parts: Meta-Materials",                "metaMaterials" },
            { "Parts: Very Heavy Rocketry",           "veryHeavyRocketry" },
            { "Parts: Advanced Science Tech",         "advScienceTech" },
            { "Parts: Advanced Motors",               "advancedMotors" },
            { "Parts: Specialized Electrics",         "specializedElectrics" },
            { "Parts: High-Performance Fuel Systems", "highPerformanceFuelSystems" },
            { "Parts: Experimental Aerodynamics",     "experimentalAerodynamics" },
            // Tier 9
            { "Parts: Automation",                    "automation" },
            { "Parts: Aerospace Tech",                "aerospaceTech" },
            { "Parts: Large Probes",                  "largeUnmanned" },
            // Tier 10
            { "Parts: Experimental Science",          "experimentalScience" },
            { "Parts: Experimental Motors",           "experimentalMotors" },
            { "Parts: Experimental Electrics",        "experimentalElectrics" },
        };
    }

    // -------------------------------------------------------------------------
    // JSON response types
    // -------------------------------------------------------------------------

    [Serializable]
    internal class ItemsResponse
    {
        public string[] items = Array.Empty<string>();
        public int index = 0;
    }

    [Serializable]
    internal class StatusResponse
    {
        public bool connected = false;
        public string slot = "";
        public string starting_pod   = "";
        public string starting_chute = "";
        public string starting_srb   = "";
        public string starting_experiment = "";
    }

    // -------------------------------------------------------------------------
    // Shared HTTP helpers
    // -------------------------------------------------------------------------

    internal static class APBridge
    {
        internal static void EnqueueNewItems(string[] allItems)
        {
            for (int i = APState.LastAppliedIndex; i < allItems.Length; i++)
                APState.PendingItems.Enqueue((i, allItems[i]));
            APState.LastAppliedIndex = allItems.Length;
        }

        internal static void FlushPendingItems()
        {
            while (APState.PendingItems.Count > 0)
            {
                var (index, name) = APState.PendingItems.Peek();
                // Already recorded in the save: skip without re-applying.
                if (APKSPPersistence.Instance?.IsApplied(index) == true)
                {
                    APState.PendingItems.Dequeue();
                    continue;
                }
                if (!TryApplyItem(name))
                    break;
                APKSPPersistence.Instance?.MarkApplied(index);
                APState.PendingItems.Dequeue();
                Log.Info($"Applied item [{index}]: {name}");
            }
        }

        // Unlock a tech node in the save without firing a player-research AP check.
        // The start node is handled by VAB filter only (cannot be researched in R&D).
        private static void UnlockTechNode(string techID)
        {
            if (techID == "start") return;
            var rnd = ResearchAndDevelopment.Instance;
            if (rnd == null) return;

            var state = rnd.GetTechState(techID);
            if (state != null && state.state == RDTech.State.Available) return;

            // Guard: if UnlockProtoTechNode fires OnTechnologyResearched synchronously,
            // OnTechResearched will see this flag and skip the AP check.
            APState.APUnlockedTechs.Add(techID);
            try
            {
                if (state == null)
                {
                    state = new ProtoTechNode();
                    state.techID = techID;
                    state.scienceCost = 0;
                    state.partsPurchased = new List<AvailablePart>();
                }
                state.state = RDTech.State.Available;
                rnd.UnlockProtoTechNode(state);
                Log.Info($"Tech unlocked by AP item: {techID}");
            }
            finally
            {
                APState.APUnlockedTechs.Remove(techID);
            }
        }

        // Find the AvailablePart with the given internal part name. Returns null if not found.
        internal static AvailablePart FindPartByName(string partName)
        {
            if (string.IsNullOrEmpty(partName) || PartLoader.Instance == null)
                return null;
            return PartLoader.Instance.loadedParts.FirstOrDefault(p => p.name == partName);
        }

        // Find the AvailablePart whose prefab contains a ModuleScienceExperiment
        // with the given experimentID. Returns null if not found or parts not loaded.
        internal static AvailablePart ComputeStartingExperimentPart(string experimentID)
        {
            if (string.IsNullOrEmpty(experimentID) || PartLoader.Instance == null)
                return null;
            return PartLoader.Instance.loadedParts.FirstOrDefault(p =>
                p.partPrefab != null &&
                p.partPrefab.GetComponents<ModuleScienceExperiment>()
                    .Any(m => m.experimentID == experimentID));
        }

        // Map from permit item name to the CelestialBody.name it unlocks.
        private static readonly Dictionary<string, string> PermitToBody =
            new Dictionary<string, string>
        {
            { "Mun Permit",    "Mun"    },
            { "Minmus Permit", "Minmus" },
            { "Moho Permit",   "Moho"   },
            { "Eve Permit",    "Eve"    },
            { "Gilly Permit",  "Gilly"  },
            { "Duna Permit",   "Duna"   },
            { "Ike Permit",    "Ike"    },
            { "Dres Permit",   "Dres"   },
            { "Jool Permit",   "Jool"   },
            { "Laythe Permit", "Laythe" },
            { "Vall Permit",   "Vall"   },
            { "Tylo Permit",   "Tylo"   },
            { "Bop Permit",    "Bop"    },
            { "Pol Permit",    "Pol"    },
            { "Eeloo Permit",  "Eeloo"  },
        };

        private static bool TryApplyItem(string itemName)
        {
            // SOI permits: add the body to the permitted set.
            if (PermitToBody.TryGetValue(itemName, out string body))
            {
                APState.PermittedBodies.Add(body);
                Log.Info($"SOI permit received: {body} added to permitted bodies.");
                return true;
            }

            // KSC upgrade capability items: tracked for AP logic; no in-game action
            // required (KSP already enforces building prerequisites in the base game).
            if (itemName == "Tracking Station Level 2"  ||
                itemName == "VAB Level 2"               ||
                itemName == "Launch Pad Level 2"        ||
                itemName == "Astronaut Complex Level 2")
            {
                Log.Info($"KSC upgrade item received: {itemName}");
                return true;
            }

            // Part bundles: record receipt and unlock the tech node so parts are usable.
            // The VAB ExcludeFilter also reads ReceivedPartBundles for visibility.
            // The "start" node cannot be researched; it is filter-only.
            if (APState.PartBundleToTechID.TryGetValue(itemName, out string techID))
            {
                APState.ReceivedPartBundles.Add(techID);
                UnlockTechNode(techID);
                return true;
            }

            // Filler items
            if (itemName == "Funds Boost")
            {
                if (Funding.Instance == null) return false;
                Funding.Instance.AddFunds(80000, TransactionReasons.None);
                return true;
            }
            if (itemName == "Reputation Boost")
            {
                if (Reputation.Instance == null) return false;
                Reputation.Instance.AddReputation(50, TransactionReasons.None);
                return true;
            }

            Log.Warn($"Unknown item name: {itemName}");
            return true;
        }

        // Returns true if a part is visible/usable in the current AP state.
        internal static bool IsPartAvailable(AvailablePart part)
        {
            if (part == null) return true;
            // Name-selected starting parts are always available.
            if (APState.StartingPartNames.Count == 0 && !string.IsNullOrEmpty(APState.StartingPod))
            {
                if (!string.IsNullOrEmpty(APState.StartingPod))   APState.StartingPartNames.Add(APState.StartingPod);
                if (!string.IsNullOrEmpty(APState.StartingChute)) APState.StartingPartNames.Add(APState.StartingChute);
                if (!string.IsNullOrEmpty(APState.StartingSRB))   APState.StartingPartNames.Add(APState.StartingSRB);
            }
            if (APState.StartingPartNames.Contains(part.name)) return true;
            // Experiment starting part: lazy-resolve name on first check.
            if (!string.IsNullOrEmpty(APState.StartingExperimentID))
            {
                if (string.IsNullOrEmpty(APState.StartingExperimentPartName))
                {
                    var ep = ComputeStartingExperimentPart(APState.StartingExperimentID);
                    APState.StartingExperimentPartName = ep?.name ?? "";
                }
                if (!string.IsNullOrEmpty(APState.StartingExperimentPartName) &&
                    part.name == APState.StartingExperimentPartName)
                    return true;
            }
            if (part.TechRequired == "start") return APState.ReceivedPartBundles.Contains("start");
            if (APState.TechIDToLocationID.ContainsKey(part.TechRequired))
                return APState.ReceivedPartBundles.Contains(part.TechRequired);
            return true; // untracked node - always available
        }

        // Scan an object's fields for an AvailablePart via reflection.
        // Used to find the part referenced by a PartTest contract or parameter.
        private static AvailablePart FindAvailablePartField(object obj)
        {
            if (obj == null) return null;
            foreach (var f in obj.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (f.FieldType == typeof(AvailablePart))
                    return f.GetValue(obj) as AvailablePart;
            }
            return null;
        }

        // Returns true if the contract references any part the player doesn't yet have.
        internal static bool ContractNeedsUnavailablePart(Contracts.Contract contract)
        {
            var part = FindAvailablePartField(contract);
            if (part != null && !IsPartAvailable(part)) return true;
            foreach (var param in contract.AllParameters)
            {
                part = FindAvailablePartField(param);
                if (part != null && !IsPartAvailable(part)) return true;
            }
            return false;
        }

        internal static IEnumerator PostCheck(MonoBehaviour host, long locationId)
        {
            if (APState.SentChecks.Contains(locationId)) yield break;
            APState.SentChecks.Add(locationId);

            byte[] body = Encoding.UTF8.GetBytes($"{{\"location_id\":{locationId}}}");
            using (var req = new UnityWebRequest(APState.BridgeUrl + "/check", "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                if (req.isNetworkError || req.isHttpError)
                {
                    Log.Warn($"Check POST failed (ID {locationId}): {req.error} - will retry on Sync");
                    APState.SentChecks.Remove(locationId);
                }
            }
        }

        internal static IEnumerator PostConnect(string server, string slot, string password)
        {
            string json = $"{{\"server\":\"{EscapeJson(server)}\","
                        + $"\"slot\":\"{EscapeJson(slot)}\","
                        + $"\"password\":\"{EscapeJson(password)}\"}}";
            byte[] body = Encoding.UTF8.GetBytes(json);
            using (var req = new UnityWebRequest(APState.BridgeUrl + "/connect", "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                if (req.isNetworkError || req.isHttpError)
                    Log.Warn($"Connect POST failed: {req.error}");
                else
                    Log.Info($"Connect request sent: {server} / {slot}");
            }
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // -------------------------------------------------------------------------
    // Connection + status UI - attached to all addon GameObjects so the F8
    // window works in SpaceCentre, Flight, and Editor scenes.
    // -------------------------------------------------------------------------

    internal class APConnectionUI : MonoBehaviour
    {
        private bool showWindow = false;
        private Rect windowRect = new Rect(80, 80, 380, 260);
        private string serverField   = "archipelago.gg:38281";
        private string slotField     = "";
        private string passwordField = "";
        internal string StatusText = "Bridge offline - run KSPClient.py first";
        private bool wasConnected = false;

        private static readonly string ConfigPath =
            KSPUtil.ApplicationRootPath + "GameData/ArchipelagoKSP/connection.cfg";

        private void LoadFields()
        {
            if (!System.IO.File.Exists(ConfigPath)) return;
            var node = ConfigNode.Load(ConfigPath);
            if (node == null) return;
            serverField = node.GetValue("server") ?? serverField;
            slotField   = node.GetValue("slot")   ?? slotField;
        }

        private void SaveFields()
        {
            var node = new ConfigNode("APKSP_CONNECTION");
            node.AddValue("server", serverField);
            node.AddValue("slot",   slotField);
            node.Save(ConfigPath);
        }

        void Start() { LoadFields(); }
        void OnDestroy() { SaveFields(); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                showWindow = !showWindow;
        }

        void OnGUI()
        {
            if (!showWindow) return;
            windowRect = GUILayout.Window(
                GetInstanceID(), windowRect, DrawWindow, "Archipelago KSP");
        }

        void DrawWindow(int id)
        {
            GUILayout.Label(StatusText);
            GUILayout.Space(6);

            GUILayout.Label("Server  (host:port)");
            serverField = GUILayout.TextField(serverField, GUILayout.Width(340));

            GUILayout.Label("Slot name");
            slotField = GUILayout.TextField(slotField, GUILayout.Width(340));

            GUILayout.Label("Password  (leave blank if none)");
            passwordField = GUILayout.PasswordField(passwordField, '*', GUILayout.Width(340));

            GUILayout.Space(8);
            if (GUILayout.Button("Connect to Archipelago"))
            {
                SaveFields();
                StartCoroutine(APBridge.PostConnect(serverField, slotField, passwordField));
            }

            GUILayout.Space(4);
            if (GUILayout.Button("Sync from AP"))
                StartCoroutine(ResyncFromAP());

            GUILayout.Space(4);
            GUILayout.Label("<size=10>Press F8 to toggle this window</size>");

            GUI.DragWindow();
        }

        // Poll /status every 10 s; store seed and trigger resync on first connect.
        internal IEnumerator PollStatus()
        {
            while (true)
            {
                yield return new WaitForSeconds(10f);
                using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/status"))
                {
                    yield return req.SendWebRequest();
                    if (!req.isNetworkError && !req.isHttpError)
                    {
                        var s = JsonUtility.FromJson<StatusResponse>(req.downloadHandler.text);
                        if (s != null)
                        {
                            if (!string.IsNullOrEmpty(s.starting_pod))   APState.StartingPod   = s.starting_pod;
                            if (!string.IsNullOrEmpty(s.starting_chute)) APState.StartingChute = s.starting_chute;
                            if (!string.IsNullOrEmpty(s.starting_srb))   APState.StartingSRB   = s.starting_srb;
                            if (!string.IsNullOrEmpty(s.starting_experiment))
                                APState.StartingExperimentID = s.starting_experiment;
                            StatusText = s.connected
                                ? $"Connected  as: {s.slot}"
                                : "Not connected  (bridge running)";
                            if (s.connected && !wasConnected)
                                StartCoroutine(ResyncFromAP());
                            wasConnected = s.connected;
                        }
                    }
                    else
                    {
                        StatusText = "Bridge offline - run KSPClient.py first";
                    }
                }
            }
        }

        // Rebuild AP item state from scratch and re-send any missed checks.
        internal IEnumerator ResyncFromAP()
        {
            Log.Info("Resync started.");
            StatusText = "Syncing from AP...";

            var rnd = ResearchAndDevelopment.Instance;

            APState.LastAppliedIndex = 0;
            APState.PendingItems.Clear();
            APState.ReceivedPartBundles.Clear();
            APState.APUnlockedTechs.Clear();

            using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/items"))
            {
                yield return req.SendWebRequest();
                if (!req.isNetworkError && !req.isHttpError)
                {
                    var resp = JsonUtility.FromJson<ItemsResponse>(req.downloadHandler.text);
                    if (resp?.items != null)
                        APBridge.EnqueueNewItems(resp.items);
                }
                else Log.Warn($"Resync: /items fetch failed: {req.error}");
            }
            APBridge.FlushPendingItems();

            // Refresh VAB filter if we are in the editor.
            ArchipelagoKSPEditor.Instance?.ApplyEditorFilter();

            // Re-post checks for techs the player researched but whose check was missed.
            // If we already have the AP item for a tech, either:
            //   (a) the check succeeded and the item was sent back normally, or
            //   (b) the tech was AP-unlocked (not player-researched) - no check needed.
            // Either way, no re-post needed. Only re-post when tech is Available but
            // the AP item has not arrived, indicating a missed check.
            if (rnd != null)
            {
                foreach (var kv in APState.TechIDToLocationID)
                {
                    if (APState.ReceivedPartBundles.Contains(kv.Key)) continue;
                    var techState = rnd.GetTechState(kv.Key);
                    if (techState != null && techState.state == RDTech.State.Available)
                    {
                        APState.SentChecks.Remove(kv.Value);
                        StartCoroutine(APBridge.PostCheck(this, kv.Value));
                    }
                }
            }

            Log.Info("Resync complete.");
            StatusText = wasConnected ? $"Connected  as: {slotField}  (synced)" : StatusText;
        }

        // Poll /items every 5 s, enqueue new arrivals, flush queue.
        internal IEnumerator PollItems()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);
                using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/items"))
                {
                    yield return req.SendWebRequest();
                    if (!req.isNetworkError && !req.isHttpError)
                    {
                        var resp = JsonUtility.FromJson<ItemsResponse>(req.downloadHandler.text);
                        if (resp?.items != null)
                            APBridge.EnqueueNewItems(resp.items);
                    }
                }
                APBridge.FlushPendingItems();
            }
        }
    }

    // -------------------------------------------------------------------------
    // SpaceCentre addon: facility upgrades + tech research -> AP checks only.
    // The tech tree and partsPurchased are never modified by this addon.
    // -------------------------------------------------------------------------

    [KSPAddon(KSPAddon.Startup.SpaceCentre, once: false)]
    public class ArchipelagoKSPSpaceCentre : MonoBehaviour
    {
        private APConnectionUI ui;

        void Start()
        {
            ui = gameObject.AddComponent<APConnectionUI>();
            StartCoroutine(ui.PollStatus());
            StartCoroutine(ui.PollItems());

            GameEvents.OnKSCFacilityUpgraded.Add(OnFacilityUpgraded);
            GameEvents.OnTechnologyResearched.Add(OnTechResearched);
            GameEvents.Contract.onOffered.Add(OnContractOffered);
            Log.Info("SpaceCentre listeners registered.  Press F8 for Archipelago menu.");
        }

        void OnDestroy()
        {
            GameEvents.OnKSCFacilityUpgraded.Remove(OnFacilityUpgraded);
            GameEvents.OnTechnologyResearched.Remove(OnTechResearched);
            GameEvents.Contract.onOffered.Remove(OnContractOffered);
        }

        void OnFacilityUpgraded(UpgradeableFacility facility, int newLevel)
        {
            string key = $"{facility.id}:{newLevel}";
            if (APState.FacilityToLocationID.TryGetValue(key, out long locID))
            {
                Log.Info($"LOCATION CHECK - Facility: {key}  ID:{locID}");
                StartCoroutine(APBridge.PostCheck(this, locID));
            }
            else
            {
                Log.Warn($"No AP location for facility key: {key}");
            }
        }

        void OnTechResearched(
            GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> data)
        {
            if (data.target != RDTech.OperationResult.Successful) return;
            string techID = data.host.techID;

            // Skip if this unlock was triggered by AP item receipt, not player research.
            if (APState.APUnlockedTechs.Contains(techID))
            {
                Log.Info($"Tech {techID} unlocked by AP item - skipping location check.");
                return;
            }

            if (APState.TechIDToLocationID.TryGetValue(techID, out long locID))
            {
                Log.Info($"LOCATION CHECK - Tech: {techID}  ID:{locID}");
                StartCoroutine(APBridge.PostCheck(this, locID));
            }
            else
            {
                Log.Warn($"No AP location for tech node: {techID}");
            }
        }

        void OnContractOffered(Contracts.Contract contract)
        {
            if (APBridge.ContractNeedsUnavailablePart(contract))
            {
                Log.Info($"Withdrawing contract ({contract.GetType().Name}): requires unavailable part.");
                contract.Withdraw();
            }
        }
    }

    // -------------------------------------------------------------------------
    // Flight addon: flag plants + SOI enforcement
    // -------------------------------------------------------------------------

    [KSPAddon(KSPAddon.Startup.Flight, once: false)]
    public class ArchipelagoKSPFlight : MonoBehaviour
    {
        private APConnectionUI ui;

        void Start()
        {
            ui = gameObject.AddComponent<APConnectionUI>();
            StartCoroutine(ui.PollStatus());
            StartCoroutine(ui.PollItems());

            GameEvents.onFlagPlant.Add(OnFlagPlant);
            GameEvents.onVesselSOIChanged.Add(OnSOIChanged);
            Log.Info("Flight listeners registered.  Press F8 for Archipelago menu.");
        }

        void OnDestroy()
        {
            GameEvents.onFlagPlant.Remove(OnFlagPlant);
            GameEvents.onVesselSOIChanged.Remove(OnSOIChanged);
        }

        void OnFlagPlant(Vessel vessel)
        {
            string body = vessel.mainBody != null ? vessel.mainBody.name : "Unknown";
            if (APState.BodyToFlagLocationID.TryGetValue(body, out long locID))
            {
                Log.Info($"LOCATION CHECK - Flag on {body}  ID:{locID}");
                StartCoroutine(APBridge.PostCheck(this, locID));
            }
            else
            {
                Log.Warn($"No AP location for flag body: {body}");
            }
        }

        void OnSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> data)
        {
            string body = data.to.name;
            if (!APState.PermittedBodies.Contains(body))
            {
                Log.Warn($"EXPLODING - entered unpermitted SOI: {body}");
                ScreenMessages.PostScreenMessage(
                    $"[APKSP] No permit for {body}!  Vessel destroyed.",
                    5f, ScreenMessageStyle.UPPER_CENTER);
                data.host.rootPart.explode();
            }
            else
            {
                Log.Info($"SOI entered: {body} (permitted)");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Editor addon: VAB/SPH part filter driven entirely by AP items.
    //
    // Uses EditorPartListFilter<AvailablePart> (same mechanism as JanitorsCloset)
    // so the filter is non-invasive - partsPurchased and the save file are never
    // touched. The filter is removed on OnDestroy (scene exit).
    //
    // Filter rules (checked in order):
    //   allowedStartPartNames - 3 seed-selected parts (any node): always visible
    //   "start" tech node     - show only if "Parts: Start" AP bundle received
    //   tracked tech nodes    - show only if AP bundle received
    //   all other nodes       - pass through (untracked, always visible)
    //
    // Starting parts' tech nodes are unlocked (so KSP allows launching with them)
    // but are NOT added to ReceivedPartBundles. The resync loop auto-posts their
    // location checks, then the AP item arrives and reveals all parts in those nodes.
    // -------------------------------------------------------------------------

    [KSPAddon(KSPAddon.Startup.EditorAny, once: false)]
    public class ArchipelagoKSPEditor : MonoBehaviour
    {
        internal static ArchipelagoKSPEditor Instance { get; private set; }

        private APConnectionUI ui;
        private EditorPartListFilter<AvailablePart> partFilter;

        // Names of the 3 seed-selected starting parts (from any tech node).
        private readonly HashSet<string> allowedStartPartNames = new HashSet<string>();

        void Awake() { Instance = this; }

        void Start()
        {
            ui = gameObject.AddComponent<APConnectionUI>();
            StartCoroutine(ui.PollStatus());
            StartCoroutine(EditorPollLoop());
            Log.Info("Editor addon started.  Press F8 for Archipelago menu.");
        }

        void OnDestroy()
        {
            Instance = null;
            RemoveFilter();
        }

        // Fetch items on editor entry (rebuilds state if coming from main menu),
        // apply filter, then re-poll every 5 s and re-filter on changes.
        private IEnumerator EditorPollLoop()
        {
            // Wait for EditorPartList to be ready (mirrors JanitorsCloset pattern).
            while (EditorPartList.Instance == null)
                yield return new WaitForSeconds(0.1f);

            // Initial items fetch.
            using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/items"))
            {
                yield return req.SendWebRequest();
                if (!req.isNetworkError && !req.isHttpError)
                {
                    var resp = JsonUtility.FromJson<ItemsResponse>(req.downloadHandler.text);
                    if (resp?.items != null) { APBridge.EnqueueNewItems(resp.items); APBridge.FlushPendingItems(); }
                }
            }

            // Fetch starting part names and experiment ID if not yet known.
            if (string.IsNullOrEmpty(APState.StartingPod) || string.IsNullOrEmpty(APState.StartingExperimentID))
            {
                using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/status"))
                {
                    yield return req.SendWebRequest();
                    if (!req.isNetworkError && !req.isHttpError)
                    {
                        var s = JsonUtility.FromJson<StatusResponse>(req.downloadHandler.text);
                        if (s != null)
                        {
                            if (!string.IsNullOrEmpty(s.starting_pod))   APState.StartingPod   = s.starting_pod;
                            if (!string.IsNullOrEmpty(s.starting_chute)) APState.StartingChute = s.starting_chute;
                            if (!string.IsNullOrEmpty(s.starting_srb))   APState.StartingSRB   = s.starting_srb;
                            if (!string.IsNullOrEmpty(s.starting_experiment))
                                APState.StartingExperimentID = s.starting_experiment;
                        }
                    }
                }
            }

            ApplyEditorFilter();

            // Ongoing poll - re-filter only when items or starting parts change.
            while (true)
            {
                yield return new WaitForSeconds(5f);
                int indexBefore = APState.LastAppliedIndex;
                string podBefore = APState.StartingPod;
                string expBefore = APState.StartingExperimentID;

                using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/items"))
                {
                    yield return req.SendWebRequest();
                    if (!req.isNetworkError && !req.isHttpError)
                    {
                        var resp = JsonUtility.FromJson<ItemsResponse>(req.downloadHandler.text);
                        if (resp?.items != null) APBridge.EnqueueNewItems(resp.items);
                    }
                }
                APBridge.FlushPendingItems();

                if (APState.LastAppliedIndex != indexBefore || APState.StartingPod != podBefore
                    || APState.StartingExperimentID != expBefore)
                    ApplyEditorFilter();
            }
        }

        // Install (or reinstall) the ExcludeFilter based on current AP state.
        internal void ApplyEditorFilter()
        {
            if (EditorPartList.Instance == null) return;

            RemoveFilter();

            // Look up and register the three name-selected starting parts.
            allowedStartPartNames.Clear();
            APState.StartingPartNames.Clear();
            APState.StartingExperimentPartName = "";
            foreach (var partName in new[] { APState.StartingPod, APState.StartingChute, APState.StartingSRB })
            {
                if (string.IsNullOrEmpty(partName)) continue;
                var p = APBridge.FindPartByName(partName);
                if (p == null) continue;
                allowedStartPartNames.Add(p.name);
                APState.StartingPartNames.Add(p.name);
                // Mark as experimental so KSP career mode unlocks it without
                // researching its tech node. TechRequired is also moved to "start"
                // so the launch validator accepts it.
                if (ResearchAndDevelopment.Instance != null)
                    ResearchAndDevelopment.AddExperimentalPart(p);
                if (p.TechRequired != "start" && !string.IsNullOrEmpty(p.TechRequired))
                    p.TechRequired = "start";
            }
            // Add the name-selected starting science experiment part.
            if (!string.IsNullOrEmpty(APState.StartingExperimentID))
            {
                var ep = APBridge.ComputeStartingExperimentPart(APState.StartingExperimentID);
                if (ep != null)
                {
                    allowedStartPartNames.Add(ep.name);
                    APState.StartingPartNames.Add(ep.name);
                    APState.StartingExperimentPartName = ep.name;
                    if (ResearchAndDevelopment.Instance != null)
                        ResearchAndDevelopment.AddExperimentalPart(ep);
                    if (ep.TechRequired != "start" && !string.IsNullOrEmpty(ep.TechRequired))
                        ep.TechRequired = "start";
                }
            }

            Func<AvailablePart, bool> criteria = (ap) =>
            {
                // Name-selected starting parts are always visible from any node.
                if (allowedStartPartNames.Contains(ap.name)) return true;

                if (ap.TechRequired == "start")
                    return APState.ReceivedPartBundles.Contains("start");

                if (APState.TechIDToLocationID.ContainsKey(ap.TechRequired))
                    return APState.ReceivedPartBundles.Contains(ap.TechRequired);

                return true; // untracked node - always visible
            };

            partFilter = new EditorPartListFilter<AvailablePart>("APKSP", criteria);
            EditorPartList.Instance.ExcludeFilters.AddFilter(partFilter);
            EditorPartList.Instance.Refresh();

            Log.Info($"Editor filter: {APState.ReceivedPartBundles.Count} bundles, "
                   + $"{allowedStartPartNames.Count} start parts, pod={APState.StartingPod}");
        }

        private void RemoveFilter()
        {
            if (partFilter == null) return;
            if (EditorPartList.Instance != null)
                EditorPartList.Instance.ExcludeFilters.RemoveFilter(partFilter);
            partFilter = null;
        }
    }

    // -------------------------------------------------------------------------
    // Persistence: tracks which AP item indices have been applied so that
    // re-applying items after loading an old save is skipped.
    // Saved/loaded with the KSP save file as a ScenarioModule.
    // -------------------------------------------------------------------------

    [KSPScenario(
        ScenarioCreationOptions.AddToAllGames,
        GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.TRACKSTATION)]
    public class APKSPPersistence : ScenarioModule
    {
        public static APKSPPersistence? Instance { get; private set; }

        private readonly HashSet<int> appliedIndices = new HashSet<int>();

        public override void OnAwake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsApplied(int index) => appliedIndices.Contains(index);

        public void MarkApplied(int index) => appliedIndices.Add(index);

        public override void OnSave(ConfigNode node)
        {
            foreach (int idx in appliedIndices)
                node.AddValue("appliedIndex", idx);
        }

        public override void OnLoad(ConfigNode node)
        {
            appliedIndices.Clear();
            foreach (string v in node.GetValues("appliedIndex"))
                if (int.TryParse(v, out int idx))
                    appliedIndices.Add(idx);
        }
    }
}
