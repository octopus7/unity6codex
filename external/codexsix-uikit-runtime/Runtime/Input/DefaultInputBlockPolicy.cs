namespace CodexSix.UiKit.Runtime
{
    public sealed class DefaultInputBlockPolicy : IInputBlockPolicy
    {
        public bool ShouldBlockGameplayInput(UiFocusState focusState, int modalDepth)
        {
            return modalDepth > 0 || focusState == UiFocusState.TextInput;
        }
    }
}