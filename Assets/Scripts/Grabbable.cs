using UnityEngine;

public class Grabbable : MonoBehaviour
{
    public Rigidbody rb;

    private Collider[] objectColliders;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectColliders = GetComponentsInChildren<Collider>();
    }

    public void OnGrab(Collider[] handColliders)
    {
        if (handColliders == null)
            return;

        foreach (Collider handCol in handColliders)
        {
            if (handCol == null)
                continue;

            foreach (Collider objCol in objectColliders)
            {
                if (objCol == null)
                    continue;

                Physics.IgnoreCollision(handCol, objCol, true);
            }
        }

        Debug.Log("Ignorar Fisicas");
    }

    public void OnRelease(Collider[] handColliders)
    {
        if (handColliders == null)
            return;

        foreach (Collider handCol in handColliders)
        {
            if (handCol == null)
                continue;

            foreach (Collider objCol in objectColliders)
            {
                if (objCol == null)
                    continue;
                    
                Physics.IgnoreCollision(handCol, objCol, false);
            }

            Debug.Log("Fisicas Continuan");
        }
    }
}
