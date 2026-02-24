namespace CodexSix.UiKit.Runtime
{
    public interface IInputBlockPolicy
    {
        bool ShouldBlockGameplayInput(UiFocusState focusState, int modalDepth);
    }
}