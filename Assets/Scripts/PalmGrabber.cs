using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agarre por la palma de la protesis.
///  - Detecta objetos Grabbable dentro del trigger de la palma (rango de agarre)
///    y se lo informa a cada objeto (para el resaltado verde/rojo).
///  - Agarra cuando la mano se CIERRA estando el objeto en rango (no se pega a
///    un puno que ya venia cerrado). Elige el candidato mas cercano a la palma.
///  - Mientras sostiene un objeto ignora las colisiones entre ese objeto y TODO
///    el brazo (armRoot). Al soltar, el Grabbable las restaura cuando se separa.
///  - Histeresis entre cerrar/abrir para que no parpadee el agarre.
/// </summary>
public class PalmGrabber : MonoBehaviour
{
    public enum ModoPreAgarre { Ninguno, IgnorarDedos, IgnorarBrazoCompleto, DedosSiempre, DedosSiempreYBrazoEnRango }

    [Tooltip("Cierre promedio de los dedos (0-1) a partir del cual se considera la mano cerrada.")]
    public float grabCurl = 0.8f;
    [Tooltip("Margen por debajo de grabCurl para considerar la mano abierta otra vez (evita parpadeos).")]
    public float histeresis = 0.1f;
    [Tooltip("Si esta activo, solo agarra si la mano se cerro con el objeto en rango (o hasta 'ventanaAgarre' segundos antes de entrar).")]
    public bool exigirCierreEnContacto = true;
    public float ventanaAgarre = 0.3f;

    [Header("Pre-agarre")]
    [Tooltip("Ninguno = comportamiento original (los dedos empujan el objeto). IgnorarDedos = con el objeto en rango, los dedos lo atraviesan. IgnorarBrazoCompleto = tambien palma y antebrazo.")]
    public ModoPreAgarre modoPreAgarre = ModoPreAgarre.Ninguno;

    // Dedos que nunca colisionan con Grabbables (modos DedosSiempre*). Grabbable los respeta al restaurar colisiones.
    private static readonly HashSet<Collider> dedosFantasma = new HashSet<Collider>();
    public static bool EsDedoFantasma(Collider c) { return dedosFantasma.Contains(c); }

    [Tooltip("Opcional: Rigidbody (p. ej. el de la mano fisica) que sostiene la union. Vacio = este objeto.")]
    public Rigidbody cuerpoUnion;

    public HandController hand;
    [Tooltip("Raiz de la mano.")]
    public Transform handRoot;
    [Tooltip("Raiz del brazo completo: sus colliders se ignoran con el objeto agarrado. Vacio = el objeto del HandController.")]
    public Transform armRoot;

    public Grabbable Held => held;

    private FixedJoint joint;
    private Grabbable held;
    private readonly Dictionary<Grabbable, int> candidatos = new Dictionary<Grabbable, int>();
    private readonly HashSet<Grabbable> preIgnorados = new HashSet<Grabbable>();
    private readonly List<Grabbable> purga = new List<Grabbable>();
    private Collider[] armColliders;
    private Collider[] dedoColliders;
    private bool manoCerrada;
    private float tCierre = -999f;

    private void Awake()
    {
        Transform root = armRoot != null ? armRoot : (hand != null ? hand.transform : handRoot);
        if (root == null) root = transform;

        var lista = new List<Collider>();
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger) lista.Add(c);
        if (handRoot != null)
            foreach (var c in handRoot.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && !lista.Contains(c)) lista.Add(c);
        armColliders = lista.ToArray();

        // colliders de los dedos (todo lo que cuelga de las falanges del HandController)
        var dedos = new List<Collider>();
        if (hand != null && hand.fingers != null)
            foreach (var f in hand.fingers)
                if (f != null && f.joints != null)
                    foreach (var j in f.joints)
                        if (j != null)
                            foreach (var c in j.GetComponentsInChildren<Collider>(true))
                                if (!c.isTrigger && !dedos.Contains(c)) dedos.Add(c);
        dedoColliders = dedos.ToArray();
        AplicarModoPermanente();

