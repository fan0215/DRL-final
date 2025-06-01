import numpy as np
import matplotlib.pyplot as plt
import re

current_episode = -1
lst = []

with open("results/sac_rdn_jason_3/python_log.txt", 'r') as f:
    for line in f:
        match = re.search(r"Episode: (\d+).*Reward: ([-+]?\d+\.\d+)", line)
        if match:
            ep = int(match.group(1))
            reward = float(match.group(2))
            if ep != current_episode:
                lst.append(reward)
                current_episode = ep
            else:
                lst[current_episode] = reward

plt.figure(figsize=(12, 6))
plt.plot(lst)
plt.title('Training Learning Curve (Reward per Episode)')
plt.xlabel('Episode')
plt.ylabel('Reward')
plt.grid(True)
plt.tight_layout()
plt.show()