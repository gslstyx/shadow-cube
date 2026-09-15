using System;
using System.Collections;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>单个方块的视图：生成时弹性弹出，消除时收缩消散（需求 §2.2-A）</summary>
    public class VoxelView : MonoBehaviour
    {
        private Vector3 _baseScale;
        private Coroutine _routine;

        public void Init(Vector3 baseScale, float spawnDuration)
        {
            _baseScale = baseScale;
            _routine = StartCoroutine(Spawn(spawnDuration));
        }

        public void PlayDespawn(float duration, Action onDone)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Despawn(duration, onDone));
        }

        private IEnumerator Spawn(float duration)
        {
            transform.localScale = _baseScale * 0.25f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localScale = _baseScale * EaseOutBack(k);
                yield return null;
            }
            transform.localScale = _baseScale;
            _routine = null;
        }

        private IEnumerator Despawn(float duration, Action onDone)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localScale = _baseScale * (1f - k * k);
                yield return null;
            }
            onDone?.Invoke();
        }

        /// <summary>EaseOutBack：先冲过 1 再回落，形成"弹出"手感</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 0.25f + (1f - 0.25f) * (1f + c3 * p * p * p + c1 * p * p);
        }
    }
}
