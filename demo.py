import torch
import torch.nn as nn
import torch.optim as optim
import torch.nn.functional as F
import numpy as np
import random
from collections import deque, namedtuple
import time
import os
from typing import *

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple


#####################################
#          HYPERPARAMETERS          #
#####################################

# training environment
ENV_PATH = "linux_build/env_build.x86_64"
RUN_ID = "sac_rdn_jason_3"
NO_GRAPHICS = False
SEED = 44

# SAC Hyperparameters
SAC_LEARNING_RATE = 3e-4
SAC_BATCH_SIZE = 256
SAC_BUFFER_SIZE = 1000000
SAC_BUFFER_INIT_STEPS = 20000
SAC_TAU = 0.005
SAC_GAMMA = 0.99
SAC_INIT_ENTCOEF = 0.5
SAC_TARGET_ENTROPY = None
SAC_LR_ALPHA = 3e-4

# RND Hyperparameters
RND_STRENGTH = 0.01
RND_GAMMA = 0.99
RND_ENCODING_SIZE = 128
RND_LEARNING_RATE = 1e-3

# Observation and Action Space
NUM_CAMERA_OBS = 5
CAMERA_HEIGHT = 84
CAMERA_WIDTH = 84
CAMERA_CHANNELS = 1
VECTOR_OBS_SIZE = 6
CONTINUOUS_ACTION_SIZE = 3
ACTION_LOW = np.array([-1.0, 0.0, -1.0], dtype=np.float32)
ACTION_HIGH = np.array([1.0, 1.0, 1.0], dtype=np.float32)

# Network Settings
NORMALIZE_OBS = False
HIDDEN_UNITS = 512
VISUAL_FEATURE_SIZE = 256
NUM_LAYERS_MLP = 3
GRAD_CLIP_NORM = 5.0

# Training Loop
END_TRAINING_STEPS = int(1e12)
START_TRAINING_STEPS = 0
STEPS_PER_UPDATE = 5
SUMMARY_FREQ = 5000
CHECKPOINT_FREQ = 500000
OUTPUT_DIR = f"results/{RUN_ID}"


#####################################
#   Helper Functions and Classes    #
#####################################

def set_seeds(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)

Experience = namedtuple("Experience", field_names=["visual_obs_list", "vector_obs", "action", "reward", "next_visual_obs_list", "next_vector_obs", "done"])


class ReplayBuffer:
    def __init__(self, capacity:int, device: torch.DeviceObjType) -> None:
        self.buffer = deque(maxlen=capacity)
        self.device = device
    
    def add(self, visual_obs_list: List[np.ndarray], vector_obs: np.ndarray, action: np.ndarray, reward: float,
            next_visual_obs_list: List[np.ndarray], next_vector_obs: np.ndarray, done: bool) -> None:
        """Adds an experience to the buffer."""
        visual_obs_list_cpu = [torch.from_numpy(obs).float().cpu() for obs in visual_obs_list]
        vector_obs_cpu = torch.from_numpy(vector_obs).float().cpu()
        action_cpu = torch.from_numpy(action).float().cpu()
        next_visual_obs_list_cpu = [torch.from_numpy(obs).float().cpu() for obs in next_visual_obs_list]
        next_vector_obs_cpu = torch.from_numpy(next_vector_obs).float().cpu()

        e = Experience(visual_obs_list_cpu, vector_obs_cpu, action_cpu, reward, next_visual_obs_list_cpu, next_vector_obs_cpu, done)
        self.buffer.append(e)

    def sample(self, batch_size: int) -> Tuple[List[torch.Tensor], torch.Tensor, torch.Tensor, torch.Tensor, List[torch.Tensor], torch.Tensor, torch.Tensor]:
        experiences = random.sample(self.buffer, k=batch_size)

        visual_obs_lists = [torch.stack([e.visual_obs_list[i] for e in experiences]).to(self.device) for i in range(NUM_CAMERA_OBS)]
        vector_obs_batch = torch.stack([e.vector_obs for e in experiences]).to(self.device)
        actions_batch = torch.stack([e.action for e in experiences]).to(self.device)
        rewards_batch = torch.tensor([e.reward for e in experiences], dtype=torch.float32).unsqueeze(1).to(self.device)
        next_visual_obs_lists = [torch.stack([e.next_visual_obs_list[i] for e in experiences]).to(self.device) for i in range(NUM_CAMERA_OBS)]
        next_vector_obs_batch = torch.stack([e.next_vector_obs for e in experiences]).to(self.device)
        dones_batch = torch.tensor([e.done for e in experiences], dtype=torch.float32).unsqueeze(1).to(self.device)

        return (visual_obs_lists, vector_obs_batch, actions_batch, rewards_batch, next_visual_obs_lists, next_vector_obs_batch, dones_batch)

    def __len__(self) -> int:
        return len(self.buffer)


