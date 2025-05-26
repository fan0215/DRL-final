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
    [Tooltip("Penalty for crashing.")]
    public float crashPenalty = -1.0f;
    [Tooltip("Reward for clearing a checkpoint.")]
    public float stageClearedReward = 5f;
    [Tooltip("Small negative reward per step to encourage faster completion.")]
    private float _timePenaltyPerStep = -0.0005f;

    private Checkpoint _lastClearedCheckpointForReward = null;

    public override void Initialize()
    {
        if (carController == null) carController = GetComponent<CarController>();
        if (rootCheckpointManager == null) rootCheckpointManager = FindObjectOfType<RootCheckpointManager>();

        if (carController == null) Debug.LogError("CarAgent: CarController not found or assigned!", this);
        if (rootCheckpointManager == null) Debug.LogError("CarAgent: RootCheckpointManager not found or assigned!", this);
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"Agent: New Episode Beginning (Step: {StepCount}).");
        _lastClearedCheckpointForReward = null;

        if (rootCheckpointManager != null)
        {
            rootCheckpointManager.ForceResetToGlobalStart(this);
        }
        else if (carController != null)
        {
            // Fallback: basic reset if RootCheckpointManager is missing
            Checkpoint firstCp = FindObjectOfType<RootCheckpointManager>()?.initialCheckpoint ?? FindObjectOfType<RootCheckpointManager>()?.checkpoint1_Ref;
            if (firstCp != null)
            {
                carController.ResetState(firstCp.spawnPointIndex);
            }
            else
            {
                Debug.LogWarning("CarAgent: Could not perform full reset. RootCheckpointManager or initial checkpoint is missing.");
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (carController == null || carController.rb == null || rootCheckpointManager == null)
        {
            Debug.LogWarning("Agent: Missing references for observation. Sending default/zero observations.");
            // Send zero observations if critical components are missing
            for (int i = 0; i < 6; i++) sensor.AddObservation(0f);
            return;
        }

        // Car speed (normalized)
        sensor.AddObservation(carController.rb.linearVelocity.magnitude / 30.0f);

        // Car's world rotation (Quaternion)
        sensor.AddObservation(transform.rotation);

        // Current steering angle (normalized)
        sensor.AddObservation(carController.GetCurrentSteerAngle() / carController.maxSteeringAngle);

        // Visual observations are added by CameraSensorComponents.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (carController == null) return;

        // Continuous Actions:
        // actions.ContinuousActions[0]: Accelerator (-1 to 1)
        // actions.ContinuousActions[1]: Brake (0 to 1)
        // actions.ContinuousActions[2]: Steering Input (-1 to 1)

        float acceleratorInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float brakeInput = Mathf.Clamp01(actions.ContinuousActions[1]);
        float steerInput = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        carController.SetAgentInputs(acceleratorInput, brakeInput, steerInput);

        // Apply a small penalty each step
        AddReward(_timePenaltyPerStep);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear();

        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");
        bool isSpacebarPressed = Input.GetKey(KeyCode.Space);

        // Map inputs to agent actions
        continuousActions[0] = verticalInput;

        if (isSpacebarPressed)
        {
            continuousActions[1] = 1.0f;
        }
        else
        {
            continuousActions[1] = 0f;
        }

        continuousActions[2] = horizontalInput;
    }

    // Called by RootCheckpointManager on crash
    public void AgentCrashed()
    {
        AddReward(crashPenalty);
        Debug.Log($"Agent: CRASHED! Penalty: {crashPenalty}. Ending Episode.");
        EndEpisode();
    }

    // Called by RootCheckpointManager when a checkpoint is cleared
    public void AgentClearedStage(Checkpoint clearedCheckpoint)
    {
        if (clearedCheckpoint == null)
        {
            Debug.LogWarning("AgentClearedStage called with null checkpoint.");
            return;
        }

        // Prevent multiple rewards for the same checkpoint
        if (_lastClearedCheckpointForReward == clearedCheckpoint)
        {
            return;
        }
        _lastClearedCheckpointForReward = clearedCheckpoint;

        AddReward(stageClearedReward);
        Debug.Log($"Agent: Stage Cleared ({clearedCheckpoint.name}). Reward: {stageClearedReward}");
    }
}