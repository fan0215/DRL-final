# RL Agent for Taiwanese Driving School Test Simulation

## 🚀 Project Description

This project aims to develop a Reinforcement Learning (RL) agent capable of autonomously learning to pass various driving test maneuvers within a simulated Taiwanese driving school environment. The goal is for the agent to master tasks such as S-curves (forward and reverse), parallel parking, and garage parking, ultimately achieving or exceeding the average human pass rate.

---

## ✨ Features (Planned or Implemented)

* **Realistic Simulation Environment:**
    * [x] 3D Visual Simulation (Unity)
    * [ ] Accurate replication of Taiwanese driving school test course layouts and dimensions.
    * [x] Basic vehicle dynamics simulation.
    * [x] Simulation of sensor lines/deduction points as per test course rules.
* **Reinforcement Learning Agent:**
    * [x] Observation space based on vector states (e.g., vehicle pose, sensor readings, target locations).
    * [ ] Visual observation space (e.g., camera feed from the car).
    * [x] Reward shaping defined in the Unity environment to guide learning effectively.
    * [x] Implementation of state-of-the-art RL algorithms (PPO via ML-Agents).
    * [ ] Exploration of Curriculum Learning strategies for phased skill acquisition.
* **Driving Test Maneuver Support (Phased Implementation):**
    * [ ] Straight Line Driving & Stability
    * [ ] S-Curve (Forward)
    * [ ] S-Curve (Reverse)
    * [ ] Garage Parking (Reverse Parking)
    * [ ] Parallel Parking
    * [ ] Uphill Start
    * [ ] Lane Change Stability / Following Traffic Rules (if extended)
* **Visualization & Analysis:**
    * [x] Training progress visualization using TensorBoard (reward curves, episode length, etc.).
    * [ ] Agent's driving behavior playback/replay in Unity.
    * [ ] Simulated test results and deduction point analysis.

---

## 🛠️ Environment Setup & Installation
1. **Clone the repository:**
```bash
git clone git@github.com:fan0215/DRL-final.git
```
2.  **Create and activate virtual environment** (Python 3.10.12 recommended):
```bash
conda create --name drivingclass python=3.10.12 -y
conda activate drivingclass
```
3. **Install Python dependencies:**
```
pip install -r requirements.txt
```
If you need CUDA support, please visit [https://pytorch.org/get-started/locally/](https://pytorch.org/get-started/locally/) and install the corresponding CUDA version.

4.  **Unity Setup:**
    * Ensure you have Unity Hub and a compatible Unity Editor version installed (6000.1.3f1)

## ▶️ How to Use
### Build the Project
To build the project by yourself. You should:

1. Clone the repository.
2. Open the Unity project, add project from disk and select `environment/` directory.
3. `File` => `Build Profiles` => Choose your platform

After some times, your environment should be ready.

### Training
1. **Configure Training:**: edit the `trainer_config.yaml` to set hyperparameters, network architecture, reward signals, etc., for your agent.
2.  **Start Training:**
There are two ways to run the training code.
* **Using a built executable (recommended):**
    ```bash
    # Replace <your_run_id> with a descriptive name for your training run
    # Replace <path_to_executable> with the actual path (the directory) to your built Unity environment
    mlagents-learn config/driving_agent_config.yaml --run-id=<your_run_id> --env=<path_to_executable> --no-graphics 
    # Use --no-graphics for headless builds
    ```
    * If you have multiple environment instances in your build or want to run multiple parallel executables, add `--num-envs=<number_of_environments>`.
* **Connecting to the Unity Editor (for debugging/quick tests):**
    ```bash
    mlagents-learn config/driving_agent_config.yaml --run-id=<your_run_id>_editor_test
    ```
    After `mlagents-learn` prints "Listening on port 5004... Start training by pressing the Play button in the Unity Editor.", press the ▶️ (Play) button in the Unity Editor.

    Be sure to set the `Run In Background` option in `Edit` => `Project Settings` => `Player`.

3.  **Monitor Training:** Use TensorBoard to visualize training progress:
```bash
tensorboard --logdir results
```

### Quick Testing (Temporal)

run the test `env_test_agent.py` and unity project at `./environment`

1. open unity project, add project from disk and select the `./environment` directory
2. at project window, navigate into `Assets/Scenes` double click `SampleScene`
3. run the testing agent with `python env_test_agent.py`, it will try to connect to unity editor
4. press the run button at the top of unity editor to run the environment, the `env_test_agent.py` will connect to your unity editor

if run successfully, the car will bump into the wall and quickly start next episode and continue.

## 💻 Technology Stack

## 📂 Project Structure
