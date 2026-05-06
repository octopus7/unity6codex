namespace BeltScroll
{
    public enum CharacterBaseMotion
    {
        Idle,
        Walk,
        Run
    }

    public enum CharacterMotionState
    {
        Idle,
        IdleToWalk,
        Walk,
        WalkToIdle,
        WalkToRun,
        Run,
        RunToWalk,
        IdleToRun,
        RunToIdle
    }

    public static class CharacterMotionStateUtility
    {
        public static CharacterMotionState ToState(this CharacterBaseMotion baseMotion)
        {
            return baseMotion switch
            {
                CharacterBaseMotion.Walk => CharacterMotionState.Walk,
                CharacterBaseMotion.Run => CharacterMotionState.Run,
                _ => CharacterMotionState.Idle
            };
        }

        public static CharacterBaseMotion ToBaseMotion(this CharacterMotionState state)
        {
            return state switch
            {
                CharacterMotionState.Walk or CharacterMotionState.WalkToIdle or CharacterMotionState.WalkToRun => CharacterBaseMotion.Walk,
                CharacterMotionState.Run or CharacterMotionState.RunToWalk or CharacterMotionState.RunToIdle => CharacterBaseMotion.Run,
                _ => CharacterBaseMotion.Idle
            };
        }

        public static bool IsBaseState(this CharacterMotionState state)
        {
            return state is CharacterMotionState.Idle or CharacterMotionState.Walk or CharacterMotionState.Run;
        }

        public static CharacterMotionState ResolveTransition(CharacterBaseMotion from, CharacterBaseMotion to)
        {
            if (from == to)
            {
                return to.ToState();
            }

            return (from, to) switch
            {
                (CharacterBaseMotion.Idle, CharacterBaseMotion.Walk) => CharacterMotionState.IdleToWalk,
                (CharacterBaseMotion.Walk, CharacterBaseMotion.Idle) => CharacterMotionState.WalkToIdle,
                (CharacterBaseMotion.Walk, CharacterBaseMotion.Run) => CharacterMotionState.WalkToRun,
                (CharacterBaseMotion.Run, CharacterBaseMotion.Walk) => CharacterMotionState.RunToWalk,
                (CharacterBaseMotion.Idle, CharacterBaseMotion.Run) => CharacterMotionState.IdleToRun,
                (CharacterBaseMotion.Run, CharacterBaseMotion.Idle) => CharacterMotionState.RunToIdle,
                _ => to.ToState()
            };
        }
    }
}
