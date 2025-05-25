using UnityEngine;
using System.Collections; // Required for Coroutines

public class LevelCrossingLight : MonoBehaviour
{
    [Header("Light GameObjects")]
    public GameObject leftLight;
    public GameObject rightLight;

    [Header("Light Materials")]
    public Material lightOnMaterial;    // Assign the material for when the light is ON
    public Material lightOffMaterial;   // Assign the material for when the light is OFF

    [Header("Flashing Parameters")]
    public float flashDuration = 0.5f;   // How long each light stays on/off during a flash cycle
    public float activeDuration = 10f;   // How long the lights flash in total

    [Header("State")]
    public bool isLightActive = false; // Public property to check activation status

    // Private references to the Renderers
    private Renderer leftRenderer;
    private Renderer rightRenderer;
    private Coroutine currentFlashRoutine; // To keep track of the running coroutine

    // Awake is called when the script instance is being loaded
    void Awake()
    {
        // Get the Renderer components from the spheres
        if (leftLight != null) leftRenderer = leftLight.GetComponent<Renderer>();
        if (rightLight != null) rightRenderer = rightLight.GetComponent<Renderer>();

        // Optional: Log warnings if any renderers couldn't be found
        if (leftRenderer == null) Debug.LogWarning("LeftLight GameObject or its Renderer not assigned/found on LevelCrossingLight script.", this);
        if (rightRenderer == null) Debug.LogWarning("RightLight GameObject or its Renderer not assigned/found on LevelCrossingLight script.", this);
    }

    // Start is called before the first frame update
    void Start()
    {
        // Ensure lights are off at the start of the scene
        TurnLightsOff();
    }

    // Public method to activate or deactivate the crossing lights
    public void SetLightActive(bool status)
    {
        if (isLightActive == status) return; // Avoid re-triggering if already in desired state

        isLightActive = status;
        Debug.Log($"Level Crossing Light Active: {isLightActive}");

        if (status)
        {
            // If activation is requested, stop any existing routine and start a new one
            if (currentFlashRoutine != null)
            {
                StopCoroutine(currentFlashRoutine);
            }
            currentFlashRoutine = StartCoroutine(FlashLightsRoutine());
        }
        else
        {
            // If deactivation is requested, stop the routine and turn lights off
            if (currentFlashRoutine != null)
            {
                StopCoroutine(currentFlashRoutine);
                currentFlashRoutine = null;
            }
            TurnLightsOff();
        }
    }

    // Coroutine for the flashing sequence
    private IEnumerator FlashLightsRoutine()
    {
        float timer = 0f;

        // Loop for the total activeDuration
        while (timer < activeDuration)
        {
            // Left light ON, Right light OFF
            if (leftRenderer != null) leftRenderer.material = lightOnMaterial;
            if (rightRenderer != null) rightRenderer.material = lightOffMaterial;
            yield return new WaitForSeconds(flashDuration);
            timer += flashDuration;

            // Check if activeDuration is met during the yield
            if (timer >= activeDuration) break;

            // Left light OFF, Right light ON
            if (leftRenderer != null) leftRenderer.material = lightOffMaterial;
            if (rightRenderer != null) rightRenderer.material = lightOnMaterial;
            yield return new WaitForSeconds(flashDuration);
            timer += flashDuration;
        }

        // After the loop, turn both lights off and deactivate
        TurnLightsOff();
        isLightActive = false; // Ensure state is correctly updated after sequence
        Debug.Log("Level Crossing Light sequence finished. Deactivated.");
    }

    // Helper method to ensure both lights are off
    private void TurnLightsOff()
    {
        if (leftRenderer != null) leftRenderer.material = lightOffMaterial;
        if (rightRenderer != null) rightRenderer.material = lightOffMaterial;
    }
}