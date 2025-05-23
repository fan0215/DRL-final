using UnityEngine;
using System.Collections.Generic; // Required for List

public class CarController : MonoBehaviour
{
    [Header("References")]
    public RootCheckpointManager rootCheckpointManager; // Assign in Inspector
    public Rigidbody rb; 

    [Header("Wheel Colliders (Assign from Hierarchy)")]
    public WheelCollider wheelBackLeft;
    public WheelCollider wheelBackRight;
    public WheelCollider wheelFrontLeft;
    public WheelCollider wheelFrontRight;

    [Header("Wheel Transforms (For Visuals - Optional)")]
    public Transform wheelFL_Transform;
    public Transform wheelFR_Transform;
    public Transform wheelBL_Transform;
    public Transform wheelBR_Transform;
    // IMPORTANT: Set this in Inspector to Quaternion.Euler(0, 0, 90) for your cylinder wheels
    public Quaternion wheelVisualsRotationOffset = Quaternion.Euler(0, 0, 90);

    [Header("Car Driving Parameters (Logic from your CarPhysics script)")]
    [Tooltip("How strong acceleration is. Corresponds to 'torquePower'.")]
    public float motorForce = 3000f;
    [Tooltip("Steering angle for front wheels.")]
    public float maxSteeringAngle = 50f;
    [Tooltip("Force applied to rear wheels when braking (Spacebar). Corresponds to 'brakeForce'.")]
    public float activeBrakeForce = 5000f;
    // Note: idleBrakeForce is intentionally omitted from driving logic to match your CarPhysics.cs

    [Header("Car State Properties (for Checkpoint System)")]
    [Tooltip("Velocity magnitude below which the car is considered stopped.")]
    public float stopVelocityThreshold = 0.1f;
    // CenterOfMassOffset and its application are removed as per your request.

    [Header("Checkpoint Spawn Points")]
    [Tooltip("Assign empty GameObjects representing spawn positions and rotations for each checkpoint/stage.")]
    public List<Transform> checkpointSpawnPoints = new List<Transform>(); // Assign these in Inspector

    // Inputs set by Agent
    private float currentAcceleratorInput = 0f; // 0 to 1
    private float currentBrakeInput = 0f;       // 0 to 1
    private float currentSteerInput = 0f;       // -1 to 1

    // // Private variables to store calculated torque and steer, mirroring CarPhysics.cs
    private float currentCalculatedTorque = 0f;
    private float currentCalculatedSteerAngle = 0f;
    // isActiveBraking (from input) will be used directly

    public float torquePower = 1500f; // How strong acceleration is
    public float brakeForce = 3000f; // Force applied when braking

