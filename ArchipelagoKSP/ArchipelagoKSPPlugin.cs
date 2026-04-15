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
            "<color=#89CFF0>A</color>"+
            "<color=#FF9EBB>P</color>" +
            "<color=#FFFFFF>K</color>" +
            "<color=#FF9EBB>S</color>" +
            "<color=#89CFF0>P</color>";

        internal static void Info(string msg)  => Debug.Log($"[{Tag}] {msg}");
        internal static void Warn(string msg)  => Debug.LogWarning($"[{Tag}] {msg}");
        internal static void Error(string msg) => Debug.LogError($"[{Tag}] {msg}");
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

        // Part names for the 4 seed-selected starting parts.
        // Populated whenever ApplyEditorFilter runs or ComputeStartingExperimentPart
        internal static readonly HashSet<string> StartingPartNames = new HashSet<string>();

        // Items received from the bridge but not yet applied.
        // Tuple: (AP items-list index, item name).
        internal static readonly Queue<(int index, string name)> PendingItems =
            new Queue<(int, string)>();

        // Slot name returned by the bridge /status endpoint. Empty when not connected.
        internal static string ConnectedSlot = "";

        // True while ResyncFromAP is rebuilding PermittedBodies from scratch.
        // SOI enforcement skips the explode check during this window.
        internal static bool ResyncInProgress = false;

        // Set to true when the user explicitly clicks Connect in the F8 menu.
        // Polling and syncing are suppressed until then.
        internal static bool UserHasConnected = false;

        // RDTech.techID -> AP location ID (mirrors Locations.py TECH_LOCATIONS order)
        internal static readonly Dictionary<string, long> TechIDToLocationID =
            new Dictionary<string, long>
        {
            // Tier 2
            { "basicRocketry",              1970000 },
            { "engineering101",             1970001 },
            // Tier 3
            { "survivability",              1970002 },
            { "stability",                  1970003 },
            { "generalRocketry",            1970004 },
            // Tier 4
            { "aviation",                   1970005 },
            { "basicScience",               1970006 },
            { "flightControl",              1970007 },
            { "advRocketry",                1970008 },
            { "generalConstruction",        1970009 },
            { "propulsionSystems",          1970010 },
            { "spaceExploration",           1970011 },
            { "advFlightControl",           1970012 },
            { "landing",                    1970013 },
            { "aerodynamicSystems",         1970014 },
            { "electrics",                  1970015 },
            // Tier 5
            { "heavyRocketry",              1970016 },
            { "fuelSystems",                1970017 },
            { "advConstruction",            1970018 },
            { "miniaturization",            1970019 },
            { "actuators",                  1970020 },
            { "commandModules",             1970021 },
            { "heavierRocketry",            1970022 },
            { "precisionEngineering",       1970023 },
            { "advExploration",             1970024 },
            { "specializedControl",         1970025 },
            { "advLanding",                 1970026 },
            // Tier 6
            { "supersonicFlight",           1970027 },
            { "advFuelSystems",             1970028 },
            { "advElectrics",               1970029 },
            { "specializedConstruction",    1970030 },
            { "precisionPropulsion",        1970031 },
            { "advAerodynamics",            1970032 },
            { "heavyLanding",               1970033 },
            { "scienceTech",                1970034 },
            { "unmannedTech",               1970035 },
            // Tier 7
            { "nuclearPropulsion",          1970036 },
            { "advMetalworks",              1970037 },
            { "fieldScience",               1970038 },
            { "highAltitudeFlight",         1970039 },
            { "largeVolumeContainment",     1970040 },
            { "composites",                 1970041 },
            { "electronics",               1970042 },
            { "largeElectrics",             1970043 },
            { "heavyAerodynamics",          1970044 },
            { "ionPropulsion",              1970045 },
            { "hypersonicFlight",           1970046 },
            // Tier 8
            { "nanolathing",                1970047 },
            { "advUnmanned",                1970048 },
            { "metaMaterials",              1970049 },
            { "veryHeavyRocketry",          1970050 },
            { "advScienceTech",             1970051 },
            { "advancedMotors",             1970052 },
            { "specializedElectrics",       1970053 },
            { "highPerformanceFuelSystems", 1970054 },
            { "experimentalAerodynamics",   1970055 },
            // Tier 9
            { "automation",                 1970056 },
            { "aerospaceTech",              1970057 },
            { "largeUnmanned",              1970058 },
            // Tier 10
            { "experimentalScience",        1970059 },
            { "experimentalMotors",         1970060 },
            { "experimentalElectrics",      1970061 },
        };

        // "UpgradeableFacility.id:newLevel" -> AP location ID
        // OrdinalIgnoreCase: KSP facility.id casing can differ by version.
        internal static readonly Dictionary<string, long> FacilityToLocationID =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "SpaceCenter/VehicleAssemblyBuilding:1", 1971000 },
            { "SpaceCenter/VehicleAssemblyBuilding:2", 1971001 },
            { "SpaceCenter/SpaceplaneHangar:1",        1971002 },
            { "SpaceCenter/SpaceplaneHangar:2",        1971003 },
            { "SpaceCenter/ResearchAndDevelopment:1",  1971004 },
            { "SpaceCenter/ResearchAndDevelopment:2",  1971005 },
            { "SpaceCenter/MissionControl:1",          1971006 },
            { "SpaceCenter/MissionControl:2",          1971007 },
            { "SpaceCenter/TrackingStation:1",         1971008 },
            { "SpaceCenter/TrackingStation:2",         1971009 },
            { "SpaceCenter/AstronautComplex:1",        1971010 },
            { "SpaceCenter/AstronautComplex:2",        1971011 },
            { "SpaceCenter/LaunchPad:1",               1971012 },
            { "SpaceCenter/LaunchPad:2",               1971013 },
            { "SpaceCenter/Runway:1",                  1971014 },
            { "SpaceCenter/Runway:2",                  1971015 },
            { "SpaceCenter/Administration:1",          1971016 },
            { "SpaceCenter/Administration:2",          1971017 },
        };

        // CelestialBody.name -> AP location ID for flag plants
        internal static readonly Dictionary<string, long> BodyToFlagLocationID =
            new Dictionary<string, long>
        {
            // Kerbin system
            { "Kerbin",  1972000 },
            { "Mun",     1972001 },
            { "Minmus",  1972002 },
            // Inner planets
            { "Moho",    1972003 },
            { "Eve",     1972004 },
            { "Gilly",   1972005 },
            // Duna system
            { "Duna",    1972006 },
            { "Ike",     1972007 },
            // Dres
            { "Dres",    1972008 },
            // Jool moons (Jool itself is a gas giant - no flag location)
            { "Laythe",  1972009 },
            { "Vall",    1972010 },
            { "Tylo",    1972011 },
            { "Bop",     1972012 },
            { "Pol",     1972013 },
            // Eeloo
            { "Eeloo",   1972014 },
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
            // Tier 4
            { "Parts: Aviation",                      "aviation" },
            { "Parts: Basic Science",                 "basicScience" },
            { "Parts: Flight Control",                "flightControl" },
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
            int skipped = 0, applied = 0;
            while (APState.PendingItems.Count > 0)
            {
                var (index, name) = APState.PendingItems.Peek();
                // Already recorded in the save: restore in-memory state only, no side effects.
                if (APKSPPersistence.Instance?.IsApplied(index) == true)
                {
                    RestoreItemState(name);
                    APState.PendingItems.Dequeue();
                    skipped++;
                    continue;
                }
                Log.Info($"[Flush] Applying new item [{index}]: {name}");
                if (!TryApplyItem(name))
                {
                    Log.Warn($"[Flush] TryApplyItem blocked on [{index}]: {name} - will retry later");
                    break;
                }
                APKSPPersistence.Instance?.MarkApplied(index);
                APState.PendingItems.Dequeue();
                applied++;
                Log.Info($"[Flush] Applied item [{index}]: {name}");
            }
            if (skipped > 0 || applied > 0)
                Log.Info($"[Flush] Done: {applied} applied, {skipped} restored from save. "
                       + $"Bundles={APState.ReceivedPartBundles.Count} "
                       + $"Permits={APState.PermittedBodies.Count}");
        }

        // Restore in-memory state for an already-applied item without re-triggering
        // side effects (no funds/rep grant, no AddExperimentalPart calls).
        // Called by FlushPendingItems when skipping a save-recorded item.
        private static void RestoreItemState(string itemName)
        {
            if (PermitToBody.TryGetValue(itemName, out string body))
            {
                APState.PermittedBodies.Add(body);
                return;
            }
            if (APState.PartBundleToTechID.TryGetValue(itemName, out string techID))
            {
                APState.ReceivedPartBundles.Add(techID);
                return;
            }
            // Filler items and unknown items have no in-memory state to restore.
        }

        // Mark all parts in a tech node as experimental so they are usable in career
        // mode without researching the node in R&D. This keeps the tech tree intact
        // as a separate system purely for location checks.
        internal static void AddExperimentalPartsForTech(string techID)
        {
            if (string.IsNullOrEmpty(techID) || PartLoader.Instance == null) return;
            if (ResearchAndDevelopment.Instance == null)
            {
                Log.Warn($"[ExpParts] R&D not available - cannot add parts for {techID}");
                return;
            }
            int count = 0;
            foreach (var p in PartLoader.Instance.loadedParts)
            {
                if (p != null && p.TechRequired == techID)
                {
                    ResearchAndDevelopment.AddExperimentalPart(p);
                    count++;
                }
            }
            Log.Info($"[ExpParts] {count} parts marked experimental for tech: {techID}");
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

            // Part bundles: record receipt and unlock the tech node so parts are usable.
            // The VAB ExcludeFilter also reads ReceivedPartBundles for visibility.
            // The "start" node cannot be researched; it is filter-only.
            if (APState.PartBundleToTechID.TryGetValue(itemName, out string techID))
            {
                APState.ReceivedPartBundles.Add(techID);
                AddExperimentalPartsForTech(techID);
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
                APState.StartingPartNames.Add(APState.StartingPod);
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
        private string serverField   = "";
        private string slotField     = "";
        private string passwordField = "";
        internal string StatusText = "Bridge offline - run KSPClient.py first";
        private bool wasConnected = false;
        private bool pollingStarted = false;

        void Start()
        {
            // Resume polling on scene transitions if the user already connected earlier.
            if (APState.UserHasConnected)
                StartPolling();
        }

        void OnDestroy() { }

        private void StartPolling()
        {
            if (pollingStarted) return;
            pollingStarted = true;
            StartCoroutine(PollStatus());
            StartCoroutine(PollItems());
        }

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
                APState.UserHasConnected = true;
                StartPolling();
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
                            if (s.connected) APState.ConnectedSlot = s.slot;
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

            APState.ResyncInProgress = true;
            try
            {
                Log.Info("Resync: clearing in-memory AP state.");
                APState.LastAppliedIndex = 0;
                APState.PendingItems.Clear();
                APState.ReceivedPartBundles.Clear();
                APState.PermittedBodies.Clear();
                APState.PermittedBodies.Add("Kerbin");
                APState.PermittedBodies.Add("Sun");

                Log.Info("Resync: fetching /items from bridge...");
                using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/items"))
                {
                    yield return req.SendWebRequest();
                    if (!req.isNetworkError && !req.isHttpError)
                    {
                        var resp = JsonUtility.FromJson<ItemsResponse>(req.downloadHandler.text);
                        if (resp?.items != null)
                        {
                            Log.Info($"Resync: received {resp.items.Length} total items from bridge.");
                            APBridge.EnqueueNewItems(resp.items);
                        }
                    }
                    else Log.Warn($"Resync: /items fetch failed: {req.error}");
                }
                Log.Info($"Resync: flushing {APState.PendingItems.Count} pending items...");
                APBridge.FlushPendingItems();
                Log.Info($"Resync: after flush - {APState.ReceivedPartBundles.Count} bundles, "
                       + $"{APState.PermittedBodies.Count} permitted bodies.");
            }
            finally
            {
                APState.ResyncInProgress = false;
            }

            // Re-apply experimental parts for all received bundles (in case R&D was
            // unavailable when the item was first applied in a previous scene).
            Log.Info("Resync: re-applying experimental parts for all received bundles...");
            foreach (string tid in APState.ReceivedPartBundles)
                APBridge.AddExperimentalPartsForTech(tid);

            // Refresh VAB filter if we are in the editor.
            ArchipelagoKSPEditor.Instance?.ApplyEditorFilter();

            // Re-post checks for techs the player researched but whose check was missed.
            // Tech nodes are never AP-unlocked; Available always means player-researched.
            if (rnd != null)
            {
                int recheckCount = 0;
                foreach (var kv in APState.TechIDToLocationID)
                {
                    var techState = rnd.GetTechState(kv.Key);
                    if (techState != null && techState.state == RDTech.State.Available)
                    {
                        APState.SentChecks.Remove(kv.Value);
                        StartCoroutine(APBridge.PostCheck(this, kv.Value));
                        recheckCount++;
                    }
                }
                if (recheckCount > 0)
                    Log.Info($"Resync: re-queued {recheckCount} tech location checks.");
            }
            else Log.Warn("Resync: R&D instance null - skipping tech check re-post.");

            // Re-post checks for KSC facility upgrades completed while bridge was offline.
            // FacilityToLocationID keys are "facilityId:N" where N is the 0-indexed required level.
            // Use UpgradeableFacility instance methods: GetNormLevel() [0,1] and GetLevelCount()
            // [total levels]. ScenarioUpgradeableFacilities static string-overloads are unreliable
            // (GetFacilityLevel returns 0 for all facilities; GetFacilityLevelCount returns max
            // index not count and can return -1 in career per Kerbalism notes).
            var facilityLookup = new Dictionary<string, UpgradeableFacility>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in FindObjectsOfType<UpgradeableFacility>())
                facilityLookup[f.id] = f;
            int facRecheckCount = 0;
            foreach (var kv in APState.FacilityToLocationID)
            {
                int colonIdx = kv.Key.LastIndexOf(':');
                if (colonIdx < 0) continue;
                string facilityId = kv.Key.Substring(0, colonIdx);
                if (!int.TryParse(kv.Key.Substring(colonIdx + 1), out int requiredLevel)) continue;

                if (!facilityLookup.TryGetValue(facilityId, out var fac))
                {
                    Log.Warn($"Resync: facility not found: {facilityId}");
                    continue;
                }
                int maxLevelIdx = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facilityId);
                if (maxLevelIdx <= 0) maxLevelIdx = 2;
                int currentLevel = Mathf.RoundToInt(fac.GetNormLevel() * maxLevelIdx);
                Log.Info($"Resync: {facilityId} level {currentLevel}/{maxLevelIdx} (required {requiredLevel})");
                if (currentLevel >= requiredLevel)
                {
                    APState.SentChecks.Remove(kv.Value);
                    StartCoroutine(APBridge.PostCheck(this, kv.Value));
                    facRecheckCount++;
                }
            }
            Log.Info($"Resync: re-queued {facRecheckCount} facility location checks.");

            // Re-post checks for flags planted while bridge was offline.
            // Flag vessels persist in flightState.protoVessels; body is the orbit reference body.
            int flagRecheckCount = 0;
            var flightState = HighLogic.fetch?.currentGame?.flightState;
            if (flightState != null)
            {
                var flaggedBodies = new HashSet<string>();
                foreach (var pv in flightState.protoVessels)
                {
                    if (pv.vesselType != VesselType.Flag) continue;
                    int bodyIdx = pv.orbitSnapShot.ReferenceBodyIndex;
                    if (bodyIdx >= 0 && bodyIdx < FlightGlobals.Bodies.Count)
                    {
                        string bodyName = FlightGlobals.Bodies[bodyIdx].name;
                        flaggedBodies.Add(bodyName);
                        Log.Info($"Resync: flag found on {bodyName} (vessel: {pv.vesselName})");
                    }
                }
                Log.Info($"Resync: {flaggedBodies.Count} flagged bodies: [{string.Join(", ", flaggedBodies)}]");
                foreach (var kv in APState.BodyToFlagLocationID)
                {
                    if (flaggedBodies.Contains(kv.Key))
                    {
                        APState.SentChecks.Remove(kv.Value);
                        StartCoroutine(APBridge.PostCheck(this, kv.Value));
                        flagRecheckCount++;
                    }
                }
            }
            else Log.Warn("Resync: flightState null - skipping flag check re-post.");
            if (flagRecheckCount > 0)
                Log.Info($"Resync: re-queued {flagRecheckCount} flag location checks.");

            Log.Info("Resync complete.");
            StatusText = wasConnected ? $"Connected  as: {APState.ConnectedSlot}  (synced)" : StatusText;
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
        void Start()
        {
            gameObject.AddComponent<APConnectionUI>();

            foreach (var f in FindObjectsOfType<UpgradeableFacility>())
                Log.Info($"Facility ID: {f.id}");

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
        void Start()
        {
            gameObject.AddComponent<APConnectionUI>();

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
            if (!APState.PermittedBodies.Contains(body) && !APState.ResyncInProgress)
            {
                Log.Warn($"EXPLODING - entered unpermitted SOI: {body}");
                ScreenMessages.PostScreenMessage(
                    $"[APKSP] No permit for {body}!  Vessel destroyed.",
                    5f, ScreenMessageStyle.UPPER_CENTER);
                data.host.rootPart.explode();
            }
            else if (!APState.PermittedBodies.Contains(body))
            {
                Log.Info($"SOI entered: {body} (resync in progress - enforcement deferred)");
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

        private EditorPartListFilter<AvailablePart> partFilter;

        // Names of the 3 seed-selected starting parts (from any tech node).
        private readonly HashSet<string> allowedStartPartNames = new HashSet<string>();

        // Guard: dump all part names only once per KSP session.
        private static bool partNamesDumped = false;

        void Awake() { Instance = this; }

        void Start()
        {
            gameObject.AddComponent<APConnectionUI>();
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
            // Wait for the user to explicitly connect before doing anything.
            while (!APState.UserHasConnected)
                yield return new WaitForSeconds(1f);

            // Wait for EditorPartList to be ready (mirrors JanitorsCloset pattern).
            while (EditorPartList.Instance == null)
                yield return new WaitForSeconds(0.1f);

            // Initial items fetch.
            Log.Info("[Editor] EditorPartList ready. Fetching initial items...");
            using (var req = UnityWebRequest.Get(APState.BridgeUrl + "/items"))
            {
                yield return req.SendWebRequest();
                if (!req.isNetworkError && !req.isHttpError)
                {
                    var resp = JsonUtility.FromJson<ItemsResponse>(req.downloadHandler.text);
                    if (resp?.items != null)
                    {
                        Log.Info($"[Editor] Initial fetch: {resp.items.Length} items. Flushing...");
                        APBridge.EnqueueNewItems(resp.items);
                        APBridge.FlushPendingItems();
                    }
                }
                else Log.Warn($"[Editor] Initial /items fetch failed: {req.error}");
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
                {
                    Log.Info($"[Editor] State changed (items:{APState.LastAppliedIndex} pod:{APState.StartingPod}). Re-applying filter.");
                    ApplyEditorFilter();
                }
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

            // Re-apply experimental parts so received-bundle parts are usable in builds.
            foreach (string tid in APState.ReceivedPartBundles)
                APBridge.AddExperimentalPartsForTech(tid);

            partFilter = new EditorPartListFilter<AvailablePart>("APKSP", criteria);
            EditorPartList.Instance.ExcludeFilters.AddFilter(partFilter);
            EditorPartList.Instance.Refresh();

            Log.Info($"[Filter] Applied. Bundles={APState.ReceivedPartBundles.Count} "
                   + $"StartParts={allowedStartPartNames.Count} Pod={APState.StartingPod} "
                   + $"Exp={APState.StartingExperimentID}");

            // One-time dump of all AvailablePart.name values so we can verify
            // that slot_data part names (starting_pod etc.) match KSP internal names.
            if (!partNamesDumped && PartLoader.Instance != null)
            {
                partNamesDumped = true;
                Log.Info($"[PartDump] AP starting_pod={APState.StartingPod} "
                       + $"starting_chute={APState.StartingChute} "
                       + $"starting_srb={APState.StartingSRB} "
                       + $"starting_experiment_id={APState.StartingExperimentID}");
                Log.Info($"[PartDump] Resolved allowedStartPartNames={string.Join(", ", allowedStartPartNames)}");
                var allNames = PartLoader.Instance.loadedParts
                    .Where(p => p != null)
                    .Select(p => $"{p.name} (tech={p.TechRequired})")
                    .OrderBy(s => s)
                    .ToList();
                Log.Info($"[PartDump] Total loaded parts: {allNames.Count}");
                foreach (var entry in allNames)
                    Log.Info($"[PartDump]   {entry}");
            }
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
