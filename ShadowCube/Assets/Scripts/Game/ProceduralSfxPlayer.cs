using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 程序化生成音效（零资源占位，M1/M2 用），M4 换成正式音频资源时只需替换实现。
    /// 生成音 = 上扬短音；消除音 = 下沉短音；过关 = 三音琶音。
    /// </summary>
    public class ProceduralSfxPlayer : MonoBehaviour, ISfxPlayer
    {
        [Range(0f, 1f)] public float volume = 0.35f;
        public bool muted;

        private AudioSource _source;
        private AudioClip _add;
        private AudioClip _remove;
        private AudioClip _solved;

        private void Awake()
        {
            // 注意：UnityEngine.Object 的 fake-null 不能用 ?? 判空
            _source = gameObject.GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _add = CreateTone("sfx_add", 520f, 780f, 0.075f, 0.6f);
            _remove = CreateTone("sfx_remove", 420f, 220f, 0.085f, 0.55f);
            _solved = CreateSolvedClip();
        }

        public void PlayAdd() => Play(_add);
        public void PlayRemove() => Play(_remove);
        public void PlaySolved() => Play(_solved);

        private void Play(AudioClip clip)
        {
            if (muted || clip == null || _source == null) return;
            _source.PlayOneShot(clip, volume);
        }

        // ── 波形生成（静态方法，便于单元测试）──────────────────
        /// <summary>扫频正弦 + 快起慢落包络</summary>
        public static AudioClip CreateTone(string name, float freqFrom, float freqTo, float duration, float amplitude)
        {
            const int rate = 44100;
            int samples = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
            var data = new float[samples];

            double phase = 0d;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(freqFrom, freqTo, t);
                phase += 2d * Mathf.PI * freq / rate;

                float attack = Mathf.Clamp01(i / (rate * 0.004f));
                float release = Mathf.Clamp01((samples - i) / (rate * 0.025f));
                data[i] = Mathf.Sin((float)phase) * attack * release * amplitude;
            }

            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>过关：C5-E5-G5 琶音</summary>
        public static AudioClip CreateSolvedClip()
        {
            const int rate = 44100;
            float[] freqs = { 523.25f, 659.25f, 783.99f };
            const float note = 0.11f;

            int perNote = Mathf.RoundToInt(rate * note);
            var data = new float[perNote * freqs.Length];

            for (int n = 0; n < freqs.Length; n++)
            {
                double phase = 0d;
                for (int i = 0; i < perNote; i++)
                {
                    float freq = freqs[n] * (1f + 0.02f * ((float)i / perNote));
                    phase += 2d * Mathf.PI * freq / rate;

                    float attack = Mathf.Clamp01(i / (rate * 0.004f));
                    float release = Mathf.Clamp01((perNote - i) / (rate * 0.04f));
                    data[n * perNote + i] = Mathf.Sin((float)phase) * attack * release * 0.55f;
                }
            }

            var clip = AudioClip.Create("sfx_solved", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
