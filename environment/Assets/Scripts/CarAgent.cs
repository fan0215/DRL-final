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
    public float stageClearedReward = 10f;
    // No extra reward for completing all stages in continuous training.
    [Tooltip("Small negative reward per step to encourage faster completion.")]
    public float timePenaltyPerStep = -0.0005f; // Tunable
    [Tooltip("Episodic trainin or non Episodic")]
    public bool episodic_train = true;

    [Header("Continuous Reward Signal")]
    public float continuousRewardScale = 0.01f;
    private Vector3 lastStepPosition;

    public override void Initialize()
    {
        lastStepPosition = transform.position;

        if (carController == null) carController = GetComponent<CarController>();
        if (rootCheckpointManager == null) rootCheckpointManager = FindObjectOfType<RootCheckpointManager>();

        if (carController == null) Debug.LogError("CarAgent: CarController component not found on this GameObject or not assigned!", this);
        if (rootCheckpointManager == null) Debug.LogError("CarAgent: RootCheckpointManager instance not found in the scene or not assigned!", this);
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"Agent: New Episode Beginning (Step: {StepCount}).");

        if (rootCheckpointManager != null)
        {
            rootCheckpointManager.ForceResetToGlobalStart(this); // Resets car to global start and checkpoint logic
        }
        else if (carController != null) // Fallback basic reset if manager is missing
        {
            // Attempt a very basic reset if RootCheckpointManager isn't available
            Checkpoint firstCp = FindObjectOfType<RootCheckpointManager>()?.initialCheckpoint ?? FindObjectOfType<RootCheckpointManager>()?.checkpoint1_1_Ref;
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
    }

    private float HeuristicRewardUpdate()
    {
        Vector3 currentPosition = transform.position;
        float l2dist_past = Mathf.Pow(lastStepPosition.x - rootCheckpointManager.currentObjposition.x, 2) + Mathf.Pow(lastStepPosition.z - rootCheckpointManager.currentObjposition.z, 2);
        float l2dist_now = Mathf.Pow(currentPosition.x - rootCheckpointManager.currentObjposition.x, 2) + Mathf.Pow(currentPosition.z - rootCheckpointManager.currentObjposition.z, 2);
        lastStepPosition = currentPosition;
        float reward = (l2dist_past - l2dist_now) * continuousRewardScale;
        // Debug.Log($"Last: {lastStepPosition}, Current: {currentPosition}, Obj: {rootCheckpointManager.currentObjposition}, L2 Past: {l2dist_past}, L2 Now: {l2dist_now}, Reward: {reward}");
        return reward;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (carController == null) return;

        // Continuous Actions from Python Agent:
        // actions.ContinuousActions[0]: Accelerator (-1 to 1)
        // actions.ContinuousActions[1]: Brake (0 to 1)
        // actions.ContinuousActions[2]: Steering Input (-1 to 1)

        float acceleratorInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float brakeInput = Mathf.Clamp01(actions.ContinuousActions[1]);
        float steerInput = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        carController.SetAgentInputs(acceleratorInput, brakeInput, steerInput);

        // Minor penalty every step to encourage progress and efficiency
        AddReward(HeuristicRewardUpdate());
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
        // Action 0: Accelerator (-1 to 1)
        continuousActions[0] = verticalInput;

        // Action 1: Brake (0 to 1)
        // Brake if space is pressed OR if "Vertical" input is negative (for reversing behavior with brake)
        if (isSpacebarPressed)
        {
            continuousActions[1] = 1.0f;
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
        if (episodic_train) // reset to global start upon crash
        {
            EndEpisode(); // End the episode on a crash
        }
        // else do not reset to global start, only to local checkpoint upon crash
    }

    // Method called by RootCheckpointManager when a checkpoint stage is cleared
    public void AgentClearedStage()
    {
        AddReward(stageClearedReward);
        Debug.Log($"Agent: Stage Cleared. Reward: {stageClearedReward}");
    }
}