class RunningMeanStd(nn.Module):
    def __init__(self, shape: Tuple, device: torch.DeviceObjType) -> None:
        super().__init__()
        self.mean = torch.zeros(shape, dtype=torch.float32, device=device)
        self.var = torch.ones(shape, dtype=torch.float32, device=device)
        self.count = 1e-4
        self.device = device

    def update(self, x: torch.Tensor) -> None:
        batch_mean = torch.mean(x, dim=0)
        batch_var = torch.var(x, dim=0)
        batch_count = x.shape[0]

        delta = batch_count - self.mean
        tot_count = self.count + batch_count

        new_mean = self.mean + delta * batch_count / tot_count
        m_a = self.var * self.count
        m_b = batch_var * batch_count
        M2 = m_a + m_b + torch.square(delta) * self.count * batch_count / tot_count
        new_var = M2 / tot_count

        self.mean = new_mean
        self.var = new_var
        self.count = tot_count

    def normalize(self, x: torch.Tensor, clip_range: int = 5.0):
        normalized_x = (x - self.mean) / (torch.sqrt(self.var) + 1e-8)
        return torch.clamp(normalized_x, -clip_range, clip_range)


class VisualEncoder(nn.Module):
    def __init__(self, height: int, width: int, channels: int, output_size: int) -> None:
        super().__init__()

        # input: (N, C, H, W) = (N, 1, 84, 84)
        self.conv1 = nn.Conv2d(channels, 32, kernel_size=8, stride=4)   # output (N, 32, 20, 20)
        self.conv2 = nn.Conv2d(32, 64, kernel_size=4, stride=2)         # output (N, 64, 9, 9)
        self.conv3 = nn.Conv2d(64, 64, kernel_size=3, stride=1)         # output (N, 64, 7, 7)

        def conv_out_size(size: int, kernel: int, stride: int, padding: int = 0) -> int:
            return (size - kernel + 2 * padding) // stride + 1
        
        h, w = height, width
        h = conv_out_size(h, 8, 4)
        w = conv_out_size(w, 8, 4)
        h = conv_out_size(h, 4, 2)
        w = conv_out_size(w, 4, 2)
        h = conv_out_size(h, 3, 1)
        w = conv_out_size(w, 3, 1)
        flat_size = 64 * h * w

        self.fc = nn.Linear(flat_size, output_size)
        self.activation = nn.ReLU()

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = self.activation(self.conv1(x))
        x = self.activation(self.conv2(x))
        x = self.activation(self.conv3(x))
        x = x.view(x.size(0), -1)
        x = self.activation(self.fc(x))
        return x


class Actor(nn.Module):
    def __init__(self, visual_encoders: nn.ModuleList, num_visual_inputs: int, visual_feature_size: int, vector_input_size: int,
                 action_size: int, hidden_units: int, log_std_min: int = -20, log_std_max: int = 2) -> None:
        super().__init__()
        self.visual_encoders = visual_encoders

        total_feature_size = (num_visual_inputs * visual_feature_size) + vector_input_size

        self.fc1 = nn.Linear(total_feature_size, hidden_units)
        self.fc2 = nn.Linear(hidden_units, hidden_units)
        self.fc3 = nn.Linear(hidden_units, hidden_units)
        self.mean_layer = nn.Linear(hidden_units, action_size)
        self.log_std_layer = nn.Linear(hidden_units, action_size)

        self.log_std_min = log_std_min
        self.log_std_max = log_std_max
    
    def forward(self, visual_obs_list: List[torch.Tensor], vector_obs: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor]:
        visual_features = []
        for i, vis_obs in enumerate(visual_obs_list):
            visual_features.append(self.visual_encoders[i](vis_obs))
        
        x = torch.cat(visual_features + [vector_obs], dim=-1)

        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        x = F.relu(self.fc3(x))

        mean = self.mean_layer(x)
        log_std = self.log_std_layer(x)
        log_std = torch.clamp(log_std, self.log_std_min, self.log_std_max)

        return mean, log_std

    def sample(self, visual_obs_list: List[torch.Tensor], vector_obs: torch.Tensor, deterministic: bool = False, with_logprob: bool = True) -> Tuple[torch.Tensor, torch.Tensor]:
        mean, log_std = self.forward(visual_obs_list, vector_obs)
        std = log_std.exp()

        normal = torch.distributions.Normal(mean, std)

        if deterministic:
            z = mean
        else:
            z = normal.rsample()
        
        action = torch.tanh(z)

        if with_logprob:
            log_prob = normal.log_prob(z)
            log_prob -= torch.log(1 - action.pow(2) + 1e-6)
            log_prob = log_prob.sum(axis=-1, keepdim=True)
        else:
            log_prob = None
        
        return action, log_prob


