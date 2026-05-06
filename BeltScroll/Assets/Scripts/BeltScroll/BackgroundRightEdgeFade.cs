using UnityEngine;

namespace BeltScroll
{
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BackgroundRightEdgeFade : MonoBehaviour
    {
        private static readonly int RightFadeWidthId = Shader.PropertyToID("_RightFadeWidth");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [Range(0f, 1f)]
        [SerializeField] private float rightFadeWidth;

        [Range(0f, 1f)]
        [SerializeField] private float alpha = 1f;

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;

        public float RightFadeWidth
        {
            get => rightFadeWidth;
            set
            {
                rightFadeWidth = Mathf.Clamp01(value);
                Apply();
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            propertyBlock ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(RightFadeWidthId, rightFadeWidth);
            propertyBlock.SetFloat(AlphaId, alpha);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
