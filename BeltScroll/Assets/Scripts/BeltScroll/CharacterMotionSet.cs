using System;
using UnityEngine;

namespace BeltScroll
{
    [CreateAssetMenu(fileName = "CharacterMotionSet", menuName = "Belt Scroll/Character Motion Set")]
    public sealed class CharacterMotionSet : ScriptableObject
    {
        public Sprite fallbackSprite;

        public CharacterMotionClip idle = CharacterMotionClip.Looping(CharacterMotionState.Idle, 6f);
        public bool immediateIdleWalkTransitions;
        public CharacterMotionClip idleToWalk = CharacterMotionClip.Transition(CharacterMotionState.IdleToWalk, 0.12f);
        public CharacterMotionClip walk = CharacterMotionClip.Looping(CharacterMotionState.Walk, 10f);
        public CharacterMotionClip walkToIdle = CharacterMotionClip.Transition(CharacterMotionState.WalkToIdle, 0.1f);
        public CharacterMotionClip walkToRun = CharacterMotionClip.Transition(CharacterMotionState.WalkToRun, 0.1f);
        public CharacterMotionClip run = CharacterMotionClip.Looping(CharacterMotionState.Run, 14f);
        public CharacterMotionClip runToWalk = CharacterMotionClip.Transition(CharacterMotionState.RunToWalk, 0.1f);
        public CharacterMotionClip idleToRun = CharacterMotionClip.Transition(CharacterMotionState.IdleToRun, 0.16f);
        public CharacterMotionClip runToIdle = CharacterMotionClip.Transition(CharacterMotionState.RunToIdle, 0.16f);

        public CharacterMotionClip GetClip(CharacterMotionState state)
        {
            return state switch
            {
                CharacterMotionState.Idle => idle,
                CharacterMotionState.IdleToWalk => idleToWalk,
                CharacterMotionState.Walk => walk,
                CharacterMotionState.WalkToIdle => walkToIdle,
                CharacterMotionState.WalkToRun => walkToRun,
                CharacterMotionState.Run => run,
                CharacterMotionState.RunToWalk => runToWalk,
                CharacterMotionState.IdleToRun => idleToRun,
                CharacterMotionState.RunToIdle => runToIdle,
                _ => idle
            };
        }
    }

    [Serializable]
    public sealed class CharacterMotionClip
    {
        public CharacterMotionState state;
        public Sprite[] frames = Array.Empty<Sprite>();
        public float framesPerSecond = 8f;
        public bool loop = true;
        public float transitionSeconds = 0.12f;

        public static CharacterMotionClip Looping(CharacterMotionState state, float framesPerSecond)
        {
            return new CharacterMotionClip
            {
                state = state,
                framesPerSecond = framesPerSecond,
                loop = true,
                transitionSeconds = 0f
            };
        }

        public static CharacterMotionClip Transition(CharacterMotionState state, float transitionSeconds)
        {
            return new CharacterMotionClip
            {
                state = state,
                framesPerSecond = 12f,
                loop = false,
                transitionSeconds = transitionSeconds
            };
        }

        public float Duration
        {
            get
            {
                if (loop)
                {
                    return float.PositiveInfinity;
                }

                if (frames != null && frames.Length > 0 && framesPerSecond > 0f)
                {
                    return frames.Length / framesPerSecond;
                }

                return Mathf.Max(0.01f, transitionSeconds);
            }
        }

        public Sprite ResolveFrame(float elapsedSeconds, Sprite fallback)
        {
            if (frames == null || frames.Length == 0)
            {
                return fallback;
            }

            var frameIndex = Mathf.FloorToInt(elapsedSeconds * Mathf.Max(0.01f, framesPerSecond));
            if (loop)
            {
                frameIndex %= frames.Length;
            }
            else
            {
                frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            }

            return frames[frameIndex] != null ? frames[frameIndex] : fallback;
        }
    }
}
