using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    // starting check
    public GameObject StartCheckpoint;
    private Renderer StartCheckpointRenderer;

    // vertical parking check
    public GameObject BackParkCheckpoint1;
    public GameObject BackParkCheckpoint2;
    private Renderer BackParkCheckpoint1Renderer;
    private Renderer BackParkCheckpoint2Renderer;

    // horizontal parking check
    public GameObject SideParkCheckpoint1;
    public GameObject SideParkCheckpoint2;
    private Renderer SideParkCheckpoint1Renderer;
    private Renderer SideParkCheckpoint2Renderer;

    // S curve check
    public GameObject SCurveCheckpoint;
    private Renderer SCurveCheckpointRenderer;

    // traffic check
    public GameObject TrafficEnterCheckpoint;
    public GameObject TrafficExitCheckpoint;
    private Renderer TrafficEnterCheckpointRenderer;
    private Renderer TrafficExitCheckpointRenderer;

    // up hill stopping check
    public GameObject UpHillFrontCheckpoint1;
    public GameObject UpHillFrontCheckpoint2;
    public GameObject UpHillLight;
    private Renderer UpHillFrontCheckpoint1Renderer;
    private Renderer UpHillFrontCheckpoint2Renderer;
    private Renderer UpHillLightRenderer;

    // stop sign check
    public GameObject StopSignEnterCheckpoint;
    public GameObject StopSignExitCheckPoint;
    private Renderer StopSignEnterCheckpointRenderer;
    private Renderer StopSignExitCheckpointRenderer;

    // level passing check
    public GameObject LevelPassingEnterCheckpoint;
    public GameObject LevelPassingExitCheckpoint;
    private Renderer LevelPassingEnterCheckpointRenderer;
    private Renderer LeverPassingExitCheckpointRenderer;

    void Start()
    {
        
    }
}
