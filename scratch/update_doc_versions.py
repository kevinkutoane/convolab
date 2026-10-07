import os
import re

files_to_update = [
    "docs/project/ROADMAP.md",
    "docs/project/README.md",
    "docs/Architecture/ProductReadinessAssessment.md",
    "docs/Architecture/README.md",
    "docs/PlatformManifest.md"
]

for file in files_to_update:
    filepath = os.path.join(r"c:\Users\W1022804\convolab-main", file)
    if not os.path.exists(filepath):
        print(f"Skipping {filepath} (does not exist)")
        continue
    
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
    
    if file == "docs/project/ROADMAP.md":
        content = re.sub(
            r"Latest formal product release: `v1\.0\.0-alpha\.18` \(commit `073152a40fe81cb3ea3669eeb512d345f6032a4b`\)\.\s*"
            r"Completed operational milestone: `alpha\.19 — Live Environment Validation & Load Testing` \(tagged `v1\.0\.0-alpha\.19`, formally closed on `main`\)\.\s*"
            r"Current development milestone: `alpha\.20 — Environment & Secret Management` \(next planned milestone\)\.",
            "Current release candidate: `v1.0.0-enterprise — Release Candidate` (four strategic pillars delivered).\n"
            "Previous formal release: `v1.0.0-alpha.18` (commit `073152a40fe81cb3ea3669eeb512d345f6032a4b`).  \n"
            "Historical operational milestone: `alpha.19 — Live Environment Validation & Load Testing` (tagged `v1.0.0-alpha.19`, formally closed on `main`).",
            content, count=1)

    elif file == "docs/project/README.md":
        content = re.sub(
            r"- \*\*Latest formal product release:\*\* `v1\.0\.0-alpha\.18` \(commit `073152a40fe81cb3ea3669eeb512d345f6032a4b`\)\n"
            r"- \*\*Completed operational milestone:\*\* `alpha\.19 — Live Environment Validation & Load Testing` \(tagged `v1\.0\.0-alpha\.19`\)\n"
            r"- \*\*Current development milestone:\*\* `alpha\.20 — Environment & Secret Management`",
            "- **Current release candidate:** `v1.0.0-enterprise — Release Candidate`\n"
            "- **Previous formal release:** `v1.0.0-alpha.18` (commit `073152a40fe81cb3ea3669eeb512d345f6032a4b`)\n"
            "- **Historical operational milestone:** `alpha.19 — Live Environment Validation & Load Testing` (tagged `v1.0.0-alpha.19`)",
            content, count=1)
        content = content.replace(
            "The latest formal product release is `v1.0.0-alpha.18`. Milestone Alpha.19 establishes operational validation and endurance baselines, and is formally closed on `main`.",
            "The current release candidate is `v1.0.0-enterprise`. The previous formal product release was `v1.0.0-alpha.18`. Milestone Alpha.19 establishes operational validation and endurance baselines, and is formally closed on `main`.",
            1)

    elif file == "docs/Architecture/ProductReadinessAssessment.md":
        content = content.replace(
            "The functional Studio baseline is stabilized at `v1.0.0-alpha.18` formal product release, with operational validation milestone `Alpha.19` formally closed",
            "The functional Studio baseline is stabilized at `v1.0.0-enterprise — Release Candidate`, with the previous formal release being `v1.0.0-alpha.18` and operational validation milestone `Alpha.19` formally closed",
            1)

    elif file == "docs/Architecture/README.md":
        content = content.replace(
            "Platform Core and ConvoLab Studio `v1.0.0-alpha.18` (formal release baseline, with operational validation milestone `Alpha.19` formally closed and next planned milestone `Alpha.20 — Environment & Secret Management`)",
            "Platform Core and ConvoLab Studio `v1.0.0-enterprise — Release Candidate` (with previous formal release baseline `v1.0.0-alpha.18` and historical operational milestone `Alpha.19` formally closed)",
            1)
            
    elif file == "docs/PlatformManifest.md":
        content = content.replace(
            "Platform Core and Studio are at `v1.0.0-alpha.18`.",
            "Platform Core and Studio are at `v1.0.0-enterprise — Release Candidate`.")
    
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
        print(f"Updated {filepath}")
