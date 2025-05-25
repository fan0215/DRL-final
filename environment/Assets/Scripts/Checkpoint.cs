using UnityEngine;

public abstract class Checkpoint : MonoBehaviour
{
    public bool isActive = false;
    [HideInInspector] public RootCheckpointManager rootManager;

    [Header("Progression")]
    [Tooltip("The next primary checkpoint to activate.")]
    public Checkpoint nextCheckpoint_A;
    [Tooltip("A secondary next checkpoint to activate, if applicable (e.g., for branching or parallel activations).")]
    public Checkpoint nextCheckpoint_B;

    [Header("Spawning")]
    [Tooltip("The index in CarController's 'Checkpoint Spawn Points' list. This checkpoint will use this spawn point if a crash resets to it.")]
    public int spawnPointIndex = 0;

    [Header("Checkpoint Visuals")]
    [Tooltip("The Renderer component whose material will be changed (e.g., the checkpoint's visual mesh). Assign this if visuals are needed.")]
    public Renderer checkpointRenderer;
    [Tooltip("Material to apply when the checkpoint is active/enabled.")]
    public Material activeMaterial;
    [Tooltip("Material to apply when the checkpoint is inactive/disabled.")]
    public Material inactiveMaterial;

    protected virtual void Awake()
    {
        // Ensure RootCheckpointManager is found
        if (rootManager == null) 
        {
            rootManager = FindObjectOfType<RootCheckpointManager>();
        }
        if (rootManager == null)
        {
            Debug.LogError($"Checkpoint '{name}': RootCheckpointManager not found in the scene! This is critical for operation.", this);
            // Consider disabling the component if RootManager is essential and not found
            // this.enabled = false; 
            // return;
        }

        // Attempt to get Renderer if not assigned explicitly in the Inspector
        if (checkpointRenderer == null)
        {
            checkpointRenderer = GetComponent<Renderer>();
        }
        if (checkpointRenderer == null) // Fallback to searching in children
        {
            checkpointRenderer = GetComponentInChildren<Renderer>();
        }

        if (checkpointRenderer == null && (activeMaterial != null || inactiveMaterial != null))
        {
            Debug.LogWarning($"Checkpoint '{name}' has materials assigned for visual state changes, but no 'Checkpoint Renderer' could be found or was assigned. Visuals will not update.", this);
        }
        
        // Initial visual state should reflect inactive. RootCheckpointManager's initial DeactivateAll
        // will call DeactivateCheckpoint, which handles this.
        // ApplyMaterialState(false); // Can be set here, but will be overwritten by RootManager init
    }

    public virtual void ActivateCheckpoint()
    {
        isActive = true;
        if(gameObject != null) gameObject.SetActive(true); // Ensure GameObject is active for colliders & visuals
        ApplyMaterialState(true);   // Apply active material
        Debug.Log($"{name} Activated. Visuals set to Active Material.");
    }

    public virtual void DeactivateCheckpoint()
    {
        isActive = false;
        // GameObject remains active to show the inactiveMaterial.
        // The 'isActive' flag prevents ProcessWheelTrigger from doing anything.
        ApplyMaterialState(false);  // Apply inactive material
        Debug.Log($"{name} Deactivated. Visuals set to Inactive Material.");
    }

    // Helper method to apply materials based on activation state
    protected void ApplyMaterialState(bool activate)
    {
        if (checkpointRenderer == null) return; // No renderer assigned to apply materials to

        Material targetMaterial = activate ? activeMaterial : inactiveMaterial;
        
        if (targetMaterial != null)
        {
            // Check if the material is already the target material to avoid unnecessary swaps
            // (especially if it creates new material instances, though direct assignment here usually shares)
            if (checkpointRenderer.sharedMaterial != targetMaterial) // Use sharedMaterial for comparison to avoid instancing from comparison
            {
                 checkpointRenderer.material = targetMaterial; // This might create an instance if not careful, but is standard.
                                                              // For performance with many objects, consider material property blocks or sharedMaterial if appropriate.
            }
        }
        // else: A material slot (active or inactive) might be unassigned. No visual change for that state.
        // Debug.LogWarning($"Checkpoint '{name}' attempting to apply material state '{activate}', but corresponding material slot is empty.", this);
    }

    // Central processing for wheel trigger events
    public void ProcessWheelTrigger(string wheelType, CarController car, bool isEnter)
    {
        if (!isActive) return; // CRITICAL: Only process if this checkpoint is logically active

        if (isEnter)
        {
            HandleCollisionLogic(wheelType, car);
        }
        else
        {
            HandleExitLogic(wheelType, car);
        }
    }

    // Abstract method for entry logic - to be implemented by specific derived checkpoint scripts
    protected abstract void HandleCollisionLogic(string wheelType, CarController car);

    // Virtual method for exit logic - can be overridden by specific derived checkpoint scripts (e.g., Checkpoint7_1)
    protected virtual void HandleExitLogic(string wheelType, CarController car) { }

    // Unity message called when another Collider enters this GameObject's trigger Collider
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!isActive || other == null) return; // Essential guards: checkpoint must be active, and collider must exist

        // Uncomment for detailed debugging of ALL trigger entries if needed:
        // Debug.Log($"Checkpoint '{this.name}' (Active: {this.isActive}) OnTriggerEnter with '{other.name}' (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");

        WheelColliderTag wheelTag = other.GetComponent<WheelColliderTag>();
        if (wheelTag != null) // Check if the colliding object is a tagged wheel
        {
            if (wheelTag.carController != null) // Ensure the wheel tag has a reference to its CarController
            {
                string detectedWheelType = wheelTag.wheelCategory.ToString(); // "FrontWheel" or "BackWheel"
                // Debug.Log($"Checkpoint '{this.name}': WheelColliderTag found on '{other.name}'. Type: {detectedWheelType}. Processing entry.");
                ProcessWheelTrigger(detectedWheelType, wheelTag.carController, true);
            }
            // else: Log warning if a WheelColliderTag is found but has no CarController reference (misconfiguration)
            // Debug.LogWarning($"Checkpoint '{this.name}' encountered WheelColliderTag on '{other.name}' which is missing its CarController reference.", wheelTag);
        }
        // else: The colliding object is not a specifically tagged wheel.
        // For this system, we only care about WheelColliderTag interactions for checkpoint logic.
    }

    // Unity message called when another Collider exits this GameObject's trigger Collider
    protected virtual void OnTriggerExit(Collider other)
    {
        if (!isActive || other == null) return; // Essential guards

        // Uncomment for detailed debugging:
        // Debug.Log($"Checkpoint '{this.name}' (Active: {this.isActive}) OnTriggerExit with '{other.name}' (Tag: {other.tag})");

        WheelColliderTag wheelTag = other.GetComponent<WheelColliderTag>();
        if (wheelTag != null)
        {
            if (wheelTag.carController != null)
            {
                string detectedWheelType = wheelTag.wheelCategory.ToString();
                // Debug.Log($"Checkpoint '{this.name}': WheelColliderTag on '{other.name}' exiting. Type: {detectedWheelType}. Processing exit.");
                ProcessWheelTrigger(detectedWheelType, wheelTag.carController, false);
            }
            // else: WheelColliderTag found but no CarController reference.
            // Debug.LogWarning($"Checkpoint '{this.name}' encountered WheelColliderTag on '{other.name}' (exiting) which is missing its CarController reference.", wheelTag);
        }
    }
}