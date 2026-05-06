using UnityEngine;

namespace BeltScroll
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterMotionDriver : MonoBehaviour
    {
        [SerializeField] private CharacterMotionSet motionSet;
        [SerializeField] private SpriteRenderer targetRenderer;

        private CharacterBaseMotion currentBaseMotion = CharacterBaseMotion.Idle;
        private CharacterBaseMotion targetBaseMotion = CharacterBaseMotion.Idle;
        private CharacterMotionState currentState = CharacterMotionState.Idle;
        private float stateElapsedSeconds;

        public CharacterMotionState CurrentState => currentState;
        public CharacterBaseMotion CurrentBaseMotion => currentBaseMotion;
        public CharacterBaseMotion TargetBaseMotion => targetBaseMotion;
        public CharacterMotionSet MotionSet => motionSet;

        public void SetMotionSet(CharacterMotionSet value)
        {
            motionSet = value;
            ApplyFrame();
        }

        public void SetDesiredMotion(CharacterBaseMotion desiredMotion)
        {
            if (desiredMotion == targetBaseMotion && currentState.IsBaseState())
            {
                return;
            }

            if (desiredMotion == targetBaseMotion && !currentState.IsBaseState())
            {
                return;
            }

            StartTransition(currentState.ToBaseMotion(), desiredMotion);
        }

        private void Awake()
        {
            EnsureRenderer();
            EnterBaseState(CharacterBaseMotion.Idle);
        }

        private void OnValidate()
        {
            EnsureRenderer();
        }

        private void Update()
        {
            stateElapsedSeconds += Time.deltaTime;

            if (!currentState.IsBaseState())
            {
                var clip = GetClip(currentState);
                if (stateElapsedSeconds >= clip.Duration)
                {
                    EnterBaseState(targetBaseMotion);
                }
            }

            ApplyFrame();
        }

        private void StartTransition(CharacterBaseMotion from, CharacterBaseMotion to)
        {
            targetBaseMotion = to;
            var transition = CharacterMotionStateUtility.ResolveTransition(from, to);

            if (transition.IsBaseState())
            {
                EnterBaseState(to);
                return;
            }

            currentState = transition;
            stateElapsedSeconds = 0f;
        }

        private void EnterBaseState(CharacterBaseMotion baseMotion)
        {
            currentBaseMotion = baseMotion;
            targetBaseMotion = baseMotion;
            currentState = baseMotion.ToState();
            stateElapsedSeconds = 0f;
        }

        private CharacterMotionClip GetClip(CharacterMotionState state)
        {
            return motionSet != null ? motionSet.GetClip(state) : null;
        }

        private void ApplyFrame()
        {
            EnsureRenderer();
            if (targetRenderer == null)
            {
                return;
            }

            var fallback = motionSet != null && motionSet.fallbackSprite != null
                ? motionSet.fallbackSprite
                : targetRenderer.sprite;

            var clip = GetClip(currentState);
            var sprite = clip != null ? clip.ResolveFrame(stateElapsedSeconds, fallback) : fallback;
            if (sprite != null)
            {
                targetRenderer.sprite = sprite;
            }
        }

        private void EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }
        }
    }
}
