# AGENTS.md

## Unity Bootstrap Safety Policy

- Default rule: use `Tools > TopDownShooter > Bootstrap MVP Scene (Safe)` for scene updates.
- `Bootstrap MVP Scene (Destructive Reset)` is allowed only for initial scene creation, when `MainScene.unity` does not exist.
- After initial creation, do not use destructive mode unless the user explicitly approves an exception.
- Bootstrap regeneration scope must stay inside `__BootstrapGenerated`.
- Keep manual/custom scene content outside `__BootstrapGenerated`.
- For destructive operations, create a backup scene first in `Assets/Scenes/Backups/`.

## Execution Guardrails For Agents

- Before destructive scene or asset changes, state the affected scope.
- Do not perform destructive work unless the user explicitly requested it.
- If a safe path exists, always prefer and suggest the safe path first.
