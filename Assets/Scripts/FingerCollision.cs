using UnityEngine;

public class FingerCollision : MonoBehaviour
{
    [HideInInspector]
    public Rigidbody touchedRigidbody; // Guarda el Rigidbody del objeto tocado

    void OnTriggerEnter(Collider other)
    {
        // Solo registramos objetos que tengan el tag "Grabbable" y un Rigidbody
        if (other.CompareTag("Grabbable") && other.attachedRigidbody != null)
        {
            touchedRigidbody = other.attachedRigidbody;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Grabbable") && other.attachedRigidbody == touchedRigidbody)
        {
            touchedRigidbody = null; // Limpiamos al dejar de tocar
        }
    }
}