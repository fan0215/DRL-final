using UnityEngine;
using System.Collections.Generic; // Required for List

public class CarController : MonoBehaviour
{
    [Header("References")]
    public RootCheckpointManager rootCheckpointManager; // Assign in Inspector
    public Rigidbody rb;                                // Should be auto-assigned if on the same GameObject

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
    public float motorForce = 1500f;
    [Tooltip("Steering angle for front wheels.")]
    public float maxSteeringAngle = 30f;
    [Tooltip("Force applied to rear wheels when braking (Spacebar). Corresponds to 'brakeForce'.")]
    public float activeBrakeForce = 3000f;
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
    }

    void Update()
    {
        // --- Start Update Debug Log ---
        // if (Time.frameCount % 60 == 0) // Log once a second to reduce spam after initial burst
        //    Debug.Log($"CarController Update - Frame: {Time.frameCount}, Time: {Time.time:F2}");
        // --- End Update Debug Log ---

        float moveInput = Input.GetAxis("Vertical");
        float steerInput = Input.GetAxis("Horizontal");
        bool isSpacebarBraking = Input.GetKey(KeyCode.Space);

        currentCalculatedTorque = moveInput * motorForce;
        currentCalculatedSteerAngle = steerInput * maxSteeringAngle;

        ApplyMotorToWheels();
        ApplySteeringToWheels();
        ApplyBrakesToWheels(isSpacebarBraking);
    }

    // You might prefer physics in FixedUpdate, but sticking to Update as per your working script's structure
    // void FixedUpdate() { /* If you move physics here, move ApplyMotor, ApplySteering, ApplyBrakes, UpdateWheelVisuals */ }

    void ApplyMotorToWheels()
    {
        if (wheelBackLeft != null) wheelBackLeft.motorTorque = currentCalculatedTorque;
        if (wheelBackRight != null) wheelBackRight.motorTorque = currentCalculatedTorque;
    }

    void ApplySteeringToWheels()
    {
        if (wheelFrontLeft != null) wheelFrontLeft.steerAngle = currentCalculatedSteerAngle;
        if (wheelFrontRight != null) wheelFrontRight.steerAngle = currentCalculatedSteerAngle;
    }

    void ApplyBrakesToWheels(bool isBrakingNow)
    {
        float brakeValueToApply = isBrakingNow ? activeBrakeForce : 0f;
        if (wheelBackLeft != null) wheelBackLeft.brakeTorque = brakeValueToApply;
        if (wheelBackRight != null) wheelBackRight.brakeTorque = brakeValueToApply;

        if (wheelFrontLeft != null) wheelFrontLeft.brakeTorque = 0f;
        if (wheelFrontRight != null) wheelFrontRight.brakeTorque = 0f;
        
        // if (Time.time < 5.0f || (Time.time - _lastResetTime) < 5.0f) // Debug brake application
        //    Debug.Log($"ApplyBrakes - isBrakingNow: {isBrakingNow}, Applied Rear Brake: {brakeValueToApply}");
    }

    void UpdateWheelVisuals()
    {
        UpdateSingleWheel(wheelFrontLeft, wheelFL_Transform);
        UpdateSingleWheel(wheelFrontRight, wheelFR_Transform);
        UpdateSingleWheel(wheelBackLeft, wheelBL_Transform);
        UpdateSingleWheel(wheelBackRight, wheelBR_Transform);
    }

    void UpdateSingleWheel(WheelCollider wc, Transform visualWheelTransform)
    {
        if (visualWheelTransform == null || wc == null) return;
        Vector3 position;
        Quaternion rotation;
        wc.GetWorldPose(out position, out rotation);
        visualWheelTransform.position = position;
        visualWheelTransform.rotation = rotation * wheelVisualsRotationOffset;
    }

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
        return rb.linearVelocity.magnitude < stopVelocityThreshold &&
               rb.angularVelocity.magnitude < stopVelocityThreshold &&
               wheelsPhysicallyStopped;
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

        WheelCollider[] allWheels = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        foreach (WheelCollider wc_reset in allWheels)
        {
            if (wc_reset != null)
            {
                wc_reset.motorTorque = 0;
                // TEST: Try setting brakeTorque to 0 initially on reset to see if it releases immediately
                // wc_reset.brakeTorque = activeBrakeForce; // Original: Apply brakes to stop
                wc_reset.brakeTorque = 0f; // TEST: No brake on reset
                wc_reset.steerAngle = 0;
                Debug.Log($"Wheel {wc_reset.name}: MotorT=0, BrakeT={wc_reset.brakeTorque}, SteerA=0");

            }
        }
        Debug.Log($"Car has been reset. Next Update frame will read fresh inputs.");
    }

    // Helper for detailed wheel state logging
    void LogWheelStates(string context)
    {
        Debug.Log($"--- {context} Wheel States (Frame: {Time.frameCount}) ---");
        if (rb != null) Debug.Log($"Car RB Vel: {rb.linearVelocity.magnitude:F2}, AngVel: {rb.angularVelocity.magnitude:F2}, IsKinematic: {rb.isKinematic}");

        WheelCollider[] wheelsToCheck = { wheelFrontLeft, wheelFrontRight, wheelBackLeft, wheelBackRight };
        string[] wheelNames = { "FL", "FR", "BL", "BR" };
        for(int i=0; i < wheelsToCheck.Length; i++)
        {
            if (wheelsToCheck[i] != null)
            {
                Debug.Log($"{wheelNames[i]}: Grounded={wheelsToCheck[i].isGrounded}, RPM={wheelsToCheck[i].rpm:F1}, MotorT={wheelsToCheck[i].motorTorque:F1}, BrakeT={wheelsToCheck[i].brakeTorque:F1}, SteerA={wheelsToCheck[i].steerAngle:F1}, Radius={wheelsToCheck[i].radius:F2}");
            }
            else
            {
                Debug.Log($"{wheelNames[i]}: NOT ASSIGNED");
            }
        }
        Debug.Log("------------------------------------");
    }
}