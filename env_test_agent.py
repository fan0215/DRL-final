import numpy as np
import random
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
import cv2 # For image display
import time # For FPS control

def generate_random_actions(action_spec):
    """
    Generates random continuous actions based on the action spec.
    Our car agent has 3 continuous actions:
    - Action 0: Accelerator (0 to 1, though CarAgent clamps -1 to 1)
    - Action 1: Brake (0 to 1)
    - Action 2: Steering Input (-1 to 1)
    """
    accelerator = random.uniform(0.0, 1.0)  # Will be positive, car goes forward
    brake = random.uniform(0.0, 1.0)        # Changed from 0.5 to 1.0 for full range
    steering = random.uniform(-1.0, 1.0)

    # Ensure actions are within a NumPy array structure expected by mlagents
    # The shape should be (num_agents, num_continuous_actions)
    return np.array([[1.0, 0.0, steering]], dtype=np.float32)

def main():
    print("Python Random Car Agent starting...")

    # --- Environment Connection ---
    env_name = "env_build.x86_64"
    worker_id = 0 
    
    engine_config_channel = EngineConfigurationChannel()
    # Setting time_scale > 1 makes Unity run faster than real-time.
    # For visualization, time_scale=1.0 might feel more natural,
    # but 20.0 will show faster progress through episodes.
    engine_config_channel.set_configuration_parameters(time_scale=20.0) 

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

        print(f"Action Spec: {action_spec}")
        if not action_spec.is_continuous():
            print("Error: This random agent is designed for continuous actions.")
            return
        if action_spec.continuous_size != 3:
            print(f"Error: Expected 3 continuous actions, but got {action_spec.continuous_size}.")
            return

        # Identify visual observation indices from the observation specs
        # Visual observations are expected to have 3 dimensions (H, W, C)
        # Vector observations usually have 1 or 2 (e.g., (6,) or (1,6) after processing)
        vis_obs_indices = []
        print("Observation Specs:")
        for i, obs_spec in enumerate(behavior_spec.observation_specs):
            print(f"  Obs {i}: shape={obs_spec.shape}, type={obs_spec.observation_type.name}")
            if len(obs_spec.shape) == 3: # Standard for visual observations (H, W, C)
                vis_obs_indices.append(i)
        
        if len(vis_obs_indices) != 5:
            print(f"Warning: Expected 5 visual sensors, but found {len(vis_obs_indices)} based on shape.")
            # If you are sure about the order, you can manually set:
            # vis_obs_indices = [1, 2, 3, 4, 5] # If obs[0] is vector, then cams are 1-5
            # For now, we'll proceed with what was found.
        else:
            print(f"Found {len(vis_obs_indices)} visual observation streams at indices: {vis_obs_indices}")


        num_episodes = 100
        target_fps = 60.0
        target_frame_time = 1.0 / target_fps

        for episode in range(num_episodes):
            print(f"\nStarting Episode: {episode + 1}")
            env.reset()
            
            decision_steps, terminal_steps = env.get_steps(behavior_name)
            
            done = False
            episode_steps = 0
            episode_reward = 0.0

            while not done:
                frame_start_time = time.perf_counter()

                random_actions = generate_random_actions(action_spec)
                
                action_tuple = action_spec.empty_action(len(decision_steps))
                if len(decision_steps) > 0:
                    action_tuple.add_continuous(random_actions) 
                
                env.set_actions(behavior_name, action_tuple)
                env.step()

                new_decision_steps, new_terminal_steps = env.get_steps(behavior_name)

                # --- Render Observations ---
                obs_to_render_from = None
                agent_idx_in_step = 0 # Assuming single agent or render for the first one

                if len(new_decision_steps) > 0:
                    obs_to_render_from = new_decision_steps
                    # If multiple agents, ensure random_actions and agent_idx_in_step are handled.
                    # For a single agent, decision_steps.agent_id[0] gives its ID,
                    # and its data is at index 0 of the observation arrays.
                elif len(new_terminal_steps) > 0:
                    obs_to_render_from = new_terminal_steps
                
                if obs_to_render_from and len(vis_obs_indices) > 0:
                    agent_visual_obs_list = []
                    # obs_to_render_from.obs is a list of observations (vector, cam1, cam2, ...)
                    # Each element obs_to_render_from.obs[k] is a batch (num_agents, data...)
                    for obs_idx in vis_obs_indices:
                        if obs_idx < len(obs_to_render_from.obs):
                            # Get the specific camera observation for the first agent in the step
                            # Shape is (num_agents_in_step, H, W, C)
                            cam_obs_batch = obs_to_render_from.obs[obs_idx]
                            if cam_obs_batch.ndim == 4 and cam_obs_batch.shape[0] > agent_idx_in_step:
                                cam_img_normalized = cam_obs_batch[agent_idx_in_step] # Should be (H, W, C)
                                
                                # Convert from [0,1] float to [0,255] uint8
                                img_uint8 = (cam_img_normalized * 255).astype(np.uint8)
                                
                                # ML-Agents usually provides RGB. OpenCV imshow expects BGR.
                                if img_uint8.shape[2] == 3: # Color image
                                    img_bgr = cv2.cvtColor(img_uint8, cv2.COLOR_RGB2BGR)
                                elif img_uint8.shape[2] == 1: # Grayscale image
                                     # Convert to BGR to allow concatenation if other images are color
                                    img_bgr = cv2.cvtColor(img_uint8, cv2.COLOR_GRAY2BGR)
                                else:
                                    img_bgr = img_uint8 # Unknown format, display as is

                                # Resize if needed, though 84x84 is small. For visibility:
                                # img_bgr = cv2.resize(img_bgr, (168, 168), interpolation=cv2.INTER_NEAREST)
                                agent_visual_obs_list.append(img_bgr)
                        else:
                            print(f"Warning: Observation index {obs_idx} out of bounds for obs list.")
                    
                    if len(agent_visual_obs_list) == 5 : # Ensure we have all 5 images
                        try:
                            stitched_images = cv2.hconcat(agent_visual_obs_list)
                            cv2.imshow("Agent Cameras (5 views)", stitched_images)
                        except cv2.error as e:
                            print(f"OpenCV error during hconcat/imshow: {e}")
                            print(f"Number of images collected: {len(agent_visual_obs_list)}")
                            for i, img in enumerate(agent_visual_obs_list):
                                print(f"Img {i} shape: {img.shape}, dtype: {img.dtype}")


                if cv2.waitKey(1) & 0xFF == ord('q'):
                    print("Quit signal received. Exiting...")
                    done = True # To break outer loop as well
                    num_episodes = episode # To prevent further episodes
                    break 
                # --- End Render Observations ---

                if len(new_terminal_steps) > 0:
                    print(f"Episode {episode + 1} ended after {episode_steps + 1} steps.")
                    # print(f"  Terminal observation (vector): {new_terminal_steps.obs[0] if len(new_terminal_steps.obs)>0 else 'N/A'}")
                    # print(f"  Terminal reward: {new_terminal_steps.reward}")
                    episode_reward += new_terminal_steps.reward[0] 
                    done = True
                elif len(new_decision_steps) > 0:
                    decision_steps = new_decision_steps
                    episode_reward += decision_steps.reward[0]
                else:
                    print("No agents found in decision or terminal steps. Ending episode.")
                    done = True
                
                episode_steps += 1
                if episode_steps > 3000: 
                    print("Max steps reached for episode. Ending.")
                    done = True 
                    # No explicit env.reset() here, episode loop will handle it or main loop exits.

                # Frame rate control
                elapsed_time = time.perf_counter() - frame_start_time
                sleep_time = target_frame_time - elapsed_time
                if sleep_time > 0:
                    time.sleep(sleep_time)

            print(f"Total reward for episode {episode + 1}: {episode_reward:.4f}")
            if cv2.getWindowProperty("Agent Cameras (5 views)", cv2.WND_PROP_VISIBLE) < 1 and episode > 0 : # Check if window was closed
                print("Display window was closed. Exiting.")
                break


    except Exception as e:
        print(f"An error occurred: {e}")
        import traceback
        traceback.print_exc()
    finally:
        if 'env' in locals() and env is not None:
            env.close()
            print("Unity Environment closed.")
        cv2.destroyAllWindows() # Close OpenCV windows

if __name__ == '__main__':
    # Before running, ensure you have OpenCV installed:
    # pip install opencv-python mlagents
    main()