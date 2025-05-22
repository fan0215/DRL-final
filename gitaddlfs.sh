find . -type f -size +50M -exec git lfs track "{}" \;
git add .gitattributes
git add .