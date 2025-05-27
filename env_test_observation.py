import numpy as np
import random
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple
import cv2 # For image saving
import os # For creating output directory

def generate_dummy_actions(action_spec):
    """
    Generates dummy continuous actions.
    """
    accelerator = 0.1
    brake = 0.0
    steering = 0.0
    return np.array([[accelerator, brake, steering]], dtype=np.float32)

def main():
    print("Python Agent: Save Single Step Observations starting...")

    # --- Environment Connection ---
    # As per your log, you are using a build.
    env_name = "env_build.x86_64" # Make sure this path is correct or set to None if using Editor
    worker_id = 0 
    
    output_dir = "saved_observations"
    os.makedirs(output_dir, exist_ok=True)
    print(f"Observations will be saved in '{output_dir}/' directory.")

    engine_config_channel = EngineConfigurationChannel()
    engine_config_channel.set_configuration_parameters(time_scale=1.0) 

    try:
        if env_name:
            print(f"Attempting to connect to Unity build: {env_name}")
            env = UnityEnvironment(file_name=env_name, worker_id=worker_id, side_channels=[engine_config_channel])
        else:
            print("Attempting to connect to running Unity Editor...")
            env = UnityEnvironment(worker_id=worker_id, side_channels=[engine_config_channel])
        
        print("Connected to Unity Environment.")

        env.reset()
        behavior_name = list(env.behavior_specs.keys())[0]
        print(f"Using behavior name: {behavior_name}")
        
        behavior_spec = env.behavior_specs[behavior_name]
        action_spec = behavior_spec.action_spec

        if not action_spec.is_continuous() or action_spec.continuous_size != 3:
            print("Error: Action spec not as expected (3 continuous actions).")
            return

        vis_obs_indices = []
        print("Observation Specs:")
        for i, obs_spec in enumerate(behavior_spec.observation_specs):
            print(f"  Obs {i}: name='{obs_spec.name}', shape={obs_spec.shape}, type={obs_spec.observation_type.name}")
            # Assuming visual observations are those with 3 dimensions (C,H,W or H,W,C)
            # or 2 dimensions (H,W for grayscale)
            if len(obs_spec.shape) == 3 or (len(obs_spec.shape) == 2 and obs_spec.shape[0] > 1 and obs_spec.shape[1] > 1):
                vis_obs_indices.append(i)
        
        if not vis_obs_indices:
            print("Error: No visual observations identified based on shape.")
            return
        # Your log shows 5 cameras are obs 0-4, and obs 5 is vector.
        # We'll filter to ensure we only take what looks like images from the spec.
        # The log showed camera shapes like (1,84,84) which have len == 3.
        print(f"Identified {len(vis_obs_indices)} potential visual observation streams at indices: {vis_obs_indices}")

        decision_steps, _ = env.get_steps(behavior_name)
        
        if not decision_steps:
            print("No agents found in initial decision_steps. Attempting one step...")
            env.step() 
            decision_steps, _ = env.get_steps(behavior_name)
            if not decision_steps:
                 print("Still no agents after an extra step. Exiting. Check Unity scene.")
                 return

        dummy_actions_np = generate_dummy_actions(action_spec)
        
        num_agents_in_step = len(decision_steps)
        if num_agents_in_step == 0:
            print("No agents available to send actions to. Exiting.")
            return
            
        action_tuple = ActionTuple(continuous=np.tile(dummy_actions_np, (num_agents_in_step, 1)))

        print("Setting actions and taking one step...")
        env.set_actions(behavior_name, action_tuple)
        env.step()

        new_decision_steps, new_terminal_steps = env.get_steps(behavior_name)

        obs_to_process_from = None
        agent_idx_in_step = 0 

        if len(new_decision_steps) > 0:
            obs_to_process_from = new_decision_steps
            print(f"Processing observations from DecisionSteps (agent_id: {obs_to_process_from.agent_id[agent_idx_in_step]})")
        elif len(new_terminal_steps) > 0:
            obs_to_process_from = new_terminal_steps
            print(f"Processing observations from TerminalSteps (agent_id: {obs_to_process_from.agent_id[agent_idx_in_step]})")
        else:
            print("No agents found in decision or terminal steps after the action. Cannot save observations.")
            return

        saved_count = 0
        # Iterate through the identified visual observation indices
        for i, obs_idx in enumerate(vis_obs_indices):
            # Double check if this obs_idx is still valid for the current obs_to_process_from.obs list
            if obs_idx >= len(obs_to_process_from.obs):
                print(f"Warning: obs_idx {obs_idx} is out of bounds for current observation list. Skipping.")
                continue

            current_obs_spec = behavior_spec.observation_specs[obs_idx]
            obs_shape_from_spec = current_obs_spec.shape # e.g., (1, 84, 84) or (84, 84, 3)

            cam_obs_batch = obs_to_process_from.obs[obs_idx]
            
            if cam_obs_batch.ndim == len(obs_shape_from_spec) + 1 and cam_obs_batch.shape[0] > agent_idx_in_step:
                # cam_img_normalized has shape from spec, e.g. (1,84,84) or (84,84,3)
                cam_img_normalized = cam_obs_batch[agent_idx_in_step] 
                img_uint8 = (cam_img_normalized * 255).astype(np.uint8)
                
                img_to_save_for_cv = None

                if len(obs_shape_from_spec) == 3:
                    # Case 1: Grayscale CHW (e.g., (1, H, W)) - Matches your log
                    if obs_shape_from_spec[0] == 1:
                        img_to_save_for_cv = img_uint8.squeeze(axis=0) # Shape becomes (H, W)
                        print(f"  Processed sensor '{current_obs_spec.name}' as grayscale (H,W) from CHW shape {obs_shape_from_spec}. New shape: {img_to_save_for_cv.shape}")
                    # Case 2: Color HWC (e.g., (H, W, 3))
                    elif obs_shape_from_spec[2] == 3:
                        img_to_save_for_cv = cv2.cvtColor(img_uint8, cv2.COLOR_RGB2BGR)
                        print(f"  Processed sensor '{current_obs_spec.name}' as color (H,W,C) from HWC shape {obs_shape_from_spec}. New shape: {img_to_save_for_cv.shape}")
                    # Case 3: Grayscale HWC (e.g., (H, W, 1))
                    elif obs_shape_from_spec[2] == 1:
                        img_to_save_for_cv = img_uint8 # Shape (H,W,1) is fine
                        print(f"  Processed sensor '{current_obs_spec.name}' as grayscale (H,W,1) from HWC shape {obs_shape_from_spec}. New shape: {img_to_save_for_cv.shape}")
                    else:
                        print(f"  Warning: Sensor '{current_obs_spec.name}' (obs_idx {obs_idx}) has an unhandled 3D shape: {obs_shape_from_spec}. Skipping.")
                        continue
                elif len(obs_shape_from_spec) == 2: # Grayscale HW (e.g. (H,W))
                    img_to_save_for_cv = img_uint8
                    print(f"  Processed sensor '{current_obs_spec.name}' as 2D (H,W) from shape {obs_shape_from_spec}. New shape: {img_to_save_for_cv.shape}")
                else:
                    print(f"  Warning: Sensor '{current_obs_spec.name}' (obs_idx {obs_idx}) has an unsupported shape: {obs_shape_from_spec}. Skipping.")
                    continue
                
                if img_to_save_for_cv is not None:
                    obs_spec_name = current_obs_spec.name
                    if obs_spec_name:
                        # Sanitize common problematic characters for filenames
                        filename_base = obs_spec_name.replace(" ", "_").replace("=", "_").replace("?", "_").replace(":", "_").lower()
                    else:
                        filename_base = f"camera_sensor_{obs_idx}"
                    
                    file_path = os.path.join(output_dir, f"{filename_base}_observation.png")
                    
                    try:
                        cv2.imwrite(file_path, img_to_save_for_cv)
                        print(f"  Saved: {file_path}")
                        saved_count += 1
                    except Exception as e:
                        print(f"  Error saving image {file_path}: {e}")
                else:
                    print(f"  Error: img_to_save_for_cv is None for sensor '{current_obs_spec.name}'. This implies a logic error.")
            else:
                print(f"Warning: Visual observation for sensor '{current_obs_spec.name}' (obs_idx {obs_idx}) has unexpected batch dimensions or no data for agent {agent_idx_in_step}.")
                print(f"  Expected batch dim + spec_dims: {len(obs_shape_from_spec) + 1}, Got ndim: {cam_obs_batch.ndim}. Batch shape: {cam_obs_batch.shape}")

        if saved_count == 0:
            print("No images were saved. Please check Unity setup, observation specs, and log messages.")
        elif saved_count < len(vis_obs_indices):
             print(f"Warning: Only {saved_count} out of {len(vis_obs_indices)} identified visual observations were saved.")

    except Exception as e:
        print(f"An error occurred: {e}")
        import traceback
        traceback.print_exc()
    finally:
        if 'env' in locals() and env is not None:
            env.close()
            print("Unity Environment closed.")
        print("Script finished.")

if __name__ == '__main__':
    main()