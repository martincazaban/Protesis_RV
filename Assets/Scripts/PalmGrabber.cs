using UnityEngine;

public class PalmGrabber : MonoBehaviour
{
    public float grabCurl = 0.8f;
    public HandController hand;
    public Transform handRoot;

    private FixedJoint joint;
    private Rigidbody candidate;
    private Grabbable candidateGrabbable;
    private Collider[] handColliders;

    private void Awake()
    {
        handColliders = handRoot.GetComponentsInChildren<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {   
        Grabbable grab = other.GetComponentInParent<Grabbable>();

        if (grab == null)
            return;

        // Obtener Rigidbody del objeto agarrable
        Rigidbody rb = grab.GetComponent<Rigidbody>();

        // Si no tiene Rigidbody, no podemos agarrarlo
        if (rb == null)
            return;

        // Guardamos el candidato
        candidate = rb;
        candidateGrabbable = grab;

        Debug.Log("Candidato detectado: " + candidate.name);
    }

    private void OnTriggerExit(Collider other)
    {
        Grabbable grab = other.GetComponentInParent<Grabbable>();

        if (grab == null)
            return;

        // Si el objeto que salió es el candidato
        if (grab == candidateGrabbable)
        {
            // Si todavía no está agarrado, simplemente olvidarlo
            if (joint == null)
            {
                candidate = null;
                candidateGrabbable = null;

                Debug.Log("Candidato salió de la palma");
            }
        }
    }

    void Update()
    {
        if (candidate == null)
            return;

        if (joint == null && HandClosed())
        {
            GrabObject();
        }

        if (joint != null && !HandClosed())
        {
            ReleaseObject();
        }
    }

    bool HandClosed()
    {
        float sum = 0f;

        foreach (var finger in hand.fingers)
            sum += finger.curl;

        return (sum / hand.fingers.Length) > grabCurl;
    }
    private void GrabObject()
    {
        // Crear FixedJoint
        joint = gameObject.AddComponent<FixedJoint>();

        joint.connectedBody = candidate;

        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;

        // Desactivar colisiones entre la mano y el objeto
        candidateGrabbable.OnGrab(handColliders);

        Debug.Log("Objeto agarrado: " + candidate.name);
    }

    private void ReleaseObject()
    {
        // Restaurar las colisiones
        candidateGrabbable.OnRelease(handColliders);

        // Destruir FixedJoint
        Destroy(joint);

        joint = null;
        candidate = null;
        candidateGrabbable = null;

        Debug.Log("Objeto soltado");
    }
    
}