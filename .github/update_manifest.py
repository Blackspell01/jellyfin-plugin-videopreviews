#!/usr/bin/env python3
"""Update manifest.json (Jellyfin plugin repository) with a newly built release zip."""
import sys
import os
import json
import hashlib
import datetime

zip_path, ver, repo, tag = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]

GUID = "a8c4e1f0-3b2d-4e6a-9c7b-1f2e3d4c5b6a"
TARGET_ABI = "10.11.0.0"

with open(zip_path, "rb") as f:
    md5 = hashlib.md5(f.read()).hexdigest()

filename = os.path.basename(zip_path)
source_url = "https://github.com/{0}/releases/download/{1}/{2}".format(repo, tag, filename)
timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000Z")

entry = {
    "version": ver,
    "changelog": "Release {0}".format(ver),
    "targetAbi": TARGET_ABI,
    "sourceUrl": source_url,
    "checksum": md5,
    "timestamp": timestamp,
}

manifest = []
if os.path.exists("manifest.json"):
    with open("manifest.json", "r", encoding="utf-8") as f:
        manifest = json.load(f)

plugin = next((p for p in manifest if p.get("guid") == GUID), None)
if plugin is None:
    plugin = {
        "guid": GUID,
        "name": "Video Previews",
        "description": "Pre-generated hover video previews per library.",
        "overview": "Smooth pre-generated hover previews (N x Ts montage).",
        "owner": "Blackspell01",
        "category": "General",
        "imageUrl": "",
        "versions": [],
    }
    manifest.append(plugin)

plugin["versions"] = [v for v in plugin.get("versions", []) if v.get("version") != ver]
plugin["versions"].insert(0, entry)

with open("manifest.json", "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2)

print("manifest.json updated:", source_url, md5)