        // FixedJoint necesita un Rigidbody en este objeto (cinematico: lo mueve el brazo)
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }
    }

    // ------------------------------------------------------------------
    //  Rango de agarre
    // ------------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        Grabbable g = other.GetComponentInParent<Grabbable>();
        if (g == null || g.rb == null) return;
        if (!g.PermiteAgarreCon(other)) return;

        int n;
        candidatos.TryGetValue(g, out n);
        candidatos[g] = n + 1;
        if (n == 0)
        {
            g.SetEnRango(this, true);
            PreIgnorar(g);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Grabbable g = other.GetComponentInParent<Grabbable>();
        if (g == null) return;
        if (!g.PermiteAgarreCon(other)) return;

        int n;
        if (!candidatos.TryGetValue(g, out n)) return;
        if (n <= 1)
        {
            candidatos.Remove(g);
            g.SetEnRango(this, false);
            PreRestaurar(g);
        }
        else candidatos[g] = n - 1;
    }

    // ------------------------------------------------------------------
    //  Pre-agarre
    // ------------------------------------------------------------------
    private Collider[] SubconjuntoPreAgarre()
    {
        switch (modoPreAgarre)
        {
            case ModoPreAgarre.IgnorarDedos: return dedoColliders;
            case ModoPreAgarre.IgnorarBrazoCompleto: return armColliders;
            case ModoPreAgarre.DedosSiempreYBrazoEnRango: return armColliders;
            default: return null;
        }
    }

    private void PreIgnorar(Grabbable g)
    {
        var sub = SubconjuntoPreAgarre();
        if (sub == null || g.IsHeld) return;
        g.IgnorarColisiones(sub);
        preIgnorados.Add(g);
    }

    private void PreRestaurar(Grabbable g)
    {
        if (!preIgnorados.Remove(g)) return;
        if (g.IsHeld) return;                       // lo restaura OnRelease
        g.RestaurarColisionesSuave(armColliders);   // superconjunto: restaurar lo que no esta ignorado no hace nada
    }

    /// <summary>Cambia el modo en caliente (aplica/deshace el ignorado permanente de los dedos).</summary>
    public void SetModo(ModoPreAgarre m)
    {
        modoPreAgarre = m;
        AplicarModoPermanente();
    }

    private void AplicarModoPermanente()
    {
        if (dedoColliders == null) return;
        bool permanente = modoPreAgarre == ModoPreAgarre.DedosSiempre || modoPreAgarre == ModoPreAgarre.DedosSiempreYBrazoEnRango;
        var grabbables = FindObjectsOfType<Grabbable>();
        foreach (var d in dedoColliders)
        {
            if (d == null) continue;
            if (permanente) dedosFantasma.Add(d); else dedosFantasma.Remove(d);
            foreach (var g in grabbables)
                foreach (var c in g.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(d, c, permanente);
        }
    }

    // ------------------------------------------------------------------
    private void Update()
    {
        Purgar();

        float curl = Curl();
        if (!manoCerrada && curl > grabCurl) { manoCerrada = true; tCierre = Time.time; }
        else if (manoCerrada && curl < grabCurl - histeresis) manoCerrada = false;

        if (held == null)
        {
            if (!manoCerrada) return;
            if (exigirCierreEnContacto && Time.time - tCierre > ventanaAgarre) return;
            Grabbable g = MejorCandidato();
            if (g != null) GrabObject(g);
        }
        else if (!manoCerrada || joint == null)
        {
            ReleaseObject();
        }
    }

    private void OnDisable()
    {
        if (held != null) ReleaseObject();
        foreach (var g in candidatos.Keys) if (g != null) g.SetEnRango(this, false);
        foreach (var g in preIgnorados) if (g != null) g.RestaurarColisionesSuave(armColliders);
        preIgnorados.Clear();
        candidatos.Clear();
    }

    private float Curl()
    {
        if (hand == null || hand.fingers == null || hand.fingers.Length == 0) return 0f;
        float sum = 0f;
        foreach (var finger in hand.fingers) sum += finger.curl;
        return sum / hand.fingers.Length;
    }

    private Grabbable MejorCandidato()
    {
        Grabbable mejor = null;
        float dMin = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var g in candidatos.Keys)
        {
            if (g == null || g.IsHeld) continue;
            float d = (g.rb.worldCenterOfMass - p).sqrMagnitude;
            if (d < dMin) { dMin = d; mejor = g; }
        }
        return mejor;
    }

    private void Purgar()
    {
        purga.Clear();
        foreach (var g in candidatos.Keys)
            if (g == null || !g.isActiveAndEnabled) purga.Add(g);
        foreach (var g in purga) { candidatos.Remove(g); preIgnorados.Remove(g); }
    }

    private void GrabObject(Grabbable g)
    {
        held = g;
        // primero se ignoran las colisiones, despues se crea la union
        g.OnGrab(armColliders);

        var hostUnion = cuerpoUnion != null ? cuerpoUnion.gameObject : gameObject;
        joint = hostUnion.AddComponent<FixedJoint>();
        joint.connectedBody = g.rb;
        joint.enablePreprocessing = false;
        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;
    }

    private void ReleaseObject()
    {
        if (joint != null) Destroy(joint);
        joint = null;
        if (held != null)
        {
            // Si sigue en rango y hay pre-agarre, se mantiene ignorado hasta que salga del rango.
            if (SubconjuntoPreAgarre() != null && candidatos.ContainsKey(held))
            {
                held.OnRelease(null);
                preIgnorados.Add(held);
            }
            else held.OnRelease(armColliders);
        }
        held = null;
    }
}