#!/usr/bin/env python3
"""Run real Godot GUI/input tests with production margin code, no game files."""
import argparse
import os
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--godot', default='godot')
parser.add_argument('--dotnet', default='dotnet')
args = parser.parse_args()
project = Path(__file__).resolve().parent / 'ScreenMarginsHarness'
cache = project / '.godot'
cache.mkdir(exist_ok=True)
(cache / 'harness.tscn').write_text('''[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://Harness.cs" id="1"]
[node name="Harness" type="Node"]
script = ExtResource("1")
''')
env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1')
subprocess.run([args.dotnet, 'build', str(project), '--nologo'], env=env, check=True)
subprocess.run([args.godot, '--headless', '--path', str(project)], env=env, check=True, timeout=30)