    private float currentTorque = 0f;
    private float currentSteer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("CarController requires a Rigidbody component on the same GameObject.", this);
            enabled = false; // Disable script if no Rigidbody
            return;
        }

        if (rootCheckpointManager == null)
        {
            rootCheckpointManager = FindObjectOfType<RootCheckpointManager>();
            if (rootCheckpointManager == null)
            {
                Debug.LogWarning("CarController: RootCheckpointManager not found in the scene. Crash handling for 'Edge' collisions will not work.", this);
            }
        }

        if (wheelFrontLeft == null || wheelFrontRight == null || wheelBackLeft == null || wheelBackRight == null)
        {
            Debug.LogError("One or more WheelColliders are not assigned in the CarController Inspector! Please assign all four.", this);
            enabled = false;
        }

        ResetState(0);
    }

    void FixedUpdate()
    {
        // human driving
        // // Get input values
        // float move = Input.GetAxis("Vertical"); // Up/Down arrow or W/S
        // float steer = Input.GetAxis("Horizontal"); // Left/Right arrow or A/D
        // bool isBraking = Input.GetKey(KeyCode.Space); // Spacebar for brakes

        // // Apply torque to back wheels
        // currentTorque = move * torquePower;

        // // Apply steering to front wheels
        // currentSteer = steer * maxSteeringAngle;

        // // Apply forces to wheels
        // ApplyTorque();
        // ApplySteering();
        // ApplyBrakes(isBraking);

        ApplyAgentSteering();
        ApplyAgentDriveAndBrake();
    }

    // Called by CarAgent.cs
    public void SetAgentInputs(float accelerator, float brake, float steer)
    {
        currentAcceleratorInput = Mathf.Clamp01(accelerator);
        currentBrakeInput = Mathf.Clamp01(brake);
        currentSteerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    void ApplyAgentSteering()
    {
        float targetSteerAngle = currentSteerInput * maxSteeringAngle;
        if (wheelFrontLeft != null) wheelFrontLeft.steerAngle = targetSteerAngle;
        if (wheelFrontRight != null) wheelFrontRight.steerAngle = targetSteerAngle;
    }

    void ApplyAgentDriveAndBrake()
    {
        // Accelerator input (0 to 1)
        float motorTorque = currentAcceleratorInput * motorForce;

        // Brake input (0 to 1)
        // Apply brake to all wheels for effective stopping by agent.
        float brakeTorque = currentBrakeInput * activeBrakeForce;

        // If braking significantly, reduce motor torque (optional, but common)
        if (currentBrakeInput > 0.1f)
        {
            motorTorque *= (1f - currentBrakeInput * 0.8f); // Reduce motor if braking
        }

        // Apply motor torque (only forward, no reverse with current action space)
        if (wheelBackLeft != null) wheelBackLeft.motorTorque = motorTorque;
        if (wheelBackRight != null) wheelBackRight.motorTorque = motorTorque;

        // Apply brake torque to all wheels
        if (wheelFrontLeft != null) wheelFrontLeft.brakeTorque = brakeTorque;
        if (wheelFrontRight != null) wheelFrontRight.brakeTorque = brakeTorque;
        if (wheelBackLeft != null) wheelBackLeft.brakeTorque = brakeTorque;
        if (wheelBackRight != null) wheelBackRight.brakeTorque = brakeTorque;
    }

    public float GetCurrentSteerAngle() // For agent observation
    {
        if (wheelFrontLeft != null)
        {
            return wheelFrontLeft.steerAngle; // Actual current steer angle
        }
        return 0f;
    }

    void ApplyTorque()
    {
        wheelBackLeft.motorTorque = currentTorque;
        wheelBackRight.motorTorque = currentTorque;
    }

    void ApplySteering()
    {
        wheelFrontLeft.steerAngle = currentSteer;
        wheelFrontRight.steerAngle = currentSteer;
    }

    void ApplyBrakes(bool isBraking)
    {
        float brake = isBraking ? activeBrakeForce : 0f;
        wheelBackLeft.brakeTorque = brake;
        wheelBackRight.brakeTorque = brake;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Edge"))
        {
            Debug.Log("Car collided with an Edge.");
            if (rootCheckpointManager != null) rootCheckpointManager.HandleCrash();
            else Debug.LogError("RootCheckpointManager not found by CarController for crash handling.");
        }
    }

    public bool IsFullyStopped()
    {
        if (rb == null) return true;
        bool wheelsPhysicallyStopped = true;
        WheelCollider[] currentActiveWheels = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        foreach (WheelCollider wc_check in currentActiveWheels) // Renamed wc to wc_check to avoid conflict with UpdateSingleWheel parameter if copy-pasted into same scope by mistake
        {
            if (wc_check != null) wheelsPhysicallyStopped &= (Mathf.Abs(wc_check.rpm) < 5);
            else { wheelsPhysicallyStopped = false; break; }
        }
        return rb.linearVelocity.magnitude < stopVelocityThreshold &&
               rb.angularVelocity.magnitude < stopVelocityThreshold &&
               wheelsPhysicallyStopped;
    }

    public void ResetState(int spawnPointIndex)
    {
        if (checkpointSpawnPoints == null || checkpointSpawnPoints.Count == 0)
        {
            Debug.LogError("CarController: 'Checkpoint Spawn Points' list is not set up or is empty! Cannot reset state.", this);
            return;
        }
        if (spawnPointIndex < 0 || spawnPointIndex >= checkpointSpawnPoints.Count || checkpointSpawnPoints[spawnPointIndex] == null)
        {
            Debug.LogError($"CarController: Invalid spawnPointIndex '{spawnPointIndex}' or the spawn point Transform at that index is null. Cannot reset state. Checking for fallback to index 0.", this);
            if (checkpointSpawnPoints.Count > 0 && checkpointSpawnPoints[0] != null)
            {
                spawnPointIndex = 0; // Fallback to the first defined spawn point if valid
                Debug.LogWarning($"CarController: Using fallback spawn point index 0.", this);
            }
            else
            {
                Debug.LogError("CarController: No valid spawn points available, including fallback index 0. Reset cannot proceed.", this);
                return; // Cannot proceed if no valid spawn points at all
            }
        }

        Transform selectedSpawnPoint = checkpointSpawnPoints[spawnPointIndex];
        transform.position = selectedSpawnPoint.position;
        transform.rotation = selectedSpawnPoint.rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset internal calculation states used by driving logic
        currentCalculatedTorque = 0f;
        currentCalculatedSteerAngle = 0f;

        // Reset WheelCollider states
        // WheelCollider[] allWheels = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        // foreach (WheelCollider wc_reset in allWheels)
        // {
        //     if (wc_reset != null)
        //     {
        //         wc_reset.motorTorque = 0;
        //         // Apply activeBrakeForce to all wheels on reset to ensure it stops quickly.
        //         // This is a common practice for reset, even if driving logic brakes rear only.
        //         wc_reset.brakeTorque = activeBrakeForce;
        //         wc_reset.steerAngle = 0;
        //     }
        // }
        Debug.Log($"Car has been reset to spawn point index: {spawnPointIndex} (Name: {selectedSpawnPoint.name})");
    }
}