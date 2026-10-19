using System.Collections;
using System.Text;
using UnityEngine;

namespace Entrenamiento.Tests
{
    /// <summary>
    /// Prueba automatica (solo para Play): acerca la palma a un cubo con el brazo
    /// simulado (rotando PivoteBrazo como lo haria la IMU + angulo de codo), cierra
    /// los dedos de a poco y verifica:
    ///   - en que curl promedio se agarra (esperado: &gt; grabCurl = 0.5)
    ///   - que existe el FixedJoint y que se ignoran las colisiones brazo/mano vs cubo
    ///   - que el cubo acompana a la mano al levantarla
    ///   - que al abrir se suelta y se restauran las colisiones
    /// No modifica Grabbable ni PalmGrabber. Resultado por Debug.Log "[PruebaAgarre]".
    /// </summary>
    public class PruebaAgarre : MonoBehaviour
    {
        public string nombreCubo = "Cubo_7cm_08";
        public float velocidadDescenso = 0.02f; // m/s
        public float velocidadCierre = 0.35f;   // curl/s
        [Tooltip("Experimento: ignora colisiones dedos-cubo para aislar la causa del fallo.")]
        public bool ignorarDedos = false;

        PalmGrabber palm; HandController hand; Transform piv; CapsuleCollider trig;
        Grabbable g; Vector3 hombro;
        Vector3 objetivo; float e = 0f;
        readonly StringBuilder log = new StringBuilder();

        void L(string s) { log.AppendLine(s); Debug.Log("[PruebaAgarre] " + s); }

