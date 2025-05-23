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
    [Tooltip("Penalty applied when the car crashes.")]
    public float crashPenalty = -1.0f;
    [Tooltip("Reward given when a checkpoint stage is successfully cleared.")]
    public float stageClearedReward = 5f;
    // No extra reward for completing all stages in continuous training.
    [Tooltip("Small negative reward per step to encourage faster completion.")]
    private float _timePenaltyPerStep = -0.0005f; // Tunable

    // Internal state for reward logic and observations
    private Checkpoint _lastClearedCheckpointForReward = null;
    private Checkpoint _currentTargetCheckpoint = null;
    private float _distanceToTargetAtLastStep = float.MaxValue;

    public override void Initialize()
    {
        if (carController == null) carController = GetComponent<CarController>();
        if (rootCheckpointManager == null) rootCheckpointManager = FindObjectOfType<RootCheckpointManager>();

        if (carController == null) Debug.LogError("CarAgent: CarController component not found on this GameObject or not assigned!", this);
        if (rootCheckpointManager == null) Debug.LogError("CarAgent: RootCheckpointManager instance not found in the scene or not assigned!", this);
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"Agent: New Episode Beginning (Step: {StepCount}).");
        _lastClearedCheckpointForReward = null;
        _distanceToTargetAtLastStep = float.MaxValue;

        if (rootCheckpointManager != null)
        {
            rootCheckpointManager.ForceResetToGlobalStart(this); // Resets car to global start and checkpoint logic
        }
        else if (carController != null) // Fallback basic reset if manager is missing
        {
            // Attempt a very basic reset if RootCheckpointManager isn't available
            Checkpoint firstCp = FindObjectOfType<RootCheckpointManager>()?.initialCheckpoint ?? FindObjectOfType<RootCheckpointManager>()?.checkpoint1_Ref;
            if (firstCp != null)
            {
                carController.ResetState(firstCp.spawnPointIndex);
            } else {
                Debug.LogWarning("CarAgent: Could not perform full reset in OnEpisodeBegin as RootCheckpointManager or its initial checkpoint is missing.");
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (carController == null || carController.rb == null || rootCheckpointManager == null)
        {
            Debug.LogWarning("Agent: Missing critical references (CarController, Rigidbody, or RootCheckpointManager) for observation. Sending default/zero observations.");
            // Add a fixed number of zero observations to match your defined vector observation space size.
            // Vector Obs Size: Speed (1) + Car Rotation (Quaternion 4) + Current Steer Angle (1) = 6 floats
            for (int i = 0; i < 6; i++) sensor.AddObservation(0f);
            return;
        }

        // 1. Car speed (1 float) - Normalized (assuming max practical speed around 30-50 m/s, adjust as needed)
        sensor.AddObservation(carController.rb.linearVelocity.magnitude / 30.0f);

        // 2. Car's world rotation (Quaternion, 4 floats)
        sensor.AddObservation(transform.rotation);

        // 3. Current steering angle of front wheels (normalized, 1 float)
        // Assuming maxSteerAngle is the denormalization factor used in CarController
        sensor.AddObservation(carController.GetCurrentSteerAngle() / carController.maxSteeringAngle);

        // Visual observations (from front, left, right, left mirror, right mirror cameras)
        // are added automatically by the CameraSensorComponents attached to this Agent's GameObject.
        // Ensure they are correctly set up in the Inspector.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (carController == null) return;

        // Continuous Actions from Python Agent:
        // actions.ContinuousActions[0]: Accelerator (0 to 1)
        // actions.ContinuousActions[1]: Brake (0 to 1)
        // actions.ContinuousActions[2]: Steering Input (-1 to 1)

        float acceleratorInput = Mathf.Clamp01(actions.ContinuousActions[0]);
        float brakeInput = Mathf.Clamp01(actions.ContinuousActions[1]);
        float steerInput = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        carController.SetAgentInputs(acceleratorInput, brakeInput, steerInput);

        // Minor penalty every step to encourage progress and efficiency
        AddReward(_timePenaltyPerStep);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Allows you to control the car manually using keyboard inputs for testing.
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear(); // Important to clear before setting

        float verticalInput = Input.GetAxis("Vertical");     // W/S or Up/Down
        float horizontalInput = Input.GetAxis("Horizontal"); // A/D or Left/Right
        bool isSpacebarPressed = Input.GetKey(KeyCode.Space);

        // Map to agent's action space:
        // Action 0: Accelerator (0 to 1)
        continuousActions[0] = (verticalInput > 0.01f) ? verticalInput : 0f;

        // Action 1: Brake (0 to 1)
        // Brake if space is pressed OR if "Vertical" input is negative (for reversing behavior with brake)
        if (isSpacebarPressed)
        {
            continuousActions[1] = 1.0f;
        }
        else if (verticalInput < -0.01f)
        {
            continuousActions[1] = Mathf.Abs(verticalInput); // Use S/Down as brake if not accelerating
            continuousActions[0] = 0f; // Don't accelerate if braking with S/Down
        }
        else
        {
            continuousActions[1] = 0f;
        }

        // Action 2: Steering Input (-1 to 1)
        continuousActions[2] = horizontalInput;
    }

    // Method called by RootCheckpointManager when a crash occurs
    public void AgentCrashed()
    {
        AddReward(crashPenalty);
        Debug.Log($"Agent: CRASHED! Penalty: {crashPenalty}. Ending Episode.");
        EndEpisode(); // End the episode on a crash
    }

    // Method called by RootCheckpointManager when a checkpoint stage is cleared
    public void AgentClearedStage(Checkpoint clearedCheckpoint)
    {
        if (clearedCheckpoint == null)
        {
            Debug.LogWarning("AgentClearedStage called with null checkpoint.");
            return;
        }

        // Prevent giving rewards multiple times for the same checkpoint if this method is called
        // again before the agent has meaningfully progressed to a new state.
        if (_lastClearedCheckpointForReward == clearedCheckpoint)
        {
            // Debug.Log($"Agent: Stage {clearedCheckpoint.name} already processed for reward. Skipping.");
            return;
        }
        _lastClearedCheckpointForReward = clearedCheckpoint;
        _distanceToTargetAtLastStep = float.MaxValue; // Reset distance for next target observation

        AddReward(stageClearedReward);
        Debug.Log($"Agent: Stage Cleared ({clearedCheckpoint.name}). Reward: {stageClearedReward}");

        // Per your request: "do not add extra reward when it clear all stages" for continuous training.
        // The episode will continue until a crash or max steps defined in Agent's parameters.
        // If you wanted each "lap" (clearing CP9) to be a distinct successful episode, you could call EndEpisode() here.
        // Example: if (clearedCheckpoint == rootCheckpointManager.checkpoint9_Ref) { EndEpisode(); }
    }
}