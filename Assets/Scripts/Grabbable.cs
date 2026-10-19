using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Objeto que puede tomar la palma de la protesis (PalmGrabber).
///  - Libre: colisiona fisicamente con todo el brazo (lo empuja, lo frena).
///  - Agarrado: se ignoran las colisiones mutuas con el brazo/mano.
///  - Al soltar: las colisiones se restauran recien cuando el objeto deja de
///    solaparse con la mano (evita que salga despedido).
/// Expone su estado (fuera de rango / en rango / agarrado) por evento para
/// que otros componentes (p.ej. el resaltado) reaccionen.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    public enum Estado { FueraDeRango, EnRango, Agarrado }

    public Rigidbody rb;

    [Tooltip("Tiempo maximo (s) que se siguen ignorando las colisiones con la mano despues de soltar, si el objeto sigue solapado.")]
    public float tiempoMaxSeparacion = 1.0f;

    [Tooltip("Opcional: si se asignan, la palma solo puede tomar el objeto por estos colliders (zona de agarre). Vacio = cualquier collider del objeto.")]
    public Collider[] zonasAgarre;

    [Tooltip("Velocidad maxima (m/s) con la que la fisica separa al objeto de un solapamiento. Valores bajos evitan que la mano cinematica lo lance al apretarlo contra la mesa o contra otros objetos.")]
    public float maxVelocidadDepenetracion = 1.0f;

    public Estado EstadoActual { get; private set; } = Estado.FueraDeRango;
    public bool IsHeld => EstadoActual == Estado.Agarrado;
    public event Action<Grabbable, Estado> EstadoCambiado;

    private Collider[] objectColliders;
    private readonly HashSet<PalmGrabber> enRangoDe = new HashSet<PalmGrabber>();
    private Coroutine restaurar;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = maxVelocidadDepenetracion;
        objectColliders = GetComponentsInChildren<Collider>();
    }

    private void OnDisable()
    {
        enRangoDe.Clear();
    }

    /// <summary>True si la palma puede tomar el objeto a traves de este collider.</summary>
    public bool PermiteAgarreCon(Collider c)
    {
        if (zonasAgarre == null || zonasAgarre.Length == 0) return true;
        foreach (var z in zonasAgarre) if (z == c) return true;
        return false;
    }

    // ------------------------------------------------------------------
    //  Rango (lo informa PalmGrabber al entrar/salir del trigger de la palma)
    // ------------------------------------------------------------------
    public void SetEnRango(PalmGrabber quien, bool enRango)
    {
        if (enRango) enRangoDe.Add(quien); else enRangoDe.Remove(quien);
        if (!IsHeld) CambiarEstado(enRangoDe.Count > 0 ? Estado.EnRango : Estado.FueraDeRango);
    }

    // ------------------------------------------------------------------
    //  Agarre / suelta
    // ------------------------------------------------------------------
    public void OnGrab(Collider[] handColliders)
    {
        if (restaurar != null) { StopCoroutine(restaurar); restaurar = null; }
        SetIgnorar(handColliders, true);
        rb.WakeUp();
        CambiarEstado(Estado.Agarrado);
    }

    public void OnRelease(Collider[] handColliders)
    {
        CambiarEstado(enRangoDe.Count > 0 ? Estado.EnRango : Estado.FueraDeRango);
        rb.WakeUp();
        if (handColliders == null) return;
        if (restaurar != null) StopCoroutine(restaurar);
        if (isActiveAndEnabled) restaurar = StartCoroutine(RestaurarCuandoSeSepare(handColliders));
        else SetIgnorar(handColliders, false);
    }

    // ------------------------------------------------------------------
    //  Pre-agarre: ignorar (parte de) la mano mientras el objeto esta en rango,
    //  para que los dedos no lo empujen antes de cerrar. Lo usa PalmGrabber.
    // ------------------------------------------------------------------
    public void IgnorarColisiones(Collider[] cols)
    {
        if (restaurar != null) { StopCoroutine(restaurar); restaurar = null; }
        SetIgnorar(cols, true);
    }

    /// <summary>Restaura las colisiones recien cuando el objeto deja de solaparse.</summary>
    public void RestaurarColisionesSuave(Collider[] cols)
    {
        if (cols == null) return;
        if (restaurar != null) StopCoroutine(restaurar);
        if (isActiveAndEnabled) restaurar = StartCoroutine(RestaurarCuandoSeSepare(cols));
        else SetIgnorar(cols, false);
    }

    private IEnumerator RestaurarCuandoSeSepare(Collider[] handColliders)
    {
        float limite = Time.time + tiempoMaxSeparacion;
        var espera = new WaitForFixedUpdate();
        yield return espera;
        while (Time.time < limite && Solapado(handColliders))
            yield return espera;
        SetIgnorar(handColliders, false);
        restaurar = null;
    }

    private bool Solapado(Collider[] handColliders)
    {
        foreach (var h in handColliders)
        {
            if (h == null || !h.enabled || h.isTrigger) continue;
            foreach (var o in objectColliders)
            {
                if (o == null || !o.enabled) continue;
                if (!h.bounds.Intersects(o.bounds)) continue;
                Vector3 dir; float dist;
                if (Physics.ComputePenetration(o, o.transform.position, o.transform.rotation,
                                               h, h.transform.position, h.transform.rotation,
                                               out dir, out dist))
                    return true;
            }
        }
        return false;
    }

    private void SetIgnorar(Collider[] handColliders, bool ignorar)
    {
        if (handColliders == null) return;
        foreach (Collider handCol in handColliders)
        {
            if (handCol == null) continue;
            if (!ignorar && PalmGrabber.EsDedoFantasma(handCol)) continue;   // modo DedosSiempre: nunca se restauran
            foreach (Collider objCol in objectColliders)
            {
                if (objCol == null) continue;
                Physics.IgnoreCollision(handCol, objCol, ignorar);
            }
        }
    }

    private void CambiarEstado(Estado nuevo)
    {
        if (nuevo == EstadoActual) return;
        EstadoActual = nuevo;
        if (EstadoCambiado != null) EstadoCambiado(this, nuevo);
    }
}
