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
    [Tooltip("Cierre promedio de los dedos (0-1) a partir del cual se considera la mano cerrada.")]
    public float grabCurl = 0.8f;
    [Tooltip("Margen por debajo de grabCurl para considerar la mano abierta otra vez (evita parpadeos).")]
    public float histeresis = 0.1f;
    [Tooltip("Si esta activo, solo agarra si la mano se cerro con el objeto en rango (o hasta 'ventanaAgarre' segundos antes de entrar).")]
    public bool exigirCierreEnContacto = true;
    public float ventanaAgarre = 0.3f;

    public HandController hand;
    [Tooltip("Raiz de la mano.")]
    public Transform handRoot;
    [Tooltip("Raiz del brazo completo: sus colliders se ignoran con el objeto agarrado. Vacio = el objeto del HandController.")]
    public Transform armRoot;

    public Grabbable Held => held;

    private FixedJoint joint;
    private Grabbable held;
    private readonly Dictionary<Grabbable, int> candidatos = new Dictionary<Grabbable, int>();
    private readonly List<Grabbable> purga = new List<Grabbable>();
    private Collider[] armColliders;
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

        int n;
        candidatos.TryGetValue(g, out n);
        candidatos[g] = n + 1;
        if (n == 0) g.SetEnRango(this, true);
    }

    private void OnTriggerExit(Collider other)
    {
        Grabbable g = other.GetComponentInParent<Grabbable>();
        if (g == null) return;

        int n;
        if (!candidatos.TryGetValue(g, out n)) return;
        if (n <= 1) { candidatos.Remove(g); g.SetEnRango(this, false); }
        else candidatos[g] = n - 1;
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
        foreach (var g in purga) candidatos.Remove(g);
    }

    private void GrabObject(Grabbable g)
    {
        held = g;
        // primero se ignoran las colisiones, despues se crea la union
        g.OnGrab(armColliders);

        joint = gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = g.rb;
        joint.enablePreprocessing = false;
        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;
    }

    private void ReleaseObject()
    {
        if (joint != null) Destroy(joint);
        joint = null;
        if (held != null) held.OnRelease(armColliders);
        held = null;
    }
}