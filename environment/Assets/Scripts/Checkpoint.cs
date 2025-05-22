using UnityEngine;

public abstract class Checkpoint : MonoBehaviour
{
    public bool isActive = false;
    [HideInInspector] public RootCheckpointManager rootManager;

    public Checkpoint nextCheckpoint_A;
    public Checkpoint nextCheckpoint_B;

    [Header("Checkpoint Visuals")]
    [Tooltip("The Renderer component whose material will be changed (e.g., the checkpoint's visual mesh).")]
    public Renderer checkpointRenderer;
    [Tooltip("Material to apply when the checkpoint is active/enabled.")]
    public Material activeMaterial;
    [Tooltip("Material to apply when the checkpoint is inactive/disabled.")]
    public Material inactiveMaterial;

    protected virtual void Awake()
    {
        rootManager = FindObjectOfType<RootCheckpointManager>();
        if (rootManager == null)
        {
            Debug.LogError($"RootCheckpointManager not found by {name}! Ensure one exists in the scene.");
        }

        // Attempt to get the Renderer if not assigned explicitly in the Inspector
        // This assumes the Renderer is on the same GameObject as the Checkpoint script.
        // If it's on a child, you'll need to assign it manually or use GetComponentInChildren.
        if (checkpointRenderer == null)
        {
            checkpointRenderer = GetComponent<Renderer>();
        }
        if (checkpointRenderer == null) // As a fallback, try a child
        {
            checkpointRenderer = GetComponentInChildren<Renderer>();
        }

        if (checkpointRenderer == null && (activeMaterial != null || inactiveMaterial != null))
        {
            Debug.LogWarning($"Checkpoint '{name}' has materials assigned but no 'Checkpoint Renderer'. Visual state changes will not occur.", this);
        }

        // Set initial visual state to inactive.
        // RootCheckpointManager's Start() calls DeactivateCheckpoint on all, which will also do this.
        ApplyMaterialState(false);
    }

    public virtual void ActivateCheckpoint()
    {
        isActive = true;
        gameObject.SetActive(true); // Ensure the GameObject and its colliders are active
        ApplyMaterialState(true);   // Apply active material
        Debug.Log($"{name} Activated, material set to active.");
    }

    public virtual void DeactivateCheckpoint()
    {
        isActive = false;
        // To see the inactive material, the GameObject itself must remain active.
        // The 'isActive' flag will prevent trigger processing.
        // gameObject.SetActive(false); // If you uncomment this, you won't see the inactive material.
        ApplyMaterialState(false);  // Apply inactive material
        Debug.Log($"{name} Deactivated, material set to inactive.");
    }

    // Helper method to apply materials
    protected void ApplyMaterialState(bool activate)
    {
        if (checkpointRenderer == null) return; // No renderer to apply to

        if (activate)
        {
            if (activeMaterial != null)
            {
                checkpointRenderer.material = activeMaterial;
            }
            // else Debug.LogWarning($"Checkpoint '{name}' has no Active Material assigned.", this);
        }
        else
        {
            if (inactiveMaterial != null)
            {
                checkpointRenderer.material = inactiveMaterial;
            }
            // else Debug.LogWarning($"Checkpoint '{name}' has no Inactive Material assigned.", this);
        }
    }

    // This method is called by OnTriggerEnter/OnTriggerExit (implemented below)
    public void ProcessWheelTrigger(string wheelType, CarController car, bool isEnter)
    {
        if (!isActive) return; // CRITICAL: Only process triggers if the checkpoint is logically active

        if (isEnter)
        {
            HandleCollisionLogic(wheelType, car);
        }
        else
        {
            HandleExitLogic(wheelType, car);
        }
    }

    // Abstract method for entry logic (implemented by derived checkpoint scripts)
    protected abstract void HandleCollisionLogic(string wheelType, CarController car);

    // Virtual method for exit logic (can be overridden by derived checkpoint scripts like Checkpoint7_1)
    protected virtual void HandleExitLogic(string wheelType, CarController car) { }

    // OnTriggerEnter and OnTriggerExit to detect WheelColliderTag
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!isActive) return; // Double-check, though ProcessWheelTrigger also checks

        WheelColliderTag wheelTag = other.GetComponent<WheelColliderTag>();
        if (wheelTag != null && wheelTag.carController != null)
        {
            string detectedWheelType = wheelTag.wheelCategory.ToString();
            ProcessWheelTrigger(detectedWheelType, wheelTag.carController, true);
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!isActive) return;

        WheelColliderTag wheelTag = other.GetComponent<WheelColliderTag>();
        if (wheelTag != null && wheelTag.carController != null)
        {
            string detectedWheelType = wheelTag.wheelCategory.ToString();
            ProcessWheelTrigger(detectedWheelType, wheelTag.carController, false);
        }
    }
}