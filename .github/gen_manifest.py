#!/usr/bin/env python3
"""Write _site/manifest.json (the Jellyfin plugin repository served via GitHub Pages)."""
import sys
import os
import json
import datetime

ver, md5, zipname, repo, tag = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5]
source_url = "https://github.com/{0}/releases/download/{1}/{2}".format(repo, tag, zipname)
timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000Z")

manifest = [
    {
        "guid": "a8c4e1f0-3b2d-4e6a-9c7b-1f2e3d4c5b6a",
        "name": "Video Previews",
        "description": "Pre-generated hover video previews per library.",
        "overview": "Smooth pre-generated hover previews.",
        "owner": "Blackspell01",
        "category": "General",
        "imageUrl": "",
        "versions": [
            {
                "version": ver,
                "changelog": "Release {0}".format(ver),
                "targetAbi": "10.11.0.0",
                "sourceUrl": source_url,
                "checksum": md5,
                "timestamp": timestamp,
            }
        ],
    }
]
os.makedirs("_site", exist_ok=True)
with open("_site/manifest.json", "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2)
print("manifest.json written:", source_url, md5)
