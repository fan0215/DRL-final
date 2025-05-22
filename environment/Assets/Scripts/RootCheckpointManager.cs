using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RootCheckpointManager : MonoBehaviour
{
    public CarController car;
    public List<Checkpoint> allCheckpoints = new List<Checkpoint>();
    public Checkpoint initialCheckpoint;

    [Header("Specific Checkpoint References (Assign in Inspector)")]
    public Checkpoint checkpoint1_Ref;
    public Checkpoint checkpoint2_1_Ref;
    public Checkpoint checkpoint2_2_Ref;
    public Checkpoint checkpoint3_1_Ref;
    public Checkpoint checkpoint3_2_Ref;
    public Checkpoint checkpoint4_1_Ref;
    public Checkpoint checkpoint4_2_Ref;
    public Checkpoint checkpoint5_1_Ref;
    public Checkpoint checkpoint5_2_Ref;
    public Checkpoint checkpoint6_0_Ref;
    public Checkpoint checkpoint6_1_Ref;
    public Checkpoint checkpoint6_2_Ref;
    public Checkpoint checkpoint7_1_Ref;
    public Checkpoint checkpoint7_2_Ref;
    public Checkpoint checkpoint8_1_Ref;
    public Checkpoint checkpoint8_2_Ref;
    public Checkpoint checkpoint9_Ref; // <-- NEW REFERENCE

    // ... (savedCarPosition, savedCarRotation, _currentMainSavedCheckpoint, _currentConcurrentSavedCheckpoints remain as per last version for spawn index logic) ...
    private Checkpoint _currentMainSavedCheckpoint;
    private List<Checkpoint> _currentConcurrentSavedCheckpoints = new List<Checkpoint>();


    [Header("Complex Checkpoint States")]
    [HideInInspector] public bool checkpoint3_1_hitByCorrectWheel = false;
    [HideInInspector] public bool checkpoint3_2_hitByCorrectWheel = false;
    public TrafficLight trafficLightForCP5;

    [HideInInspector] public bool isCheckpoint6LightUp = false;
    private bool cp6_isMonitoringForStop = false;
    private float cp6_timeCarActuallyStopped = -1f;
    private const float CP6_STOP_CONFIRMATION_DELAY = 1.0f;
    private CarController cp6_carRefForMonitoring;
    public Renderer checkpoint6LightIndicatorRenderer;
    public Material cp6_DefaultMaterial;
    public Material cp6_YellowMaterial;

    [Header("Checkpoint 7-2 State (Managed by RootManager)")]
    [HideInInspector] public bool cp7_2_canBePassedAfterStop = false;
    private bool cp7_2_isMonitoringForStop = false;
    private float cp7_2_timeCarActuallyStopped = -1f;
    private const float CP7_2_STOP_CONFIRMATION_DELAY = 1.0f;
    private CarController cp7_2_carRefForMonitoring;

    public LevelCrossingLight levelCrossingLightForCP8;

    void Start()
    {
        if (car == null) car = FindObjectOfType<CarController>();
        if (car == null) Debug.LogError("CarController not found by RootCheckpointManager. Car interactions will fail.");

        PopulateAllCheckpointsIfNeeded();

        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint();
        }

        Checkpoint effectiveStartingCheckpoint = initialCheckpoint;
        if (effectiveStartingCheckpoint == null && checkpoint1_Ref != null)
        {
            Debug.LogWarning("InitialCheckpoint was not assigned in RootCheckpointManager. Using 'checkpoint1_Ref' as the starting checkpoint.");
            effectiveStartingCheckpoint = checkpoint1_Ref;
        }

        if (effectiveStartingCheckpoint != null)
        {
            DefineSegmentStart(effectiveStartingCheckpoint);
        }
        else
        {
            Debug.LogError("No starting checkpoint defined (neither 'Initial Checkpoint' nor 'Checkpoint1 Ref' is properly set and assigned) in RootCheckpointManager!");
        }
    }

    void Update()
    {
        HandleCheckpoint6StopCondition();
        HandleCheckpoint7_2StopCondition();
    }

    void PopulateAllCheckpointsIfNeeded()
    {
        var specificRefs = new List<Checkpoint> {
            checkpoint1_Ref, checkpoint2_1_Ref, checkpoint2_2_Ref, checkpoint3_1_Ref, checkpoint3_2_Ref,
            checkpoint4_1_Ref, checkpoint4_2_Ref, checkpoint5_1_Ref, checkpoint5_2_Ref,
            checkpoint6_0_Ref, checkpoint6_1_Ref, checkpoint6_2_Ref,
            checkpoint7_1_Ref, checkpoint7_2_Ref, checkpoint8_1_Ref, checkpoint8_2_Ref,
            checkpoint9_Ref // <-- ADDED CP9 Ref
        };
        foreach (var sRef in specificRefs)
        {
            if (sRef != null && !allCheckpoints.Contains(sRef))
            {
                allCheckpoints.Add(sRef);
            }
        }
    }

    public void DefineSegmentStart(Checkpoint mainCheckpoint, params Checkpoint[] concurrentCheckpoints)
    {
        if (car == null) { Debug.LogError("Car reference not set in RootCheckpointManager! Cannot define segment start."); return; }
        if (mainCheckpoint == null) { Debug.LogError("MainCheckpoint for DefineSegmentStart is null! Cannot define segment."); return; }

        _currentMainSavedCheckpoint = mainCheckpoint;
        _currentConcurrentSavedCheckpoints.Clear();
        if (concurrentCheckpoints != null)
            _currentConcurrentSavedCheckpoints.AddRange(concurrentCheckpoints.Where(cp => cp != null));

        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint();
        }

        ResetCheckpointInternalStatesForNewSegment();

        mainCheckpoint.ActivateCheckpoint();
        foreach (var cp in _currentConcurrentSavedCheckpoints)
        {
            if (cp != null) cp.ActivateCheckpoint();
        }
        Debug.Log($"Segment defined. Main CP for reset: {mainCheckpoint?.name} (will use its spawnPointIndex: {mainCheckpoint?.spawnPointIndex}).");
    }

    public void AdvanceToSegment(Checkpoint nextMainCheckpoint, params Checkpoint[] nextConcurrentCheckpoints)
    {
        if (nextMainCheckpoint == null) { Debug.LogError("AdvanceToSegment called with a null nextMainCheckpoint.", this); return; }
        Debug.Log($"Advancing to segment. Next Main: {nextMainCheckpoint?.name}");
        DefineSegmentStart(nextMainCheckpoint, nextConcurrentCheckpoints);
    }

    public void HandleCrash()
    {
        Debug.Log("CRASH DETECTED! Resetting car and checkpoint states.");

        if (levelCrossingLightForCP8 != null)
        {
            if (levelCrossingLightForCP8.isLightActive) // Optional: Check if it's even active before trying to deactivate
            {
                Debug.Log("HandleCrash: Deactivating Level Crossing Light for CP8.");
            }
            levelCrossingLightForCP8.SetLightActive(false); // Call this regardless to ensure it stops and resets
        }

        if (car == null) { Debug.LogError("Car reference null in HandleCrash. Cannot reset car.", this); return; }

        if (_currentMainSavedCheckpoint != null)
        {
            Debug.Log($"Resetting car to spawn index: {_currentMainSavedCheckpoint.spawnPointIndex} (from checkpoint: {_currentMainSavedCheckpoint.name})");
            car.ResetState(_currentMainSavedCheckpoint.spawnPointIndex);
        }
        else
        {
            Debug.LogError("Cannot HandleCrash: _currentMainSavedCheckpoint is null! Attempting fallback to initial or CP1's spawn index.", this);
            Checkpoint fallbackResetCp = initialCheckpoint ?? checkpoint1_Ref;
            if (fallbackResetCp != null)
            {
                Debug.LogWarning($"Attempting fallback reset to spawn index of: {fallbackResetCp.name} (Index: {fallbackResetCp.spawnPointIndex})");
                car.ResetState(fallbackResetCp.spawnPointIndex);
            }
            else
            {
                Debug.LogError("No fallback checkpoint available for crash reset. Car position not reset.");
            }
        }

        ResetCheckpointInternalStatesForFailedAttempt();
        ReactivateSavedSegment();
    }

    void ResetCheckpointInternalStatesForNewSegment()
    {
        checkpoint3_1_hitByCorrectWheel = false;
        checkpoint3_2_hitByCorrectWheel = false;
        ResetCP6StateAndLight();
        ResetCP7_2State();
        Debug.Log("Internal states (CP3, CP6, CP7-2) reset for new segment.");
    }

    void ResetCheckpointInternalStatesForFailedAttempt()
    {
        checkpoint3_1_hitByCorrectWheel = false;
        checkpoint3_2_hitByCorrectWheel = false;
        ResetCP6StateAndLight();
        ResetCP7_2State();
        Debug.Log("Internal states (CP3, CP6, CP7-2) reset after failed attempt.");
    }

    private void ResetCP6StateAndLight()
    {
        SetCheckpoint6LightActiveState(false);
        cp6_isMonitoringForStop = false;
        cp6_timeCarActuallyStopped = -1f;
        cp6_carRefForMonitoring = null;
    }

    private void ResetCP7_2State()
    {
        cp7_2_canBePassedAfterStop = false;
        cp7_2_isMonitoringForStop = false;
        cp7_2_timeCarActuallyStopped = -1f;
        cp7_2_carRefForMonitoring = null;
    }

    void ReactivateSavedSegment()
    {
        Debug.Log("Reactivating saved segment.");
        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint();
        }

        if (_currentMainSavedCheckpoint != null)
        {
            _currentMainSavedCheckpoint.ActivateCheckpoint();
            Debug.Log($"Reactivated main saved CP: {_currentMainSavedCheckpoint.name}");
            foreach (var cp in _currentConcurrentSavedCheckpoints)
            {
                if (cp != null)
                {
                    cp.ActivateCheckpoint();
                    Debug.Log($"Reactivated concurrent saved CP: {cp.name}");
                }
            }
        }
        else
        {
            Checkpoint effectiveFallbackStart = initialCheckpoint ?? checkpoint1_Ref;
            if (effectiveFallbackStart != null)
            {
                Debug.LogWarning("_currentMainSavedCheckpoint was null during ReactivateSavedSegment. Resetting to effective initial checkpoint and defining new segment start.");
                DefineSegmentStart(effectiveFallbackStart);
            }
            else
            {
                Debug.LogError("CRITICAL: No saved segment or any initial checkpoint to reset to during ReactivateSavedSegment!");
            }
        }
    }

    public Checkpoint GetCheckpointByName(string name)
    {
        return allCheckpoints.FirstOrDefault(cp => cp != null && cp.name == name);
    }

    public void ReportCheckpoint3PartHit(bool isPart1, bool byCorrectWheel)
    {
        if (isPart1) checkpoint3_1_hitByCorrectWheel = byCorrectWheel; else checkpoint3_2_hitByCorrectWheel = byCorrectWheel;
        if (checkpoint3_1_hitByCorrectWheel && checkpoint3_2_hitByCorrectWheel)
        {
            Debug.Log("CP3 completed!");
            if (checkpoint3_1_Ref) checkpoint3_1_Ref.DeactivateCheckpoint();
            if (checkpoint3_2_Ref) checkpoint3_2_Ref.DeactivateCheckpoint();
            if (checkpoint4_1_Ref != null) AdvanceToSegment(checkpoint4_1_Ref);
            else Debug.LogError("RootCheckpointManager: CP4-1 Ref not set for CP3 completion!");
        }
    }

    public TrafficLight.LightState GetTrafficLight5State()
    {
        if (trafficLightForCP5 != null) return trafficLightForCP5.currentLightState;
        Debug.LogWarning("TrafficLight for CP5 not assigned in RootManager. Defaulting to Green.");
        return TrafficLight.LightState.Green;
    }

    private void SetCheckpoint6VisualLight(bool makeYellow)
    {
        if (checkpoint6LightIndicatorRenderer != null)
        {
            checkpoint6LightIndicatorRenderer.material = makeYellow ? cp6_YellowMaterial : cp6_DefaultMaterial;
        }
    }

    public void SetCheckpoint6LightActiveState(bool isLightNowUp)
    {
        isCheckpoint6LightUp = isLightNowUp;
        SetCheckpoint6VisualLight(isLightNowUp);
        if (isCheckpoint6LightUp) Debug.Log("RootCheckpointManager: CP6 Light Active State set to -> TRUE (ON).");
        else Debug.Log("RootCheckpointManager: CP6 Light Active State set to -> FALSE (OFF).");
    }

    public void StartCheckingForCP6StopCondition(CarController carForMonitoring)
    {
        Debug.Log("RootCheckpointManager: Initiating CP6 stop condition check.");
        cp6_carRefForMonitoring = carForMonitoring;
        cp6_isMonitoringForStop = true;
        cp6_timeCarActuallyStopped = -1f;
        SetCheckpoint6LightActiveState(false);
    }

    private void HandleCheckpoint6StopCondition()
    {
        if (!cp6_isMonitoringForStop || isCheckpoint6LightUp || cp6_carRefForMonitoring == null) return;
        if (cp6_carRefForMonitoring.IsFullyStopped())
        {
            if (cp6_timeCarActuallyStopped < 0f)
            {
                cp6_timeCarActuallyStopped = Time.time;
                Debug.Log("RootCheckpointManager: CP6 - Car has stopped. Timer started for light activation.");
            }
            if (Time.time - cp6_timeCarActuallyStopped >= CP6_STOP_CONFIRMATION_DELAY)
            {
                Debug.Log("RootCheckpointManager: CP6 - 1 second passed since stop. Turning light UP.");
                SetCheckpoint6LightActiveState(true);
                cp6_isMonitoringForStop = false;
                cp6_timeCarActuallyStopped = -1f;
            }
        }
        else
        {
            if (cp6_timeCarActuallyStopped >= 0f) Debug.Log("RootCheckpointManager: CP6 - Car moved during stop check. Resetting stop timer.");
            cp6_timeCarActuallyStopped = -1f;
        }
    }

    public void ResetCheckpoint6Sequence()
    {
        Debug.Log("RootCheckpointManager: Resetting CP6 internal sequence. Looping back to CP6-1.");
        ResetCP6StateAndLight();
        HandleCrash();
        if (checkpoint6_0_Ref != null && checkpoint6_0_Ref.isActive) checkpoint6_0_Ref.DeactivateCheckpoint();
        if (checkpoint6_2_Ref != null && checkpoint6_2_Ref.isActive) checkpoint6_2_Ref.DeactivateCheckpoint();
        if (checkpoint6_1_Ref != null) checkpoint6_1_Ref.ActivateCheckpoint();
        else Debug.LogError("RootCheckpointManager: Cannot reset CP6 sequence: checkpoint6_1_Ref is null!");
    }

    public void CompleteCheckpoint6AndAdvance()
    {
        Debug.Log("RootCheckpointManager: CP6 PASSED. Advancing to CP7-1.");
        ResetCP6StateAndLight();
        if (checkpoint6_0_Ref != null && checkpoint6_0_Ref.isActive) checkpoint6_0_Ref.DeactivateCheckpoint();
        if (checkpoint6_1_Ref != null && checkpoint6_1_Ref.isActive) checkpoint6_1_Ref.DeactivateCheckpoint();
        if (checkpoint6_2_Ref != null && checkpoint6_2_Ref.isActive) checkpoint6_2_Ref.DeactivateCheckpoint();
        if (checkpoint7_1_Ref != null) AdvanceToSegment(checkpoint7_1_Ref);
        else Debug.LogError("RootCheckpointManager: Cannot advance from CP6: checkpoint7_1_Ref is null!");
    }

    public void InitiateCheckpoint7_2StopCondition(CarController carToMonitor)
    {
        Debug.Log("RootCheckpointManager: Initiating CP7-2 stop condition check.");
        cp7_2_carRefForMonitoring = carToMonitor;
        cp7_2_isMonitoringForStop = true;
        cp7_2_canBePassedAfterStop = false;
        cp7_2_timeCarActuallyStopped = -1f;
    }

    private void HandleCheckpoint7_2StopCondition()
    {
        if (!cp7_2_isMonitoringForStop || cp7_2_canBePassedAfterStop || cp7_2_carRefForMonitoring == null) return;
        if (cp7_2_carRefForMonitoring.IsFullyStopped())
        {
            if (cp7_2_timeCarActuallyStopped < 0f)
            {
                cp7_2_timeCarActuallyStopped = Time.time;
                Debug.Log("RootCheckpointManager: CP7-2 - Car has stopped. Timer started for passability.");
            }
            if (Time.time - cp7_2_timeCarActuallyStopped >= CP7_2_STOP_CONFIRMATION_DELAY)
            {
                Debug.Log("RootCheckpointManager: CP7-2 - 1 second passed since stop. CP7-2 can now be passed.");
                cp7_2_canBePassedAfterStop = true;
                cp7_2_isMonitoringForStop = false;
                cp7_2_timeCarActuallyStopped = -1f;
            }
        }
        else
        {
            if (cp7_2_timeCarActuallyStopped >= 0f) Debug.Log("RootCheckpointManager: CP7-2 - Car moved during stop check. Resetting stop timer.");
            cp7_2_timeCarActuallyStopped = -1f;
        }
    }

    public void LoopBackToCheckpoint7_1()
    {
        Debug.Log("RootCheckpointManager: CP7-2 touched too early. Looping back to CP7-1.");
        ResetCP7_2State();
        HandleCrash();
        if (checkpoint7_2_Ref != null && checkpoint7_2_Ref.isActive) checkpoint7_2_Ref.DeactivateCheckpoint();
        if (checkpoint7_1_Ref != null) checkpoint7_1_Ref.ActivateCheckpoint();
        else Debug.LogError("RootCheckpointManager: Cannot loop back to CP7-1: checkpoint7_1_Ref is null!");
    }

    public bool IsLevelCrossingLight8Active()
    {
        if (levelCrossingLightForCP8 != null) return levelCrossingLightForCP8.isLightActive;
        Debug.LogWarning("LevelCrossingLight for CP8 not assigned in RootManager. Defaulting to false.");
        return false;
    }
    public void ActivateLevelCrossingLight8(bool activeState)
    {
        if (levelCrossingLightForCP8 != null) levelCrossingLightForCP8.SetLightActive(activeState);
        else Debug.LogWarning("LevelCrossingLight for CP8 not assigned in RootManager. Cannot set active state.");
    }
    
    // Add this new public method:
    public void LoopBackToCheckpoint8_1()
    {
        Debug.Log("RootCheckpointManager: Looping back to Checkpoint 8-1 due to early touch on CP8-2.");

        // It's important to treat this as starting a new segment defined by Checkpoint 8-1.
        // This will handle deactivating CP8-2 (and any others), activating CP8-1,
        // and setting CP8-1 as the current checkpoint to reset to if a crash happens next.
        // It also calls ResetCheckpointInternalStatesForNewSegment().
        HandleCrash();
        if (checkpoint8_1_Ref != null)
        {
            AdvanceToSegment(checkpoint8_1_Ref);
            // When Checkpoint8_1 is activated, it will wait for a front wheel hit.
            // Upon that hit, its own logic (in Checkpoint8_1.cs) will ensure the
            // level crossing light is activated (or re-confirmed) and then activate Checkpoint8_2 again.
            // The level crossing light should ideally remain ON from its first activation by 8-1,
            // and 8-1's logic would just re-confirm it if needed.
        }
        else
        {
            Debug.LogError("RootCheckpointManager: Cannot loop back to CP8-1 because 'checkpoint8_1_Ref' is null! Assign it in the Inspector.");
            // As a fallback, you might consider a full crash reset if 8-1 can't be activated.

        }
    }
}