# RL Agent for Taiwanese Driving School Test Simulation

## 🚀 Project Description

This project aims to develop a Reinforcement Learning (RL) agent capable of autonomously learning to pass various driving test maneuvers within a simulated Taiwanese driving school environment. The goal is for the agent to master tasks such as S-curves (forward and reverse), parallel parking, and garage parking, ultimately achieving or exceeding the average human pass rate.

---

## ✨ Features (Planned or Implemented)

* **Realistic Simulation Environment:**
    * [ ] 2D or 3D Visual Simulation (e.g., using Pygame, Unity)
    * [ ] Accurate replication of Taiwanese driving school test course layouts and dimensions.
    * [ ] Basic vehicle dynamics simulation.
    * [ ] Simulation of sensor lines/pressure plates as per test course rules.
* **Reinforcement Learning Agent:**
    * [ ] Observation space based on visual input (e.g., camera feed) or vector states (e.g., vehicle pose, sensor readings).
    * [ ] Reward function designed according to official Taiwanese driving test scoring criteria.
    * [ ] Implementation of state-of-the-art RL algorithms (e.g., PPO, SAC, DQN).
    * [ ] Support for Curriculum Learning strategies.
* **Driving Test Maneuver Support (Phased Implementation):**
    * [ ] Straight Line Driving & Stability
    * [ ] S-Curve (Forward)
    * [ ] S-Curve (Reverse)
    * [ ] Garage Parking (Reverse Parking)
    * [ ] Parallel Parking
    * [ ] Uphill Start (if applicable to the simulation)
    * [ ] (Other test items...)
* **Visualization & Analysis:**
    * [ ] Training progress visualization (e.g., reward curves).
    * [ ] Agent's driving behavior playback/replay.
    * [ ] Simulated test results and deduction point analysis.

---

## 🛠️ Environment Setup & Installation

create environment for python 3.10.12

```
conda create --name drivingclass python=3.10.12 -y
conda activate drivingclass
pip install -r requirements.txt
```

## ▶️ How to Use

run the test `env_test_agent.py` and unity project at `./environment`

1. open unity project, add project from disk and select the `./environment` directory
2. at project window, navigate into `Assets/Scenes` double click `SampleScene`
3. run the testing agent with `python env_test_agent.py`, it will try to connect to unity editor
4. press the run button at the top of unity editor to run the environment, the `env_test_agent.py` will connect to your unity editor

if run successfully, the car will bump into the wall and quickly start next episode and continue.

## 💻 Technology Stack

## 📂 Project Structure
