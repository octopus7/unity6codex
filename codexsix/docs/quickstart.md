# Quickstart

## 1) Run server

From repository root:

```powershell
cd server
dotnet run --project TopdownShooter.Server
```

Optional arguments:

```powershell
dotnet run --project TopdownShooter.Server -- --ip 0.0.0.0 --port 7777 --max-players 8
```

Server console commands:
- `status`
- `kick <playerId>`
- `quit`

Or from Unity Editor (Windows):
- `Tools > Server > Start Local Server`
- It checks if the listen port is already in use and starts only when no server is running.

Headless monitor from Unity Editor (Windows):
- `Tools > Server > Open Headless Console Monitor`
- Set `Bot Count` (1~512), then click `Start All`.
- Server/Bot logs are shown side-by-side inside the Editor window.
- In this headless mode, separate external console windows are not opened.

## 2) Bootstrap Unity scene

In Unity Editor:
- Open project `codexsix`
- Menu: `Tools > TopDownShooter > Bootstrap MVP Scene (Safe)`

This creates:
- `Assets/Scenes/MainScene.unity`
- Runtime objects (transport/client/input/HUD)
- Environment, obstacles, portals, shop zone
- Build Settings scene registration

Destructive full reset (with backup):
- `Tools > TopDownShooter > Bootstrap MVP Scene (Destructive Reset)`
- Policy: available only for initial scene creation (before `MainScene.unity` exists).

## 3) Play

- Enter host/port/nickname in the on-screen debug window.
- Click `Connect`.
- Move: `WASD`
- Aim: mouse cursor
- Fire: left mouse
- Buy in shop: stand on item, click purchase button in debug window.
