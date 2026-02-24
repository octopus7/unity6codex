# Extension Guide

## Custom screen rendering
`UiToolkitPresenter` is intentionally minimal.
For project-specific visuals, create a presenter that:
1. Uses `UiRoot` layer references.
2. Subscribes to `UiContext` services.
3. Binds project-specific UXML/USS assets.

## Custom input policy
Implement `IInputBlockPolicy`:
```csharp
public sealed class CombatInputBlockPolicy : IInputBlockPolicy
{
    public bool ShouldBlockGameplayInput(UiFocusState focusState, int modalDepth)
    {
        return modalDepth > 0;
    }
}
```

## Additional channels
Use `UiStateChannel<T>` per domain slice and publish typed signals via `UiSignalBus`.

## Persistent overlays
Use `UiToastChannels.Persistent` for long-lived overlay notifications across scene-scoped contexts.