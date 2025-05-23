using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class CarAgent : Agent
{
    [Header("References")]
    public CarController carController;
    public RootCheckpointManager rootCheckpointManager;

    [Header("Training Rewards & Penalties")]
    public float crashPenalty = -1.0f;
    public float stageClearedReward = 0.5f;
    // No extra reward for all stages (lap), as per continuous training.
    private float _timePenaltyPerStep = -0.001f; // Small penalty per step

    private Checkpoint _lastClearedCheckpointForReward = null;


    public override void Initialize()
    {
        if (carController == null) carController = GetComponent<CarController>();
        if (rootCheckpointManager == null) rootCheckpointManager = FindObjectOfType<RootCheckpointManager>();

        if (carController == null) Debug.LogError("CarAgent: CarController not found!", this);
        if (rootCheckpointManager == null) Debug.LogError("CarAgent: RootCheckpointManager not found!", this);
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("Agent: New Episode Beginning.");
        if (rootCheckpointManager != null)
        {
            rootCheckpointManager.ForceResetToGlobalStart(this); // Resets car and checkpoint logic
        }
        _lastClearedCheckpointForReward = null; // Reset for new episode
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (carController == null || carController.rb == null)
        {
            Debug.LogWarning("Agent: CarController or its Rigidbody not available for observations.");
            // Add dummy observations matching your space size if this happens
            sensor.AddObservation(0f); // Speed
            sensor.AddObservation(Quaternion.identity); // Rotation (4 floats)
            sensor.AddObservation(0f); // Steering Angle
            return;
        }

        // 1. Car speed (1 float)
        sensor.AddObservation(carController.rb.linearVelocity.magnitude);

        // 2. Car rotation (Quaternion, 4 floats)
        sensor.AddObservation(transform.rotation); // World rotation is usually better for CNNs than local if camera is also world-aligned.
                                                 // Or transform.localRotation if your setup normalizes around a parent.

        // 3. Current steering angle of front wheels (1 float)
        sensor.AddObservation(carController.GetCurrentSteerAngle());

        // Visual observations (cameras) are added automatically by CameraSensorComponents.
        // Ensure they are configured on this Agent GameObject or children.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (carController == null) return;

        // Continuous Actions:
        // Action 0: Accelerator (0 to 1)
        // Action 1: Brake (0 to 1)
        // Action 2: Steering Input (-1 to 1)
        float acceleratorInput = Mathf.Clamp01(actions.ContinuousActions[0]);
        float brakeInput = Mathf.Clamp01(actions.ContinuousActions[1]);
        float steerInput = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        carController.SetAgentInputs(acceleratorInput, brakeInput, steerInput);

        // Minor penalty every frame/step
        AddReward(_timePenaltyPerStep);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear(); // Clear previous actions

        // Map keyboard to the 3 continuous actions
        float verticalInput = Input.GetAxis("Vertical"); // W/S or Up/Down
        float horizontalInput = Input.GetAxis("Horizontal"); // A/D or Left/Right
        bool spacePressed = Input.GetKey(KeyCode.Space);

        // Action 0: Accelerator
        continuousActions[0] = (verticalInput > 0) ? verticalInput : 0f;
        // Action 1: Brake
        continuousActions[1] = spacePressed ? 1.0f : ((verticalInput < 0) ? -verticalInput : 0f); // Brake also if reversing
        // Action 2: Steering
        continuousActions[2] = horizontalInput;
    }

    // Called by RootCheckpointManager
    public void AgentCrashed()
    {
        AddReward(crashPenalty);
        Debug.Log($"Agent: CRASHED! Penalty: {crashPenalty}. Ending Episode.");
        EndEpisode();
    }

    // Called by RootCheckpointManager when a stage is properly cleared
    public void AgentClearedStage(Checkpoint clearedCheckpoint)
    {
        if (clearedCheckpoint == null) return;

        // Prevent multiple rewards for the same cleared checkpoint if called rapidly
        if (_lastClearedCheckpointForReward == clearedCheckpoint) return;
        _lastClearedCheckpointForReward = clearedCheckpoint;

        AddReward(stageClearedReward);
        Debug.Log($"Agent: Stage Cleared ({clearedCheckpoint.name}). Reward: {stageClearedReward}");

        // As per "do not add extra reward when it clear all stages" for continuous training,
        // we don't have a special "lap completion" reward that's different *unless*
        // you want to end the episode on lap completion. If so, CP9 would be the trigger.
        if (clearedCheckpoint == rootCheckpointManager.checkpoint9_Ref) {
             Debug.Log($"Agent: Final stage (CP9) cleared. Episode will continue or reset via OnEpisodeBegin if max steps hit.");
             // Optionally, could end episode here if you want distinct laps as episodes:
             // EndEpisode();
        }
    }
}