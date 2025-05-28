import os
import subprocess

os.environ["CUDA_VISIBLE_DEVICES"] = ""
result = subprocess.run("mlagents-learn trainer/sac/sac_rdn_jason_config.yaml --run-id=sac_rdn_jason_test17 --env=env_build.x86_64 --no-graphics --num-envs 8", shell=True)