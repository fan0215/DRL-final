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
RUN_ID = "sac_rdn_jason_2"
NO_GRAPHICS = True
SEED = 43

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
HIDDEN_UNITS = 256
VISUAL_FEATURE_SIZE = 128
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
        self.fc3 = nn.Linear(hidden_units, 1)
    
    def forward(self, visual_obs_list: List[torch.Tensor], vector_obs: torch.Tensor, action: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor]:
        visual_features = []
        for i, vis_obs in enumerate(visual_obs_list):
            visual_features.append(self.visual_encoders[i](vis_obs))

        x = torch.cat(visual_features + [vector_obs, action], dim=-1)

        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        x = self.fc3(x)

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
    with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
        f.write(f"Python: Using device: {device}\n")

    engine_config_channel = EngineConfigurationChannel()
    engine_config_channel.set_configuration_parameters(time_scale=20.0, width=CAMERA_WIDTH, height=CAMERA_HEIGHT)

    env = UnityEnvironment(file_name=ENV_PATH, worker_id=0, no_graphics=NO_GRAPHICS, side_channels=[engine_config_channel], base_port=11451)
    env.reset()
    behavior_name = list(env.behavior_specs.keys())[0]
    spec = env.behavior_specs[behavior_name]
    with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
        f.write(f"Python: Behavior spec: {spec}\n")

    if spec.action_spec.continuous_size != CONTINUOUS_ACTION_SIZE:
        raise ValueError(f"Action size mismatch: Env expects {spec.action_spec.continuous_size}, script configured for {CONTINUOUS_ACTION_SIZE}")
    
    vec_obs_actual_size = 0
    vis_obs_count_actual = 0
    for obs_spec in spec.observation_specs:
        if len(obs_spec.shape) == 1: # vector observation
            vec_obs_actual_size += obs_spec.shape[0]
        elif len(obs_spec.shape) == 3: # visual observation
            vis_obs_count_actual += 1
            with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
                f.write(f"Python: Detected visual obs shape from env: {obs_spec.shape}\n")
    
    if vec_obs_actual_size != VECTOR_OBS_SIZE:
        raise ValueError(f"Vector_obs size mismatch: Env has {vec_obs_actual_size}, script configured for {VECTOR_OBS_SIZE}")
    if vis_obs_count_actual != NUM_CAMERA_OBS:
        raise ValueError(f"Num visual_obs mismatch: Env has {vis_obs_count_actual}, script configured for {NUM_CAMERA_OBS}")
    
    visualencoders = nn.ModuleList([VisualEncoder(CAMERA_HEIGHT, CAMERA_WIDTH, CAMERA_CHANNELS, VISUAL_FEATURE_SIZE) for _ in range(NUM_CAMERA_OBS)])
    actor = Actor(visualencoders, NUM_CAMERA_OBS, VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, CONTINUOUS_ACTION_SIZE, HIDDEN_UNITS).to(device)
    critic1 = Critic(visualencoders, NUM_CAMERA_OBS, VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, CONTINUOUS_ACTION_SIZE, HIDDEN_UNITS).to(device)
    critic2 = Critic(visualencoders, NUM_CAMERA_OBS, VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, CONTINUOUS_ACTION_SIZE, HIDDEN_UNITS).to(device)

    critic1_target = Critic(visualencoders, NUM_CAMERA_OBS,VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, CONTINUOUS_ACTION_SIZE, HIDDEN_UNITS).to(device)
    critic2_target = Critic(visualencoders, NUM_CAMERA_OBS,VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, CONTINUOUS_ACTION_SIZE, HIDDEN_UNITS).to(device)
    critic1_target.load_state_dict(critic1.state_dict())
    critic2_target.load_state_dict(critic2.state_dict())
    for p in critic1_target.parameters():
        p.requires_grad = False
    for p in critic2_target.parameters():
        p.requires_grad = False
    
    rnd_model = RNDModel(visualencoders, NUM_CAMERA_OBS, VISUAL_FEATURE_SIZE, VECTOR_OBS_SIZE, RND_ENCODING_SIZE, HIDDEN_UNITS).to(device)

    actor_optimizer = optim.Adam(actor.parameters(), lr=SAC_LEARNING_RATE)
    critic1_optimizer = optim.Adam(critic1.parameters(), lr=SAC_LEARNING_RATE)
    critic2_optimizer = optim.Adam(critic2.parameters(), lr=SAC_LEARNING_RATE)
    rnd_predictor_optimizer = optim.Adam(rnd_model.predictor.parameters(), lr=RND_LEARNING_RATE)

    if SAC_TARGET_ENTROPY is None:
        target_entropy = -torch.prod(torch.Tensor( (CONTINUOUS_ACTION_SIZE,) ).to(device)).item()
    else:
        target_entropy = SAC_TARGET_ENTROPY
    
    log_alpha = torch.tensor(np.log(SAC_INIT_ENTCOEF), dtype=torch.float32, requires_grad=True, device=device)
    alpha_optimizer = optim.Adam([log_alpha], lr=SAC_LR_ALPHA)

    replay_buffer = ReplayBuffer(SAC_BATCH_SIZE, device)

    if NORMALIZE_OBS:
        vector_obs_rms = RunningMeanStd(shape=(VECTOR_OBS_SIZE,), device=device)
        rnd_obs_rms = RunningMeanStd(shape=( (NUM_CAMERA_OBS * VISUAL_FEATURE_SIZE) + VECTOR_OBS_SIZE, ), device=device)
        rnd_target_feature_rms = RunningMeanStd(shape=(RND_ENCODING_SIZE,), device=device)
    

    with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
        f.write("Initialization complete. Starting training loop...\n")
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

        if total_steps < SAC_BUFFER_INIT_STEPS:
            action_np = np.random.uniform(low=ACTION_LOW, high=ACTION_HIGH, size=(CONTINUOUS_ACTION_SIZE,)).astype(np.float32)
        else:
            with torch.no_grad():
                vec_obs_for_actor = vector_obs_rms.normalize(current_vector_obs) if NORMALIZE_OBS else current_vector_obs
                action_tensor, _ = actor.sample(current_visual_obs_list, vec_obs_for_actor, deterministic=False)
                action_np = action_tensor.squeeze(0).cpu().numpy()
    
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
            with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
                f.write(f"Python: Warning: Agent {agent_id} not in decision_steps or terminal_steps. Assuming episode ended.\n")
            done = True
            reward = 0.0
            next_visual_obs_list = current_visual_obs_list
            next_vector_obs = current_vector_obs
        
        episode_reward_sum += reward

        intrinsic_reward = 0.0
        if RND_STRENGTH > 0 and total_steps > SAC_BUFFER_INIT_STEPS:
            with torch.no_grad():
                next_vis_features_rnd = [enc(vo) for enc, vo in zip(rnd_model.visual_encoders, next_visual_obs_list)]
                next_vec_obs_rnd = vector_obs_rms.normalize(next_vector_obs) if NORMALIZE_OBS else next_vector_obs

                next_state_features_for_rnd = torch.cat(next_vis_features_rnd + [next_vec_obs_rnd], dim=-1)

                if NORMALIZE_OBS:
                    rnd_obs_rms.update(next_state_features_for_rnd)
                    next_state_features_for_rnd = rnd_obs_rms.normalize(next_state_features_for_rnd)
                
                target_feat, pred_feat = rnd_model(next_visual_obs_list, next_vector_obs)

                if NORMALIZE_OBS:
                    rnd_target_feature_rms.update(target_feat)
                    target_feat = rnd_target_feature_rms.normalize(target_feat)
                
                intrinsic_reward_tensor = F.mse_loss(pred_feat, target_feat, reduction='none').mean(dim=-1, keepdim=True)
                intrinsic_reward = intrinsic_reward_tensor.item() * RND_STRENGTH
        
        total_reward = reward + intrinsic_reward

        current_vis_obs_list_np = [vo.squeeze(0).cpu().numpy() for vo in current_visual_obs_list]
        current_vector_obs_np = current_vector_obs.squeeze(0).cpu().numpy()
        next_vis_obs_list_np = [vo.squeeze(0).cpu().numpy() for vo in next_visual_obs_list]
        next_vector_obs_np = next_vector_obs.squeeze(0).cpu().numpy()

        replay_buffer.add(current_vis_obs_list_np, current_vector_obs_np, action_np, total_reward, next_vis_obs_list_np, next_vector_obs_np, done)

        current_visual_obs_list = next_visual_obs_list
        current_vector_obs = next_vector_obs

        if NORMALIZE_OBS and not done:
            vector_obs_rms.update(current_vector_obs)
        
        if total_steps % SUMMARY_FREQ == 0:
            avg_steps_per_sec = step / (time.time() - start_time + 1e-6)
            with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
                f.write(f"Python: Episode: {episode_count}, Total Steps: {total_steps}, Episode Steps: {episode_steps}, Reward: {episode_reward_sum:.2f}, Alpha: {log_alpha.exp().item():.3f}, Speed: {avg_steps_per_sec:.2f} steps/s\n")
        
        if total_steps >= SAC_BUFFER_INIT_STEPS and len(replay_buffer) > SAC_BATCH_SIZE:
            if total_steps % STEPS_PER_UPDATE == 0:
                for _ in range(STEPS_PER_UPDATE):
                    batch_vis_obs_list, batch_vector_obs, batch_actions, batch_rewards, batch_next_vis_obs_list, batch_next_vector_obs, batch_dones = replay_buffer.sample(SAC_BATCH_SIZE)

                    if NORMALIZE_OBS:
                        batch_vector_obs = vector_obs_rms.normalize(batch_vector_obs)
                        batch_next_vector_obs = vector_obs_rms.normalize(batch_next_vector_obs)
                    
                    with torch.no_grad():
                        next_actions, next_log_pi = actor.sample(batch_next_vis_obs_list, batch_next_vector_obs)
                        q1_target_next = critic1_target(batch_next_vector_obs, batch_next_vector_obs, next_actions)
                        q2_target_next = critic2_target(batch_next_vector_obs, batch_next_vector_obs, next_actions)
                        q_target_next = torch.min(q1_target_next, q2_target_next)

                        target_q_value = batch_rewards + (1.0 - batch_dones) * SAC_GAMMA * (q_target_next - log_alpha.exp() * next_log_pi)
                    
                    q1_current = critic1(batch_vis_obs_list, batch_vector_obs, batch_actions)
                    q2_current = critic2(batch_vis_obs_list, batch_vector_obs, batch_actions)

                    critic1_loss = F.mse_loss(q1_current, target_q_value)
                    critic2_loss = F.mse_loss(q2_current, target_q_value)

                    critic1_optimizer.zero_grad()
                    critic1_loss.backward()
                    if GRAD_CLIP_NORM > 0:
                        torch.nn.utils.clip_grad_norm_(critic1.parameters(), GRAD_CLIP_NORM)
                    critic1_optimizer.step()

                    critic2_optimizer.zero_grad()
                    critic2_loss.backward()
                    if GRAD_CLIP_NORM > 0:
                        torch.nn.utils.clip_grad_norm_(critic2.parameters(), GRAD_CLIP_NORM)
                    critic2_optimizer.step()

                    for p in critic1.parameters(): p.requires_grad = False
                    for p in critic2.parameters(): p.requires_grad = False

                    new_actions, new_log_pi = actor.sample(batch_vis_obs_list, batch_vector_obs)
                    q1_new_actions = critic1(batch_vis_obs_list, batch_vector_obs, new_actions)
                    q2_new_actions = critic2(batch_vis_obs_list, batch_vector_obs, new_actions)
                    min_q_new_actions = torch.min(q1_new_actions, q2_new_actions)
                    actor_loss = (log_alpha.exp().detach() * new_log_pi - min_q_new_actions).mean()

                    actor_optimizer.zero_grad()
                    actor_loss.backward()
                    if GRAD_CLIP_NORM > 0:
                        torch.nn.utils.clip_grad_norm_(actor.parameters(), GRAD_CLIP_NORM)
                    actor_optimizer.step()

                    for p in critic1.parameters(): p.requires_grad = True
                    for p in critic2.parameters(): p.requires_grad = True

                    alpha_loss = -(log_alpha.exp() * (new_log_pi + target_entropy).detach()).mean()
                    alpha_optimizer.zero_grad()
                    alpha_loss.backward()
                    alpha_optimizer.step()

                    if RND_STRENGTH > 0:
                        rnd_next_vis_features = [enc(vo) for enc, vo in zip(rnd_model.visual_encoders, batch_next_vis_obs_list)]
                        rnd_next_vec_obs = batch_next_vector_obs

                        rnd_next_state_features = torch.cat(rnd_next_vis_features + [rnd_next_vec_obs], dim=-1)

                        if NORMALIZE_OBS:
                            rnd_next_state_features = rnd_obs_rms.normalize(rnd_next_state_features)
                        
                        rnd_target_feat_batch, rnd_pred_feat_batch = rnd_model(batch_next_vis_obs_list, batch_next_vector_obs)

                        if NORMALIZE_OBS:
                            rnd_target_feat_batch = rnd_target_feature_rms.normalize(rnd_target_feat_batch.detach())
                        
                        rnd_loss = F.mse_loss(rnd_pred_feat_batch, rnd_target_feat_batch.detach())
                        rnd_predictor_optimizer.zero_grad()
                        rnd_loss.backward()
                        if GRAD_CLIP_NORM > 0:
                            torch.nn.utils.clip_grad_norm_(rnd_model.parameters(), GRAD_CLIP_NORM)
                        rnd_predictor_optimizer.step()
                    
                    for target_param, local_param in zip(critic1_target.parameters(), critic1.parameters()):
                        target_param.data.copy_(SAC_TAU * local_param.data + (1.0 - SAC_TAU) * target_param.data)
                    for target_param, local_param in zip(critic2_target.parameters(), critic2.parameters()):
                        target_param.data.copy_(SAC_TAU * local_param.data + (1.0 - SAC_TAU) * target_param.data)
        if done:
            episode_count += 1
            avg_steps_per_sec = step / (time.time() - start_time + 1e-6)
            with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
                f.write(f"Python: Episode: {episode_count}, Total Steps: {total_steps}, Episode Steps: {episode_steps}, Reward: {episode_reward_sum:.2f}, Alpha: {log_alpha.exp().item():.3f}, Speed: {avg_steps_per_sec:.2f} steps/s\n")

            episode_reward_sum = 0
            episode_steps = 0

            for i in range(10):
                env.step()
        
        if total_steps % CHECKPOINT_FREQ == 0:
            with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
                f.write(f"Python: Saving models at step {total_steps}...\n")
            torch.save(actor.state_dict(), os.path.join(OUTPUT_DIR, f"actor_{total_steps}.pth"))
            torch.save(critic1.state_dict(), os.path.join(OUTPUT_DIR, f"critic1_{total_steps}.pth"))
    
    with open(OUTPUT_DIR+"/python_log.txt", "a") as f:
        f.write("Max training steps reached.\n")
    env.close()


if __name__ == "__main__":
    main()