class Critic(nn.Module):
    def __init__(self, visual_encoders: nn.ModuleList, num_visual_inputs: int, visual_feature_size: int, vector_input_size: int, action_size: int, hidden_units: int) -> None:
        super().__init__()
        self.visual_encoders = visual_encoders

        total_feature_size = (num_visual_inputs * visual_feature_size) + vector_input_size + action_size

        self.fc1 = nn.Linear(total_feature_size, hidden_units)
        self.fc2 = nn.Linear(hidden_units, hidden_units)
        self.fc3 = nn.Linear(hidden_units, hidden_units)
        self.fc4 = nn.Linear(hidden_units, 1)
    
    def forward(self, visual_obs_list: List[torch.Tensor], vector_obs: torch.Tensor, action: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor]:
        visual_features = []
        for i, vis_obs in enumerate(visual_obs_list):
            visual_features.append(self.visual_encoders[i](vis_obs))

        x = torch.cat(visual_features + [vector_obs, action], dim=-1)

        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        x = F.relu(self.fc3(x))
        x = self.fc4(x)

        return x


class RNDModel(nn.Module):
    def __init__(self, visual_encoders: nn.ModuleList, num_visual_inputs: int, visual_feature_size: int, vector_input_size: int, output_size: int, hidden_units: int) -> None:
        super().__init__()

        self.visual_encoders = visual_encoders

        total_feature_size = (num_visual_inputs * visual_feature_size) + vector_input_size

        self.target = nn.Sequential(
            nn.Linear(total_feature_size, hidden_units),
            nn.ReLU(),
            nn.Linear(hidden_units, hidden_units),
            nn.ReLU(),
            nn.Linear(hidden_units, output_size)
        )
        
        self.predictor = nn.Sequential(
            nn.Linear(total_feature_size, hidden_units),
            nn.ReLU(),
            nn.Linear(hidden_units, hidden_units),
            nn.ReLU(),
            nn.Linear(hidden_units, output_size)
        )

        for param in self.target.parameters():
            param.requires_grad = False

    def _extract_features(self, visual_obs_list: List[torch.Tensor], vector_obs: torch.Tensor) -> torch.Tensor:
        visual_features = []
        for i, vis_obs in enumerate(visual_obs_list):
            visual_features.append(self.visual_encoders[i](vis_obs))
        features = torch.cat(visual_features + [vector_obs], dim=-1)

        return features

    def forward(self, visual_obs_list: List[torch.Tensor], vector_obs: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor]:
        features = self._extract_features(visual_obs_list, vector_obs)
        target_features = self.target(features)
        predicted_features = self.predictor(features)
        return target_features, predicted_features


#####################################
#         Main Training Loop        #
#####################################


