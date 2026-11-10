# NeuroCompanion — Teammate Setup Guide

> Follow these steps **in order** to get the full project running on your machine, including the Unity MCP connection that lets Antigravity control Unity Editor.

---

## Step 1 — Install Unity Hub

Download and install Unity Hub from:  
👉 https://unity.com/download

---

## Step 2 — Install Exact Unity Version

> [!IMPORTANT]
> You must use **exactly Unity 6000.3.28f1** (Unity 6.3 LTS). A different version will cause errors.

1. Open Unity Hub
2. Go to **Installs → Install Editor**
3. Search for `6000.3.28f1` and install it
4. When prompted for modules, the defaults are fine (no need for Visual Studio)

---

## Step 3 — Clone the Repository

```powershell
git clone https://github.com/aleezaprojects123-rgb/Neuro-companion.git
cd Neuro-companion
```

---

## Step 4 — Install Unity CLI

The Unity CLI is needed for the MCP connection. Run this once in PowerShell:

```powershell
irm https://unity.com/install.ps1 | iex
```

Then **close and reopen PowerShell** so `unity` is on your PATH.  
Verify with:

```powershell
unity --version
```

---

## Step 5 — Open the Unity Project

1. Open Unity Hub
2. Click **Add → Add project from disk**
3. Navigate to: `<where-you-cloned>\Neuro-companion\Unity\NeuroCompanion`
4. Select that folder and click **Add Project**
5. Click **Open** — Unity will import packages automatically
6. If prompted to upgrade URP materials → click **Yes / Proceed**
7. Wait for the import to finish (may take a few minutes the first time)

> The `com.unity.pipeline` MCP package is already in `Packages/manifest.json` — Unity installs it automatically.

---

## Step 6 — Run the MCP Setup Script (One Command!)

This is the step that configures Antigravity to talk to Unity.  
**Keep Unity Editor open**, then run this in PowerShell from the repo root:

```powershell
.\setup\setup_unity_mcp.ps1
```

The script will:
- Find your Unity CLI automatically
- Detect the project path automatically  
- Write the MCP config to `~\.gemini\config\mcp_config.json`
- Confirm everything is correct

**If your Unity project is in a custom location**, pass the path:

```powershell
.\setup\setup_unity_mcp.ps1 -ProjectPath "C:\YourPath\Unity\NeuroCompanion"
```

---

## Step 7 — Restart Antigravity

1. Fully close Antigravity
2. Reopen Antigravity
3. The Unity MCP tools are now active

---

## Step 8 — Verify the Connection

In Antigravity, type:
> *"List the available Unity MCP tools"*

You should see tools like `editor_play`, `create_gameobject`, `get_hierarchy`, etc.

---

## ✅ You're Done!

| What you got from GitHub | How |
|--------------------------|-----|
| All backend/ML code | `Backend/` folder |
| Unity project files | `Unity/NeuroCompanion/` |
| Unity MCP package | Auto-installed by Unity via `manifest.json` |
| Docs | `Docs/` folder |

| What the setup script did | Result |
|---------------------------|--------|
| Configured Antigravity MCP | `~\.gemini\config\mcp_config.json` |
| Linked to your local Unity project | Auto-detected path |

---

## Troubleshooting

**Unity Editor not connecting to MCP?**
- Make sure Unity Editor is **open** before using MCP in Antigravity
- Check the Unity Console for errors about `com.unity.pipeline`

**Script says Unity CLI not found?**
- Run `irm https://unity.com/install.ps1 | iex` again
- Open a **new** PowerShell window and retry

**Wrong Unity version warning?**
- Must be `6000.3.28f1` exactly — reinstall via Unity Hub if needed

**Setup script can't find the project path?**
- Pass it manually: `.\setup\setup_unity_mcp.ps1 -ProjectPath "your\path\here"`
