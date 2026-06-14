#!/usr/bin/env python3
"""Write pkg/meta.json (the plugin metadata embedded in the release zip)."""
import sys
import os
import json

ver = sys.argv[1]
meta = {
    "category": "General",
    "changelog": "",
    "description": "Pre-generated hover video previews per library.",
    "guid": "a8c4e1f0-3b2d-4e6a-9c7b-1f2e3d4c5b6a",
    "name": "Video Previews",
    "overview": "Smooth pre-generated hover previews.",
    "owner": "Blackspell01",
    "targetAbi": "10.11.0.0",
    "framework": "net9.0",
    "version": ver,
    "status": "Active",
    "autoUpdate": True,
    "imagePath": "",
}
os.makedirs("pkg", exist_ok=True)
with open("pkg/meta.json", "w", encoding="utf-8") as f:
    json.dump(meta, f, indent=2)
print("meta.json written for", ver)