def main():
    set_seeds(SEED)
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    engine_config_channel = EngineConfigurationChannel()
    engine_config_channel.set_configuration_parameters(time_scale=1.0, width=1200, height=720)

    env = UnityEnvironment(file_name=ENV_PATH, worker_id=0, no_graphics=NO_GRAPHICS, side_channels=[engine_config_channel], base_port=11454)
    env.reset()
    behavior_name = list(env.behavior_specs.keys())[0]
    
    print(behavior_name)
    
    spec = env.behavior_specs[behavior_name]

    if spec.action_spec.continuous_size != CONTINUOUS_ACTION_SIZE:
        raise ValueError(f"Action size mismatch: Env expects {spec.action_spec.continuous_size}, script configured for {CONTINUOUS_ACTION_SIZE}")
    
    vec_obs_actual_size = 0
    vis_obs_count_actual = 0
    for obs_spec in spec.observation_specs:
        if len(obs_spec.shape) == 1: # vector observation
            vec_obs_actual_size += obs_spec.shape[0]
        elif len(obs_spec.shape) == 3: # visual observation
            vis_obs_count_actual += 1
    
    if vec_obs_actual_size != VECTOR_OBS_SIZE:
        raise ValueError(f"Vector_obs size mismatch: Env has {vec_obs_actual_size}, script configured for {VECTOR_OBS_SIZE}")
    if vis_obs_count_actual != NUM_CAMERA_OBS:
        raise ValueError(f"Num visual_obs mismatch: Env has {vis_obs_count_actual}, script configured for {NUM_CAMERA_OBS}")
    
    print("before load")
    
    visualencoders = nn.ModuleList([VisualEncoder(CAMERA_HEIGHT, CAMERA_WIDTH, CAMERA_CHANNELS, VISUAL_FEATURE_SIZE) for _ in range(NUM_CAMERA_OBS)])
    actor = Actor(visualencoders, NUM_CAMERA_OBS, VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, CONTINUOUS_ACTION_SIZE, HIDDEN_UNITS).to(device)
    actor.load_state_dict(torch.load('results/sac_rdn_jason_3/actor_17000000.pth', map_location=device))

    actor.eval()

    print("loaded")

    if NORMALIZE_OBS:
        vector_obs_rms = RunningMeanStd(shape=(VECTOR_OBS_SIZE,), device=device)

    total_steps = START_TRAINING_STEPS
    episode_count = 0

    decision_stpes, terminal_steps = env.get_steps(behavior_name)
    agent_id = list(decision_stpes)[0]
    current_obs_info = decision_stpes[agent_id]

    def preprocess_observation(obs_list_from_env: List[np.ndarray], specs_list: np.ndarray, device_target: torch.DeviceObjType) -> Tuple[torch.Tensor, torch.Tensor]:
        visual_obs_processed = []
        vector_obs_processed = []
        for i, obs_data_np in enumerate(obs_list_from_env):
            spec = specs_list[i]
            if len(spec.shape) == 3: # Visual
                visual_obs_processed.append(torch.from_numpy(obs_data_np).float().unsqueeze(0).to(device_target))
            else: # Vector
                vector_obs_processed.append(torch.from_numpy(obs_data_np).float().unsqueeze(0).to(device_target))
        
        if not vector_obs_processed:
            final_vector_obs = torch.empty(1, 0, device=device_target)
        elif len(vector_obs_processed) > 1:
            final_vector_obs = torch.cat(vector_obs_processed, dim=-1)
        else:
            final_vector_obs = vector_obs_processed[0]
        
        return visual_obs_processed, final_vector_obs
    

    current_visual_obs_list, current_vector_obs = preprocess_observation(current_obs_info.obs, spec.observation_specs, device)

    if NORMALIZE_OBS:
        vector_obs_rms.update(current_obs_info)
    
    episode_reward_sum = 0
    episode_steps = 0

    start_time = time.time()

    step = START_TRAINING_STEPS

    while step <= END_TRAINING_STEPS:
        step += 1
        total_steps += 1
        episode_steps += 1

        with torch.no_grad():
            vec_obs_for_actor = vector_obs_rms.normalize(current_vector_obs) if NORMALIZE_OBS else current_vector_obs
            action_tensor, _ = actor.sample(current_visual_obs_list, vec_obs_for_actor, deterministic=True)
            action_np = action_tensor.squeeze(0).cpu().numpy()
    
        print(action_np)
        action_tuple = ActionTuple(continuous=np.expand_dims(action_np, axis=0))
        env.set_actions(behavior_name, action_tuple)
        env.step()

        decision_stpes, terminal_steps = env.get_steps(behavior_name)

        done = False
        reward = 0.0

        next_visual_obs_list_tensor = None # Placeholder
        next_vector_obs_tensor = None    # Placeholder

        if agent_id in terminal_steps:
            done = True
            term_info = terminal_steps[agent_id]
            reward = term_info.reward - 5
            next_visual_obs_list, next_vector_obs = preprocess_observation(term_info.obs, spec.observation_specs, device)
        elif agent_id in decision_stpes:
            step_info = decision_stpes[agent_id]
            reward = step_info.reward
            next_visual_obs_list, next_vector_obs = preprocess_observation(step_info.obs, spec.observation_specs, device)
        else:
            done = True
            reward = 0.0
            next_visual_obs_list = current_visual_obs_list
            next_vector_obs = current_vector_obs
        
        episode_reward_sum += reward

        intrinsic_reward = 0.0
        
        total_reward = reward + intrinsic_reward

        current_vis_obs_list_np = [vo.squeeze(0).cpu().numpy() for vo in current_visual_obs_list]
        current_vector_obs_np = current_vector_obs.squeeze(0).cpu().numpy()
        next_vis_obs_list_np = [vo.squeeze(0).cpu().numpy() for vo in next_visual_obs_list]
        next_vector_obs_np = next_vector_obs.squeeze(0).cpu().numpy()

        current_visual_obs_list = next_visual_obs_list
        current_vector_obs = next_vector_obs

        if NORMALIZE_OBS and not done:
            vector_obs_rms.update(current_vector_obs)
        
        if done:
            episode_count += 1
            avg_steps_per_sec = step / (time.time() - start_time + 1e-6)

            episode_reward_sum = 0
            episode_steps = 0

            for i in range(10):
                env.step()
    env.close()


if __name__ == "__main__":
    main()