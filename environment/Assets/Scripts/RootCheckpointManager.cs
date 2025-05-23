using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RootCheckpointManager : MonoBehaviour
{
    [Header("Core References")]
    public CarController car;
    public CarAgent carAgent; // Assign your Car (with CarAgent script)
    public List<Checkpoint> allCheckpoints = new List<Checkpoint>();
    public Checkpoint initialCheckpoint;

    [Header("ML-Agent Settings")]
    [Tooltip("If true, car's position will be reset on crash. Set to false if agent should learn to recover or episode ends without repositioning by manager.")]
    public bool resetPositionOnCrashDuringTraining = true;

    [Header("Specific Checkpoint References")]
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
    public Checkpoint checkpoint9_Ref;

    private Checkpoint _currentMainSavedCheckpoint; // Determines spawn index on crash
    private List<Checkpoint> _currentConcurrentSavedCheckpoints = new List<Checkpoint>();

    [Header("Complex Checkpoint Runtime States")]
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

    [HideInInspector] public bool cp7_2_canBePassedAfterStop = false;
    private bool cp7_2_isMonitoringForStop = false;
    private float cp7_2_timeCarActuallyStopped = -1f;
    private const float CP7_2_STOP_CONFIRMATION_DELAY = 1.0f;
    private CarController cp7_2_carRefForMonitoring;

    public LevelCrossingLight levelCrossingLightForCP8;

    void Start()
    {
        if (car == null) car = FindObjectOfType<CarController>();
        if (carAgent == null && car != null) carAgent = car.GetComponent<CarAgent>();

        if (car == null) Debug.LogError("CRITICAL: CarController not found by RootCheckpointManager.");
        if (carAgent == null) Debug.LogWarning("CarAgent not found/assigned. ML-Agent features (rewards, penalties, episode ends) will be disabled.");

        PopulateAllCheckpointsIfNeeded();

        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint();
        }

        Checkpoint effectiveStartingCheckpoint = initialCheckpoint ?? checkpoint1_Ref;

        if (effectiveStartingCheckpoint != null)
        {
            DefineSegmentStart(effectiveStartingCheckpoint, null); // No "previously cleared" checkpoint at game start
        }
        else
        {
            Debug.LogError("CRITICAL: No starting checkpoint defined in RootCheckpointManager!");
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
            checkpoint9_Ref
        };
        foreach (var sRef in specificRefs)
        {
            if (sRef != null && !allCheckpoints.Contains(sRef)) allCheckpoints.Add(sRef);
        }
    }

    public void DefineSegmentStart(Checkpoint mainCheckpoint, Checkpoint justClearedCheckpointForReward, params Checkpoint[] concurrentCheckpoints)
    {
        if (carAgent != null && justClearedCheckpointForReward != null)
        {
            // CarAgent's AgentClearedStage method should handle its own logic for preventing duplicate rewards
            // if it's called multiple times for the same checkpoint instance without progress.
            carAgent.AgentClearedStage(justClearedCheckpointForReward);
        }

        if (car == null) { Debug.LogError("Car reference not set! Cannot define segment start."); return; }
        if (mainCheckpoint == null) { Debug.LogError("MainCheckpoint for DefineSegmentStart is null!"); return; }

        _currentMainSavedCheckpoint = mainCheckpoint;
        _currentConcurrentSavedCheckpoints.Clear();
        if (concurrentCheckpoints != null)
            _currentConcurrentSavedCheckpoints.AddRange(concurrentCheckpoints.Where(cp => cp != null));

        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint();
        }
        
        ResetCheckpointInternalStatesForNewSegment(); // Reset complex states BEFORE activating new ones

        mainCheckpoint.ActivateCheckpoint();
        foreach (var cp in _currentConcurrentSavedCheckpoints)
        {
            if (cp != null) cp.ActivateCheckpoint();
        }
        Debug.Log($"Segment defined. Main CP for reset: {mainCheckpoint?.name} (SpawnIndex: {mainCheckpoint?.spawnPointIndex}). Last cleared for reward: {justClearedCheckpointForReward?.name}");
    }

    // Primary method for Checkpoint scripts to call when a stage is passed
    public void AdvanceToSegment(Checkpoint nextMainCheckpoint, Checkpoint currentlyPassedCheckpoint, params Checkpoint[] nextConcurrentCheckpoints)
    {
        if (nextMainCheckpoint == null) { Debug.LogError("AdvanceToSegment called with a null nextMainCheckpoint."); return; }
        Debug.Log($"Advancing from {currentlyPassedCheckpoint?.name} to segment. Next Main: {nextMainCheckpoint?.name}");
        DefineSegmentStart(nextMainCheckpoint, currentlyPassedCheckpoint, nextConcurrentCheckpoints);
    }

    // Overload if a checkpoint doesn't explicitly state what it just cleared (less ideal for rewards)
    public void AdvanceToSegment(Checkpoint nextMainCheckpoint, params Checkpoint[] nextConcurrentCheckpoints)
    {
        AdvanceToSegment(nextMainCheckpoint, null, nextConcurrentCheckpoints);
    }

    public void HandleCrash()
    {
        Debug.Log("CRASH DETECTED!");
        if (carAgent != null)
        {
            carAgent.AgentCrashed(); // Agent handles penalty and EndEpisode
        }

        if (levelCrossingLightForCP8 != null && levelCrossingLightForCP8.isLightActive)
        {
            Debug.Log("HandleCrash: Deactivating Level Crossing Light for CP8.");
            levelCrossingLightForCP8.SetLightActive(false);
        }

        if (resetPositionOnCrashDuringTraining)
        {
            if (car == null) { Debug.LogError("Car ref null in HandleCrash for position reset."); }
            else if (_currentMainSavedCheckpoint != null)
            {
                car.ResetState(_currentMainSavedCheckpoint.spawnPointIndex);
            }
            else
            {
                Checkpoint fallback = initialCheckpoint ?? checkpoint1_Ref;
                if (fallback != null && car != null) car.ResetState(fallback.spawnPointIndex);
                else Debug.LogError("No fallback checkpoint for crash reset position.");
            }
        } else {
             Debug.Log("HandleCrash: resetPositionOnCrashDuringTraining is FALSE. Car position not reset by RootManager.");
        }

        ResetCheckpointInternalStatesForFailedAttempt();
        ReactivateSavedSegment();
    }

    void ResetCheckpointInternalStatesForNewSegment()
    {
        checkpoint3_1_hitByCorrectWheel = false; checkpoint3_2_hitByCorrectWheel = false;
        ResetCP6StateAndLight(); ResetCP7_2State();
        // Debug.Log("Internal states (CP3, CP6, CP7-2) reset for new segment.");
    }

    void ResetCheckpointInternalStatesForFailedAttempt()
    {
        checkpoint3_1_hitByCorrectWheel = false; checkpoint3_2_hitByCorrectWheel = false;
        ResetCP6StateAndLight(); ResetCP7_2State();
        // Debug.Log("Internal states (CP3, CP6, CP7-2) reset after failed attempt.");
    }
    
    private void ResetCP6StateAndLight()
    {
        SetCheckpoint6LightActiveState(false); cp6_isMonitoringForStop = false;
        cp6_timeCarActuallyStopped = -1f; cp6_carRefForMonitoring = null;
    }

    private void ResetCP7_2State()
    {
        cp7_2_canBePassedAfterStop = false; cp7_2_isMonitoringForStop = false;
        cp7_2_timeCarActuallyStopped = -1f; cp7_2_carRefForMonitoring = null;
    }

     void ReactivateSavedSegment()
    {
        Debug.Log("Reactivating saved segment.");
        foreach (var cp in allCheckpoints) { if (cp != null) cp.DeactivateCheckpoint(); }
        
        if (_currentMainSavedCheckpoint != null) {
            _currentMainSavedCheckpoint.ActivateCheckpoint();
            foreach (var cp_concurrent in _currentConcurrentSavedCheckpoints) { if (cp_concurrent != null) cp_concurrent.ActivateCheckpoint(); }
        } else {
            Checkpoint fallback = initialCheckpoint ?? checkpoint1_Ref;
            if (fallback != null) DefineSegmentStart(fallback, null); // Re-define segment from fallback
            else Debug.LogError("CRITICAL: No saved or initial checkpoint to reactivate!");
        }
    }

    public Checkpoint GetCheckpointByName(string name)
    {
        return allCheckpoints.FirstOrDefault(cp => cp != null && cp.name == name);
    }

    public Checkpoint GetCurrentTargetCheckpointForAgent(CarAgent agent)
    {
        // This heuristic finds an active checkpoint; refine for complex scenarios.
        if (checkpoint6_0_Ref != null && checkpoint6_0_Ref.isActive) return checkpoint6_0_Ref;
        if (checkpoint6_2_Ref != null && checkpoint6_2_Ref.isActive) return checkpoint6_2_Ref;
        if (checkpoint7_2_Ref != null && checkpoint7_2_Ref.isActive && !cp7_2_canBePassedAfterStop) return checkpoint7_2_Ref; // Target 7-2 if waiting for stop

        if (_currentMainSavedCheckpoint != null && _currentMainSavedCheckpoint.isActive)
            return _currentMainSavedCheckpoint;
        
        foreach (Checkpoint cp_search in allCheckpoints) {
            if (cp_search != null && cp_search.isActive) return cp_search;
        }
        return initialCheckpoint ?? checkpoint1_Ref;
    }

    public void ForceResetToGlobalStart(CarAgent requestingAgent)
    {
        Debug.Log("RootCheckpointManager: ForceResetToGlobalStart called by agent.");
        Checkpoint startCp = initialCheckpoint ?? checkpoint1_Ref;
        if (startCp != null)
        {
            if (car != null) car.ResetState(startCp.spawnPointIndex);
            DefineSegmentStart(startCp, null); // No reward for "previous" on global reset
            if (levelCrossingLightForCP8 != null) levelCrossingLightForCP8.SetLightActive(false);
        }
        else Debug.LogError("Cannot ForceResetToGlobalStart: No initialCheckpoint or checkpoint1_Ref defined.");
    }

    public void ReportCheckpoint3PartHit(bool isPart1, bool byCorrectWheel)
    {
        if (isPart1) checkpoint3_1_hitByCorrectWheel = byCorrectWheel; else checkpoint3_2_hitByCorrectWheel = byCorrectWheel;
        if (checkpoint3_1_hitByCorrectWheel && checkpoint3_2_hitByCorrectWheel)
        {
            Debug.Log("CP3 completed!");
            Checkpoint cp3EndRef = checkpoint3_2_Ref ?? checkpoint3_1_Ref; // For reward context

            if(checkpoint3_1_Ref) checkpoint3_1_Ref.DeactivateCheckpoint();
            if(checkpoint3_2_Ref) checkpoint3_2_Ref.DeactivateCheckpoint();

            if (checkpoint4_1_Ref != null) AdvanceToSegment(checkpoint4_1_Ref, cp3EndRef);
            else Debug.LogError("CP4-1 Ref not set for CP3 completion!");
        }
    }

    public TrafficLight.LightState GetTrafficLight5State() { return trafficLightForCP5 != null ? trafficLightForCP5.currentLightState : TrafficLight.LightState.Green; }
    
    private void SetCheckpoint6VisualLight(bool makeYellow) { if (checkpoint6LightIndicatorRenderer != null) checkpoint6LightIndicatorRenderer.material = makeYellow ? cp6_YellowMaterial : cp6_DefaultMaterial; }
    
    public void SetCheckpoint6LightActiveState(bool isLightNowUp) { 
        isCheckpoint6LightUp = isLightNowUp; 
        SetCheckpoint6VisualLight(isLightNowUp); 
        // Debug.Log($"RCM: CP6 Light -> {(isLightNowUp ? "ON" : "OFF")}."); 
    }
    
    public void StartCheckingForCP6StopCondition(CarController carForMonitoring) { 
        cp6_carRefForMonitoring = carForMonitoring; cp6_isMonitoringForStop = true; 
        cp6_timeCarActuallyStopped = -1f; SetCheckpoint6LightActiveState(false); 
        Debug.Log("RCM: Start CP6 Stop Check"); 
    }
    
    private void HandleCheckpoint6StopCondition() {
        if (!cp6_isMonitoringForStop || isCheckpoint6LightUp || cp6_carRefForMonitoring == null) return;
        if (cp6_carRefForMonitoring.IsFullyStopped()) {
            if (cp6_timeCarActuallyStopped < 0f) { cp6_timeCarActuallyStopped = Time.time; /*Debug.Log("RCM: CP6 - Car stopped. Timer started.");*/ }
            if (Time.time - cp6_timeCarActuallyStopped >= CP6_STOP_CONFIRMATION_DELAY) {
                SetCheckpoint6LightActiveState(true); cp6_isMonitoringForStop = false; cp6_timeCarActuallyStopped = -1f;
                Debug.Log("RCM: CP6 - 1s passed. Light UP.");
            }
        } else {
            if (cp6_timeCarActuallyStopped >= 0f) { /*Debug.Log("RCM: CP6 - Car moved. Reset timer.");*/ }
            cp6_timeCarActuallyStopped = -1f;
        }
    }
    
    public void ResetCheckpoint6Sequence() { 
        Debug.Log("RCM: Reset CP6 Seq (Loopback to 6-1 / Crash).");
        // The HandleCrash call was in the version user provided as "current"
        if (carAgent != null) HandleCrash(); // This will trigger agent penalty & potential position reset
        else { // If not using agent, just reset CP6 state and activate CP6-1
            ResetCP6StateAndLight(); 
            if(checkpoint6_0_Ref && checkpoint6_0_Ref.isActive) checkpoint6_0_Ref.DeactivateCheckpoint();
            if(checkpoint6_2_Ref && checkpoint6_2_Ref.isActive) checkpoint6_2_Ref.DeactivateCheckpoint();
            if(checkpoint6_1_Ref) checkpoint6_1_Ref.ActivateCheckpoint();
            else Debug.LogError("CP6-1 Ref null for simple reset!");
        }
    }
    
    public void CompleteCheckpoint6AndAdvance() {
        Debug.Log("RCM: CP6 PASSED. Advancing to CP7-1.");
        Checkpoint cp6EndRef = checkpoint6_2_Ref ?? checkpoint6_0_Ref ?? checkpoint6_1_Ref; // For reward context

        ResetCP6StateAndLight();
        if (checkpoint6_0_Ref != null && checkpoint6_0_Ref.isActive) checkpoint6_0_Ref.DeactivateCheckpoint();
        if (checkpoint6_1_Ref != null && checkpoint6_1_Ref.isActive) checkpoint6_1_Ref.DeactivateCheckpoint();
        if (checkpoint6_2_Ref != null && checkpoint6_2_Ref.isActive) checkpoint6_2_Ref.DeactivateCheckpoint();
        
        if (checkpoint7_1_Ref != null) AdvanceToSegment(checkpoint7_1_Ref, cp6EndRef);
        else Debug.LogError("Cannot advance from CP6: checkpoint7_1_Ref is null!");
    }
    
    public void InitiateCheckpoint7_2StopCondition(CarController carToMonitor) { 
        cp7_2_carRefForMonitoring = carToMonitor; cp7_2_isMonitoringForStop = true; 
        cp7_2_canBePassedAfterStop = false; cp7_2_timeCarActuallyStopped = -1f; 
        Debug.Log("RCM: Start CP7-2 Stop Check");
    }
    
    private void HandleCheckpoint7_2StopCondition() {
        if (!cp7_2_isMonitoringForStop || cp7_2_canBePassedAfterStop || cp7_2_carRefForMonitoring == null) return;
        if (cp7_2_carRefForMonitoring.IsFullyStopped()) {
            if (cp7_2_timeCarActuallyStopped < 0f) { cp7_2_timeCarActuallyStopped = Time.time; /*Debug.Log("RCM: CP7-2 - Car stopped. Timer started.");*/ }
            if (Time.time - cp7_2_timeCarActuallyStopped >= CP7_2_STOP_CONFIRMATION_DELAY) {
                cp7_2_canBePassedAfterStop = true; cp7_2_isMonitoringForStop = false; cp7_2_timeCarActuallyStopped = -1f;
                Debug.Log("RCM: CP7-2 - 1s passed. Can be passed.");
            }
        } else {
            if (cp7_2_timeCarActuallyStopped >= 0f) { /*Debug.Log("RCM: CP7-2 - Car moved. Reset timer.");*/ }
            cp7_2_timeCarActuallyStopped = -1f;
        }
    }
    
    public void LoopBackToCheckpoint7_1() { 
        Debug.Log("RCM: Loop to CP7-1 (CP7-2 touched too early / Crash).");
        if (carAgent != null) HandleCrash(); // This will trigger agent penalty & potential position reset
        else { // If not using agent, just reset CP7 state and activate CP7-1
            ResetCP7_2State(); 
            if(checkpoint7_2_Ref && checkpoint7_2_Ref.isActive) checkpoint7_2_Ref.DeactivateCheckpoint();
            if(checkpoint7_1_Ref) checkpoint7_1_Ref.ActivateCheckpoint();
            else Debug.LogError("CP7-1 Ref null for simple loop!");
        }
    }
    
    public void LoopBackToCheckpoint8_1()
    {
        Debug.Log("RootCheckpointManager: Looping back to Checkpoint 8-1 due to early touch on CP8-2 (light was on). This will trigger a crash sequence.");

        // Your logic included HandleCrash() here, making this loop-back act like a full crash and reset
        // before re-activating Checkpoint 8-1.
        HandleCrash(); 
        
        // After HandleCrash, the car is reset to the start of the segment that was active 
        // *before* this LoopBackToCheckpoint8_1 method was called (which should be the segment started by CP8-1).
        // Then, AdvanceToSegment re-affirms that CP8-1 is the active segment.
        // No positive "stage clear" reward is given for CP8-2 because this is a failure/loop-back path.
        if (checkpoint8_1_Ref != null)
        {
            // The 'null' here means no specific checkpoint is being marked as "just successfully cleared" for reward purposes.
            AdvanceToSegment(checkpoint8_1_Ref); 
        }
        else
        {
            Debug.LogError("RootCheckpointManager: Cannot loop back to CP8-1 because 'checkpoint8_1_Ref' is null! Assign it in the Inspector.");
        }
    }
    
    public bool IsLevelCrossingLight8Active() { return levelCrossingLightForCP8 != null && levelCrossingLightForCP8.isLightActive; }
    public void ActivateLevelCrossingLight8(bool activeState) { if (levelCrossingLightForCP8 != null) levelCrossingLightForCP8.SetLightActive(activeState); }
}