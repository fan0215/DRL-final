using UnityEngine;
using System.Collections; // Needed for Coroutines

public class LevelCrossingLightController : MonoBehaviour
{
    public GameObject Light1;
    public GameObject Light2;

    public Material lightOnMaterial;
    public Material lightOffMaterial;

    public float switchDuration = 1f;

    private Renderer Light1Renderer;
    private Renderer Light2Renderer;

    void Start()
    {
        // Get the Renderer components from the spheres
        Light1Renderer = Light1.GetComponent<Renderer>();
        Light2Renderer = Light2.GetComponent<Renderer>();

        // Start the traffic light sequence
        StartCoroutine(TrafficLightSequence());
    }

    IEnumerator TrafficLightSequence()
    {
        while (true) // Loop forever
        {
            SetLight(Light1Renderer, lightOnMaterial);
            SetLight(Light2Renderer, lightOffMaterial);
            yield return new WaitForSeconds(switchDuration);

            SetLight(Light1Renderer, lightOffMaterial);
            SetLight(Light2Renderer, lightOnMaterial);
            yield return new WaitForSeconds(switchDuration);
        }
    }

    // Helper function to switch the material of a light
    void SetLight(Renderer lightRenderer, Material targetMaterial)
    {
        if (lightRenderer != null && targetMaterial != null)
        {
            lightRenderer.material = targetMaterial;
        }
    }
}