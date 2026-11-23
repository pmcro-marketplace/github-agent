#!/usr/bin/env python3
"""Project a managed .github configuration from authoritative agent/skill sources."""
import argparse
import json
from pathlib import Path
import shutil
import hashlib

root = Path(__file__).resolve().parents[5]
plugin = root / "plugins/github"
parser = argparse.ArgumentParser()
parser.add_argument("--target", type=Path, default=root/"dist/github/.github")
target = parser.parse_args().target
if target.name != ".github" or target.exists() or any(p.is_symlink() for p in (target,*target.parents)):
    raise SystemExit("Fresh non-symlink .github target required")
target.mkdir(parents=True)
shutil.copytree(plugin/"agents",target/"agents")
shutil.copytree(plugin/"skills",target/"skills",ignore=shutil.ignore_patterns("__pycache__","bin","obj"))
registry = json.loads((plugin/"skills/github/assets/native-capabilities.json").read_text())
registry["definitionSha256"]=hashlib.sha256((target/"agents/github-agent.agent.md").read_bytes()).hexdigest()
registry["projectionOnly"]=True
(target/"profile.json").write_text(json.dumps(registry,indent=2)+"\n")
for skill in (target/"skills").iterdir():
    if not (skill/"SKILL.md").is_file() or not all(any(p.is_file() for p in (skill/d).rglob("*")) for d in ("scripts","references","assets")):
        raise SystemExit("Incomplete skill: "+skill.name)
print(json.dumps({"status":"PASS","skills":len(list((target/"skills").iterdir())),"profile":str(target.relative_to(root)) if target.is_relative_to(root) else str(target),"nativeInstall":"NOT_INVOKED"}))
