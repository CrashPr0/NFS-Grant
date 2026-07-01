using UnityEngine;

namespace NSFGrant.Core
{
    /// <summary>
    /// Spatialized ambient loop for the hub waterfall, synthesized at
    /// runtime (filtered noise), so no audio asset ships with the project.
    /// In the headset, hearing the waterfall from the correct direction is
    /// one of the strongest presence cues available and it costs almost
    /// nothing - one looping AudioSource.
    ///
    /// Research note: the source sits in the hub only, equidistant from
    /// all three exhibit rooms (like the visual waterfall itself), so it
    /// adds ambience without acoustically favoring any station/condition.
    /// The clip is generated from a FIXED seed: every participant hears
    /// the identical sound.
    /// </summary>
    public class ProceduralAmbience : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 14f;
        [SerializeField] private int sampleRate = 22050;
        [SerializeField] private float loopSeconds = 6f;

        private void Start()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = BuildWaterLoop();
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 1f;                       // fully 3D
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;   // guarantees silence past maxDistance
            source.dopplerLevel = 0f;                       // no pitch warble while teleporting
            source.Play();
        }

        /// <summary>
        /// Water-like noise: white noise through a one-pole low-pass, with
        /// two slow amplitude wobbles so it churns instead of hissing
        /// statically. The final quarter second is crossfaded into the
        /// start so the loop point is seamless.
        /// </summary>
        private AudioClip BuildWaterLoop()
        {
            int count = Mathf.CeilToInt(sampleRate * loopSeconds);
            var samples = new float[count];
            var rng = new System.Random(20260630);          // fixed seed - identical for every participant

            float lowPassed = 0f;
            const float cutoff = 0.18f;                     // one-pole coefficient, ~dark rushing water
            for (int i = 0; i < count; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowPassed += cutoff * (white - lowPassed);

                float t = (float)i / sampleRate;
                // Two slow wobbles at different rates = churn rather than
                // static hiss. Each completes a whole number of cycles per
                // loop (2 and 5), so the wobble itself is seam-free.
                float cycles = t / loopSeconds * Mathf.PI * 2f;
                float churn = 0.75f
                    + 0.15f * Mathf.Sin(cycles * 2f)
                    + 0.10f * Mathf.Sin(cycles * 5f + 1.3f);
                samples[i] = lowPassed * churn;
            }

            // Crossfade tail into head for a click-free loop point.
            int fade = Mathf.Min(sampleRate / 4, count / 4);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                int tail = count - fade + i;
                samples[tail] = samples[tail] * (1f - w) + samples[i] * w;
            }

            var clip = AudioClip.Create("WaterfallLoop", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