        IEnumerator Start()
        {
            palm = FindObjectOfType<PalmGrabber>();
            hand = palm.hand; piv = hand.transform.parent; hombro = piv.position;
            trig = palm.GetComponent<CapsuleCollider>();
            g = GameObject.Find(nombreCubo).GetComponent<Grabbable>();
            if (ignorarDedos) { foreach (var f in hand.GetComponentsInChildren<Collider>(true)) if (f.name.StartsWith("Falange")) foreach (var c in g.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(f, c, true); L("EXPERIMENTO: colisiones dedos-cubo ignoradas"); }
            L($"grabCurl={palm.grabCurl} histeresis={palm.histeresis} exigirCierre={palm.exigirCierreEnContacto} ventana={palm.ventanaAgarre}");

            yield return new WaitForSeconds(1.0f);
            Vector3 cubo0 = g.transform.position;
            L($"cubo inicial {cubo0:F3}");

            // --- 1) Aproximacion desde arriba ---
            float alto = 0.12f, bajo = 0.012f;
            float h = alto;
            Mover(cubo0 + Vector3.up * h);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.5f);

            while (h > bajo && g.EstadoActual != Grabbable.Estado.EnRango)
            {
                h -= velocidadDescenso * Time.fixedDeltaTime;
                Mover(cubo0 + Vector3.up * h);
                yield return new WaitForFixedUpdate();
            }
            L($"fin descenso: h={h:F3} estado={g.EstadoActual} palma={trig.bounds.center:F3} cubo={g.transform.position:F3}");
            yield return new WaitForSeconds(0.6f);
            L($"tras pausa: estado={g.EstadoActual} cubo={g.transform.position:F3} (desplazamiento={(g.transform.position - cubo0).magnitude * 100f:F1} cm)");

            // --- 2) Cierre gradual ---
            float curl = 0f; bool agarro = false; float curlAgarre = -1f;
            while (curl < 1f)
            {
                curl = Mathf.Min(1f, curl + velocidadCierre * Time.fixedDeltaTime);
                hand.SetHandCurl(curl);
                yield return new WaitForFixedUpdate();
                if (Mathf.Abs(curl * 20f - Mathf.Round(curl * 20f)) < velocidadCierre * Time.fixedDeltaTime * 10f) L($"  curl={curl:F2} estado={g.EstadoActual} cubo={g.transform.position:F3} palma={trig.bounds.center:F3} dist={(g.transform.position - trig.bounds.center).magnitude * 100f:F1}cm held={(palm.Held != null)}");
                if (!agarro && palm.Held != null) { agarro = true; curlAgarre = curl; L($"AGARRE en curl={curl:F2} sosteniendo {palm.Held.name}"); }
                if (agarro && curl > curlAgarre + 0.1f) break;
            }
            if (!agarro) { L($"NO AGARRO. curl final={curl:F2} estado={g.EstadoActual} held={(palm.Held ? palm.Held.name : "null")}"); }
            yield return new WaitForSeconds(0.3f);

            // --- 3) Verificaciones de fisica ---
            if (palm.Held != null)
            {
                var j = palm.GetComponent<FixedJoint>();
                L($"FixedJoint={(j != null)} conectado={(j != null && j.connectedBody == g.rb)}");
                int tot = 0, ign = 0;
                foreach (var a in palm.hand.GetComponentsInChildren<Collider>(true))
                {
                    if (a.isTrigger) continue;
                    foreach (var c in g.GetComponentsInChildren<Collider>())
                    { tot++; if (Physics.GetIgnoreCollision(a, c)) ign++; }
                }
                L($"colisiones ignoradas brazo-cubo: {ign}/{tot}");
                L($"cubo.isKinematic={g.rb.isKinematic} gravedad={g.rb.useGravity} estado={g.EstadoActual}");

                // --- 4) Levantar ---
                Vector3 baseT = cubo0 + Vector3.up * h;
                Vector3 off0 = g.transform.position - trig.bounds.center;
                float lift = 0f;
                while (lift < 0.06f)
                {
                    lift += 0.04f * Time.fixedDeltaTime;
                    Mover(baseT + Vector3.up * lift);
                    yield return new WaitForFixedUpdate();
                }
                yield return new WaitForSeconds(0.5f);
                Vector3 off1 = g.transform.position - trig.bounds.center;
                L($"levantado: cubo={g.transform.position:F3} palma={trig.bounds.center:F3} desvio relativo={(off1 - off0).magnitude * 100f:F2} cm held={(palm.Held != null)}");

                // --- 5) Soltar ---
                float c2 = 1f;
                while (c2 > 0f)
                {
                    c2 = Mathf.Max(0f, c2 - 0.8f * Time.fixedDeltaTime);
                    hand.SetHandCurl(c2);
                    yield return new WaitForFixedUpdate();
                    if (palm.Held == null) { L($"SUELTA en curl={c2:F2}"); break; }
                }
                // alejar la mano hacia arriba para que el cubo se separe
                for (int i = 0; i < 40; i++) { lift += 0.1f * Time.fixedDeltaTime; Mover(baseT + Vector3.up * lift); yield return new WaitForFixedUpdate(); }
                yield return new WaitForSeconds(1.5f);
                int ign2 = 0;
                foreach (var a in palm.hand.GetComponentsInChildren<Collider>(true))
                {
                    if (a.isTrigger) continue;
                    foreach (var c in g.GetComponentsInChildren<Collider>()) if (Physics.GetIgnoreCollision(a, c)) ign2++;
                }
                L($"tras soltar: held={(palm.Held != null)} joint={(palm.GetComponent<FixedJoint>() != null)} colisiones aun ignoradas={ign2}/{tot} cubo={g.transform.position:F3}");
            }
            L("FIN");
            Resultado = log.ToString();
            Terminado = true;
        }

        public static string Resultado = "";
        public static bool Terminado = false;

        // Posiciona la palma (centro del trigger) en 't' usando codo + rotacion del pivote.
void Mover(Vector3 t)
        {
            float need = (t - hombro).magnitude;
            piv.rotation = Quaternion.identity;
            float bestE = e, bestErr = 1e9f;
            for (float a = 0f; a <= 90f; a += 1f)
            {
                hand.SetElbowAngle(a); Physics.SyncTransforms();
                float err = Mathf.Abs((trig.bounds.center - hombro).magnitude - need);
                if (err < bestErr) { bestErr = err; bestE = a; }
            }
            e = bestE; hand.SetElbowAngle(e); Physics.SyncTransforms();
            Quaternion r1 = Quaternion.FromToRotation(trig.bounds.center - hombro, t - hombro);
            // giro (roll) alrededor del eje hombro->objetivo para que la palma mire hacia abajo
            Vector3 eje = (t - hombro).normalized;
            float bestPhi = 0f, bestScore = -2f;
            for (float phi = 0f; phi < 360f; phi += 2f)
            {
                piv.rotation = Quaternion.AngleAxis(phi, eje) * r1;
                Physics.SyncTransforms();
                float score = palm.transform.forward.y; // la normal de la palma es -forward; queremos que mire a -Y
                if (score > bestScore) { bestScore = score; bestPhi = phi; }
            }
            piv.rotation = Quaternion.AngleAxis(bestPhi, eje) * r1;
            Physics.SyncTransforms();
        }
    }
}
