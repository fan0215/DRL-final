import numpy as np
import random
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

def generate_random_actions(action_spec):
    """
    Generates random continuous actions based on the action spec.
    Our car agent has 3 continuous actions:
    - Action 0: Accelerator (0 to 1)
    - Action 1: Brake (0 to 1)
    - Action 2: Steering Input (-1 to 1)
    """
    accelerator = random.uniform(0.0, 1.0)
    brake = random.uniform(0.0, 0.5)
    steering = random.uniform(-1.0, 1.0)

    # Ensure actions are within a NumPy array structure expected by mlagents
    # The shape should be (num_agents, num_continuous_actions)
    # For a single agent, it's (1, 3)
    return np.array([[1.0, 0.0, 1.0]], dtype=np.float32)
    # return np.array([[accelerator, brake, steering]], dtype=np.float32)

def main():
    print("Python Random Car Agent starting...")

    # --- Environment Connection ---
    # If you have a built Unity environment, provide the path to the executable.
    # env_name = "path/to/your/UnityBuiltEnvironment"
    # If running from the Unity Editor, set env_name=None (or leave it out).
    env_name = None 
    # You might need to specify worker_id if running multiple instances or if default doesn't work
    worker_id = 0 
    
    # Channel to configure engine settings (optional, but good practice for faster simulation during training)
    engine_config_channel = EngineConfigurationChannel()
    engine_config_channel.set_configuration_parameters(time_scale=20.0) # Example: run simulation 20x faster

    try:
        if env_name:
            print(f"Attempting to connect to Unity build: {env_name}")
            env = UnityEnvironment(file_name=env_name, worker_id=worker_id, side_channels=[engine_config_channel])
        else:
            print("Attempting to connect to running Unity Editor...")
            env = UnityEnvironment(worker_id=worker_id, side_channels=[engine_config_channel])
        
        print("Connected to Unity Environment.")

        # --- Behavior Name ---
        # This must match the "Behavior Name" you set in the Behavior Parameters component in Unity
        # For example: "CarRacerBrain"
        # The script will try to get the first available behavior name if not specified,
        # but it's better to know it.
        
        env.reset() # Initialize the environment
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
            print("Action space should be: [Accelerator (0-1), Brake (0-1), Steering (-1 to 1)]")
            return

        num_episodes = 100
        for episode in range(num_episodes):
            print(f"\nStarting Episode: {episode + 1}")
            env.reset() # Reset environment for a new episode
            
            # Get initial decision steps and terminal steps
            decision_steps, terminal_steps = env.get_steps(behavior_name)
            
            done = False
            episode_steps = 0
            episode_reward = 0.0

            while not done:
                # Generate random actions
                random_actions = generate_random_actions(action_spec)
                
                # Apply actions to all agents (usually just one for this setup)
                # Create an ActionTuple
                action_tuple = action_spec.empty_action(len(decision_steps)) # Create action structure for N agents
                if len(decision_steps) > 0: # If there are agents needing decisions
                    action_tuple.add_continuous(random_actions) 
                
                env.set_actions(behavior_name, action_tuple)
                env.step() # Move the simulation forward

                # Get new state
                new_decision_steps, new_terminal_steps = env.get_steps(behavior_name)

                if len(new_terminal_steps) > 0: # Episode ended for one or more agents
                    done = True
                    print(f"Episode {episode + 1} ended after {episode_steps + 1} steps.")
                    print(f"  Terminal observation: {new_terminal_steps.obs}")
                    print(f"  Terminal reward: {new_terminal_steps.reward}")
                    episode_reward += new_terminal_steps.reward[0] # Assuming single agent
                elif len(new_decision_steps) > 0: # Episode continues
                    decision_steps = new_decision_steps
                    # print(f"  Step reward: {decision_steps.reward}")
                    episode_reward += decision_steps.reward[0] # Assuming single agent
                else: # No agents found, perhaps environment closed or issue
                    print("No agents found in decision or terminal steps. Ending loop.")
                    done = True
                
                episode_steps += 1
                if episode_steps > 3000: # Max steps per episode to prevent infinite loops
                    print("Max steps reached for episode. Ending.")
                    done = True # Force end episode
                    env.reset() # Make sure to reset to properly terminate on Unity side if max_steps is hit here

            print(f"Total reward for episode {episode + 1}: {episode_reward:.4f}")

    except Exception as e:
        print(f"An error occurred: {e}")
    finally:
        if 'env' in locals() and env is not None:
            env.close()
            print("Unity Environment closed.")

if __name__ == '__main__':
    main()