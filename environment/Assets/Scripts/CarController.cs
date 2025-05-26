using UnityEngine;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    [Header("References")]
    public RootCheckpointManager rootCheckpointManager;
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
    public Quaternion wheelVisualsRotationOffset = Quaternion.Euler(0, 0, 90);

    [Header("Car Driving Parameters (Logic from your CarPhysics script)")]
    [Tooltip("How strong acceleration is. Corresponds to 'torquePower'.")]
    public float motorForce = 3000f;
    [Tooltip("Steering angle for front wheels.")]
    public float maxSteeringAngle = 60f;
    [Tooltip("Force applied to rear wheels when braking (Spacebar). Corresponds to 'brakeForce'.")]
    public float activeBrakeForce = 5000f;
    [Tooltip("This variable is present for consistency but NOT USED by the current driving logic (set to 0).")]
    public float idleBrakeForce = 0f;

    [Header("Car State Properties (for Checkpoint System)")]
    [Tooltip("Velocity magnitude below which the car is considered stopped.")]
    public float stopVelocityThreshold = 0.1f;

    [Header("Checkpoint Spawn Points")]
    [Tooltip("Assign empty GameObjects representing spawn positions and rotations for each checkpoint/stage.")]
    public List<Transform> checkpointSpawnPoints = new List<Transform>();

    private float currentCalculatedTorque = 0f;
    private float currentCalculatedSteerAngle = 0f;
    private bool previousFrameBrakingStatusForLog = false; // For logging brake changes

    private float currentAcceleratorInput = 0f; // -1 to 1
    private float currentBrakeInput = 0f;       //  0 to 1
    private float currentSteerInput = 0f;       // -1 to 1

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("CarController requires a Rigidbody component.", this);
            enabled = false;
            return;
        }
        // Center of Mass adjustment REMOVED as per your request.

        if (rootCheckpointManager == null)
        {
            rootCheckpointManager = FindObjectOfType<RootCheckpointManager>();
            if (rootCheckpointManager == null)
                Debug.LogWarning("CarController: RootCheckpointManager not found. Crash handling will not work.", this);
        }

        if (wheelFrontLeft == null || wheelFrontRight == null || wheelBackLeft == null || wheelBackRight == null)
        {
            Debug.LogError("One or more WheelColliders are not assigned in the CarController Inspector!", this);
            enabled = false;
        }

        ResetState(0);
    }

    void FixedUpdate()
    {
        // pass
    }

    // Called by CarAgent.cs
    public void SetAgentInputs(float accelerator, float brake, float steer)
    {
        currentAcceleratorInput = Mathf.Clamp(accelerator, -1f, 1f);
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
        float motorTorque = currentAcceleratorInput * motorForce;
        float brakeTorque = currentBrakeInput * activeBrakeForce;

        // If braking significantly, reduce motor torque
        if (currentBrakeInput > 0.1f)
        {
            motorTorque *= (1.0f - currentBrakeInput);
        }

        if (wheelBackLeft != null) wheelBackLeft.motorTorque = motorTorque;
        if (wheelBackRight != null) wheelBackRight.motorTorque = motorTorque;

        if (wheelFrontLeft != null) wheelFrontLeft.brakeTorque = brakeTorque;
        if (wheelFrontRight != null) wheelFrontRight.brakeTorque = brakeTorque;
        if (wheelBackLeft != null) wheelBackLeft.brakeTorque = brakeTorque;
        if (wheelBackRight != null) wheelBackRight.brakeTorque = brakeTorque;
    }

    public float GetCurrentSteerAngle()
    {
        if (wheelFrontLeft != null)
        {
            return wheelFrontLeft.steerAngle;
        }
        return 0f;
    }

    // void Update()
    // {
    //     float moveInput = Input.GetAxis("Vertical");
    //     float steerInput = Input.GetAxis("Horizontal");
    //     bool isSpacebarBraking = Input.GetKey(KeyCode.Space);

    //     currentCalculatedTorque = moveInput * motorForce;
    //     currentCalculatedSteerAngle = steerInput * maxSteeringAngle;

    //     ApplyMotorToWheels();
    //     ApplySteeringToWheels();
    //     ApplyBrakesToWheels(isSpacebarBraking);
    // }

    void OnCollisionEnter(Collision collision)
    {
        if (rootCheckpointManager == null) return;
        if (collision.gameObject.CompareTag("Edge"))
        {
            Debug.Log("Car collided with an Edge. Notifying RootCheckpointManager.");
            rootCheckpointManager.HandleCrash();
        }
    }

    public bool IsFullyStopped()
    {
        if (rb == null) return true;
        bool wheelsPhysicallyStopped = true;
        WheelCollider[] currentActiveWheels = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        foreach (WheelCollider wc_check in currentActiveWheels)
        {
            if (wc_check != null) wheelsPhysicallyStopped &= (Mathf.Abs(wc_check.rpm) < 10);
            else { wheelsPhysicallyStopped = false; break; }
        }
        return rb.linearVelocity.magnitude < stopVelocityThreshold && rb.angularVelocity.magnitude < stopVelocityThreshold && wheelsPhysicallyStopped;
    }

    private float _lastResetTime = -10f; // For debug logging after reset

    public void ResetState(int spawnPointIndex)
    {
        _lastResetTime = Time.time; // Track reset time for debugging
        Debug.Log($"--- CarController.ResetState called with spawnPointIndex: {spawnPointIndex} at Time: {Time.time} ---");

        if (checkpointSpawnPoints == null || checkpointSpawnPoints.Count == 0)
        {
            Debug.LogError("CarController: 'Checkpoint Spawn Points' list is not set up or is empty!", this);
            return;
        }
        if (spawnPointIndex < 0 || spawnPointIndex >= checkpointSpawnPoints.Count || checkpointSpawnPoints[spawnPointIndex] == null)
        {
            Debug.LogError($"CarController: Invalid spawnPointIndex '{spawnPointIndex}' or spawn point Transform is null. Attempting fallback to 0.", this);
            if (checkpointSpawnPoints.Count > 0 && checkpointSpawnPoints[0] != null) spawnPointIndex = 0;
            else { Debug.LogError("CarController: No valid spawn points available for fallback.", this); return; }
        }

        Transform selectedSpawnPoint = checkpointSpawnPoints[spawnPointIndex];
        transform.position = selectedSpawnPoint.position;
        transform.rotation = selectedSpawnPoint.rotation;
        Debug.Log($"Car position set to: {transform.position}, rotation set to: {transform.rotation.eulerAngles}");


        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Debug.Log("Rigidbody velocities zeroed.");
        }

        currentCalculatedTorque = 0f;
        currentCalculatedSteerAngle = 0f;
        Debug.Log("Internal calculated torque/steer reset to 0.");

        Debug.Log($"Car has been reset. Next Update frame will read fresh inputs.");
    }
}