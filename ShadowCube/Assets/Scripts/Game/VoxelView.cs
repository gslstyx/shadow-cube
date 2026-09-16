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

        /// <summary>过关庆祝脉冲：放大再回弹（delay 用于错峰形成波浪）</summary>
        public void PlayPulse(float delay, float amount = 0.18f, float duration = 0.32f)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Pulse(delay, amount, duration));
        }

        /// <summary>
        /// 过关动效（需求 §3.4）：脉冲波浪 → **方块爆散**（向外飞出并缩小）→ 归位重组。
        /// 重组而不是消失，保证玩家仍能看到自己搭出的造型。
        /// </summary>
        public void PlaySolvedSequence(float delay, Vector3 outward)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(SolvedSequence(delay, outward));
        }

        private IEnumerator SolvedSequence(float delay, Vector3 outward)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            var basePos = transform.localPosition;

            // ① 波浪脉冲
            yield return PulseNow(0.18f, 0.26f);

            // ② 爆散：向外飞出 + 缩小
            float t = 0f;
            while (t < 0.34f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.34f);
                transform.localPosition = basePos + outward * (k * 0.55f);
                transform.localScale = _baseScale * (1f - k);
                yield return null;
            }

            // ③ 归位重组
            t = 0f;
            while (t < 0.22f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.22f);
                transform.localPosition = Vector3.Lerp(basePos + outward * 0.55f, basePos, k);
                transform.localScale = _baseScale * k;
                yield return null;
            }

            transform.localPosition = basePos;
            transform.localScale = _baseScale;
            _routine = null;
        }

        private IEnumerator PulseNow(float amount, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localScale = _baseScale * (1f + amount * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        private IEnumerator Pulse(float delay, float amount, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float wave = Mathf.Sin(k * Mathf.PI);           // 0 → 1 → 0
                transform.localScale = _baseScale * (1f + amount * wave);
                yield return null;
            }

            transform.localScale = _baseScale;
            _routine = null;
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
