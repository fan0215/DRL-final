using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RootCheckpointManager : MonoBehaviour
{
    public CarController car;
    public List<Checkpoint> allCheckpoints = new List<Checkpoint>(); // Populate in Inspector or have CPs register
    public Checkpoint initialCheckpoint;

    // --- Specific Checkpoint References (Assign in Inspector) ---
    // These help in direct calls or ensuring they are part of 'allCheckpoints' for certain logic
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
    public Checkpoint checkpoint6_1_Ref;
    public Checkpoint checkpoint6_2_Ref;
    public Checkpoint checkpoint7_1_Ref;
    public Checkpoint checkpoint7_2_Ref;
    public Checkpoint checkpoint8_1_Ref;
    public Checkpoint checkpoint8_2_Ref;


    private Vector3 savedCarPosition;
    private Quaternion savedCarRotation;
    private Checkpoint _currentMainSavedCheckpoint;
    private List<Checkpoint> _currentConcurrentSavedCheckpoints = new List<Checkpoint>();

    // --- State variables for complex checkpoints (managed here) ---
    [Header("Complex Checkpoint States")]
    // Checkpoint 3
    [HideInInspector] public bool checkpoint3_1_hitByCorrectWheel = false;
    [HideInInspector] public bool checkpoint3_2_hitByCorrectWheel = false;

    // Checkpoint 5
    public TrafficLight trafficLightForCP5; // Assign in Inspector

    // Checkpoint 6
    public enum Checkpoint6InternalState { Idle, WaitingForStop, YellowLight_Need_6_2_Front, YellowLight_Need_6_1_Back, YellowLight_Need_6_2_Back }
    [HideInInspector] public Checkpoint6InternalState cp6_CurrentInternalState = Checkpoint6InternalState.Idle;
    public Renderer checkpoint6LightIndicatorRenderer; // Assign a renderer whose material will change
    public Material cp6_DefaultMaterial;
    public Material cp6_YellowMaterial;


    // Checkpoint 8
    public LevelCrossingLight levelCrossingLightForCP8; // Assign in Inspector


    void Start()
    {
        if (car == null) car = FindObjectOfType<CarController>();
        if (car == null) Debug.LogError("CarController not found in scene for RootCheckpointManager.");

        // Ensure all specific references are also in the 'allCheckpoints' list if not already
        // (You might automate this or ensure manual population in Inspector is complete)
        PopulateAllCheckpointsIfNeeded();


        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint(); // Start with all off
        }

        if (initialCheckpoint != null)
        {
            DefineSegmentStart(initialCheckpoint);
        }
        else
        {
            Debug.LogError("Initial checkpoint not set in RootCheckpointManager!");
        }
    }
    
    void PopulateAllCheckpointsIfNeeded()
    {
        // A helper to ensure specifically referenced checkpoints are in the main list for global operations.
        // This can be expanded or made more robust.
        var specificRefs = new List<Checkpoint> {
            checkpoint1_Ref, checkpoint2_1_Ref, checkpoint2_2_Ref, checkpoint3_1_Ref, checkpoint3_2_Ref,
            checkpoint4_1_Ref, checkpoint4_2_Ref, checkpoint5_1_Ref, checkpoint5_2_Ref, checkpoint6_1_Ref,
            checkpoint6_2_Ref, checkpoint7_1_Ref, checkpoint7_2_Ref, checkpoint8_1_Ref, checkpoint8_2_Ref
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
        if (car == null)
        {
            Debug.LogError("Car reference not set in RootCheckpointManager!");
            return;
        }
        if (mainCheckpoint == null)
        {
            Debug.LogError("MainCheckpoint for DefineSegmentStart is null. Cannot define segment.");
            return;
        }

        savedCarPosition = car.transform.position;
        savedCarRotation = car.transform.rotation;

        _currentMainSavedCheckpoint = mainCheckpoint;
        _currentConcurrentSavedCheckpoints.Clear();
        if (concurrentCheckpoints != null)
        {
            _currentConcurrentSavedCheckpoints.AddRange(concurrentCheckpoints.Where(cp => cp != null));
        }

        foreach (var cp in allCheckpoints)
        {
            if (cp != null) cp.DeactivateCheckpoint();
        }

        mainCheckpoint.ActivateCheckpoint();
        foreach (var cp in _currentConcurrentSavedCheckpoints)
        {
            if (cp != null) cp.ActivateCheckpoint();
        }

        ResetCheckpointInternalStatesForNewSegment();
        Debug.Log($"Segment defined. Main CP: {mainCheckpoint?.name}. Car Pos Saved: {savedCarPosition}. Concurrent CPs: {_currentConcurrentSavedCheckpoints.Count}");
    }

    public void AdvanceToSegment(Checkpoint nextMainCheckpoint, params Checkpoint[] nextConcurrentCheckpoints)
    {
        Debug.Log($"Advancing to segment. Next Main: {nextMainCheckpoint?.name}");
        DefineSegmentStart(nextMainCheckpoint, nextConcurrentCheckpoints);
    }

    public void HandleCrash()
    {
        Debug.Log("CRASH DETECTED! Resetting car and checkpoint states.");
        if (car == null)
        {
            Debug.LogError("Car reference is null in HandleCrash.");
            return;
        }

        car.ResetState(savedCarPosition, savedCarRotation);
        ResetCheckpointInternalStatesForFailedAttempt(); // Reset flags like CP3 hits, CP6 state
        ReactivateSavedSegment();
    }

    void ResetCheckpointInternalStatesForNewSegment()
    {
        checkpoint3_1_hitByCorrectWheel = false;
        checkpoint3_2_hitByCorrectWheel = false;

        cp6_CurrentInternalState = Checkpoint6InternalState.Idle;
        SetCheckpoint6LightMaterial(false); // Set to default material

        Debug.Log("Internal states (CP3, CP6) reset for new segment attempt.");
    }

    void ResetCheckpointInternalStatesForFailedAttempt()
    {
        // Often same as for new segment, ensures clean state for the re-attempt.
        checkpoint3_1_hitByCorrectWheel = false;
        checkpoint3_2_hitByCorrectWheel = false;

        cp6_CurrentInternalState = Checkpoint6InternalState.Idle;
        SetCheckpoint6LightMaterial(false);
        Debug.Log("Internal states (CP3, CP6) reset after a failed attempt.");
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
        else if (initialCheckpoint != null)
        {
            Debug.LogWarning("CurrentMainSavedCheckpoint was null during ReactivateSavedSegment. Resetting to InitialCheckpoint.");
            DefineSegmentStart(initialCheckpoint);
        }
        else
        {
            Debug.LogError("CRITICAL: No saved segment or initial checkpoint to reset to during ReactivateSavedSegment!");
        }
    }

    public Checkpoint GetCheckpointByName(string name)
    {
        return allCheckpoints.FirstOrDefault(cp => cp != null && cp.name == name);
    }

    // --- Checkpoint 3 Specific Logic ---
    public void ReportCheckpoint3PartHit(bool isPart1, bool byCorrectWheel)
    {
        if (isPart1) checkpoint3_1_hitByCorrectWheel = byCorrectWheel;
        else checkpoint3_2_hitByCorrectWheel = byCorrectWheel;

        if (checkpoint3_1_hitByCorrectWheel && checkpoint3_2_hitByCorrectWheel)
        {
            Debug.Log("Checkpoint 3 (both parts 3-1 & 3-2) successfully completed!");
            if(checkpoint3_1_Ref) checkpoint3_1_Ref.DeactivateCheckpoint(); // Explicitly deactivate parts
            if(checkpoint3_2_Ref) checkpoint3_2_Ref.DeactivateCheckpoint();

            if (checkpoint4_1_Ref != null)
            {
                AdvanceToSegment(checkpoint4_1_Ref);
            }
            else
            {
                Debug.LogError("Checkpoint4-1 (checkpoint4_1_Ref) not assigned in RootManager for CP3 completion!");
            }
        }
    }

    // --- Checkpoint 5 Specific Logic ---
    public TrafficLight.LightState GetTrafficLight5State()
    {
        if (trafficLightForCP5 != null) return trafficLightForCP5.currentLightState;
        Debug.LogWarning("TrafficLight for CP5 not assigned in RootManager. Defaulting to Green for safety.");
        return TrafficLight.LightState.Green;
    }

    // --- Checkpoint 6 Specific Logic ---
    public void SetCheckpoint6State(Checkpoint6InternalState newState)
    {
        cp6_CurrentInternalState = newState;
        Debug.Log($"Checkpoint 6 Internal State updated to: {newState}");
    }
    public void SetCheckpoint6LightMaterial(bool makeYellow)
    {
        if (checkpoint6LightIndicatorRenderer != null)
        {
            checkpoint6LightIndicatorRenderer.material = makeYellow ? cp6_YellowMaterial : cp6_DefaultMaterial;
            Debug.Log($"Checkpoint 6 Light Material set to: {(makeYellow ? "Yellow" : "Default")}");
        }
        else Debug.LogWarning("CP6 Light Indicator Renderer not assigned in RootManager.");
    }
    public void CompleteCheckpoint6Sequence()
    {
        Debug.Log("Checkpoint 6 sequence fully completed!");
        SetCheckpoint6State(Checkpoint6InternalState.Idle); // Reset state
        SetCheckpoint6LightMaterial(false); // Reset light

        if(checkpoint6_1_Ref) checkpoint6_1_Ref.DeactivateCheckpoint();
        if(checkpoint6_2_Ref) checkpoint6_2_Ref.DeactivateCheckpoint();

        if (checkpoint7_1_Ref != null)
        {
            AdvanceToSegment(checkpoint7_1_Ref);
        }
        else
        {
            Debug.LogError("Checkpoint7-1 (checkpoint7_1_Ref) not assigned in RootManager for CP6 completion!");
        }
    }


    // --- Checkpoint 8 Specific Logic ---
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
}