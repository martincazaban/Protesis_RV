using System.Collections;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Sonido ambiente sutil generado por codigo: viento suave en loop y
    /// trinos de pajaros a lo lejos (3D, posicionados cerca de la ventana).
    /// No requiere archivos de audio.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbienteSonoro : MonoBehaviour
    {
        [Range(0f, 1f)] public float volumenViento = 0.12f;
        [Range(0f, 1f)] public float volumenPajaros = 0.25f;
        [Tooltip("Punto desde donde se oyen los pajaros (por ejemplo, la ventana).")]
        public Transform origenPajaros;
        public Vector2 intervaloPajaros = new Vector2(3f, 9f);

        private const int Fs = 22050;
        private AudioSource _viento;
        private AudioSource _pajaros;
        private AudioClip[] _trinos;

        private void Start()
        {
            _viento = GetComponent<AudioSource>();
            _viento.clip = CrearViento(8f);
            _viento.loop = true;
            _viento.spatialBlend = 0f;
            _viento.volume = volumenViento;
            _viento.Play();

            var go = new GameObject("Pajaros_Lejanos");
            go.transform.SetParent(origenPajaros != null ? origenPajaros : transform, false);
            _pajaros = go.AddComponent<AudioSource>();
            _pajaros.spatialBlend = 1f;
            _pajaros.rolloffMode = AudioRolloffMode.Logarithmic;
            _pajaros.minDistance = 3f;
            _pajaros.maxDistance = 30f;
            _pajaros.volume = volumenPajaros;
            _pajaros.playOnAwake = false;

            _trinos = new[]
            {
                CrearTrino(3000f, 4200f, 3, 0.12f, 1),
                CrearTrino(4600f, 3400f, 2, 0.16f, 2),
                CrearTrino(3500f, 5200f, 4, 0.09f, 3)
            };
            StartCoroutine(RutinaPajaros());
        }

        private IEnumerator RutinaPajaros()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(intervaloPajaros.x, intervaloPajaros.y));
                _pajaros.transform.localPosition = new Vector3(Random.Range(-2f, 2f), Random.Range(-0.5f, 1f), Random.Range(-2f, 2f));
                _pajaros.pitch = Random.Range(0.9f, 1.1f);
                _pajaros.PlayOneShot(_trinos[Random.Range(0, _trinos.Length)], 1f);
            }
        }

        private static AudioClip CrearViento(float segundos)
        {
            int n = (int)(segundos * Fs);
            int f = Fs / 2; // zona de crossfade para loop sin cortes
            var rnd = new System.Random(7);
            var buf = new float[n + f];
            float lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / n;
                float lfo1 = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f * t);
                float lfo2 = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 3f * t + 1.3f);
                float cutoff = Mathf.Lerp(0.015f, 0.06f, lfo1);
                float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += cutoff * (ruido - lp);
                buf[i] = lp * Mathf.Lerp(0.5f, 1f, lfo2) * 6f;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                data[i] = i < f ? Mathf.Lerp(buf[n + i], buf[i], (float)i / f) : buf[i];
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }
            var clip = AudioClip.Create("Viento", n, 1, Fs, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CrearTrino(float f0, float f1, int notas, float durNota, int seed)
        {
            float pausa = 0.07f;
            float total = notas * (durNota + pausa) + 0.1f;
            int n = (int)(total * Fs);
            var data = new float[n];
            var rnd = new System.Random(seed);
            for (int k = 0; k < notas; k++)
            {
                int ini = (int)(k * (durNota + pausa) * Fs);
                int len = (int)(durNota * Fs);
                float a = f0 * (0.95f + 0.1f * (float)rnd.NextDouble());
                float b = f1 * (0.95f + 0.1f * (float)rnd.NextDouble());
                float fase = 0f;
                for (int i = 0; i < len && ini + i < n; i++)
                {
                    float u = (float)i / len;
                    float frec = Mathf.Lerp(a, b, u) + 120f * Mathf.Sin(2f * Mathf.PI * 25f * u);
                    fase += 2f * Mathf.PI * frec / Fs;
                    float env = Mathf.Sin(Mathf.PI * u);
                    data[ini + i] += Mathf.Sin(fase) * env * env * 0.5f;
                }
            }
            var clip = AudioClip.Create("Trino" + seed, n, 1, Fs, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
