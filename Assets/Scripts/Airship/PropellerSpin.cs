using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropellerSpin : MonoBehaviour
{
    [SerializeField] MeshRenderer physicalProp;
    [SerializeField] MeshRenderer blurredProp;
    [Range(0,100)]
    [SerializeField] private float maxSpinSpeed = 10;

    [Range(0, 1)]
    public float throttle = 0;

    [SerializeField] private bool autoThrottle = false;
    [SerializeField] private float autoThrottleSpeed = 5;
    [SerializeField] private bool autoLatch = false;

    [Range(0, 200)]
    [SerializeField] private float spinMultiplier = 10;

    [SerializeField] private float blurrPoint = 0.5f;
    [SerializeField] private float physicalPropPoint = 0.75f;
    
    [SerializeField] private float randomSpinOffset;
    [SerializeField] private float startRot;

    void Start()
    {
        randomSpinOffset = Random.Range(0.75f,1f);
        startRot = Random.Range(0f, 90f);

        transform.Rotate(Vector3.up, startRot, Space.Self);
        if (!physicalProp)
        {
            physicalProp = GetComponent<MeshRenderer>();
        }
        if (!blurredProp)
        {
            blurredProp = transform.GetChild(0).GetComponent<MeshRenderer>();
        }
    }

    void Update()
    {
        if (autoThrottle)
        {
            if (autoLatch)
            {
                throttle += autoThrottleSpeed * Time.deltaTime;
                if (throttle >= 1)
                {
                    throttle = 1;
                    autoLatch = false;
                }
            }
            else
            {
                throttle -= autoThrottleSpeed * Time.deltaTime;
                if (throttle <= 0)
                {
                    throttle = 0;
                    autoLatch = true;
                }
            }
        }
        // OnThrottleChanged();
        transform.Rotate(Vector3.up, OnThrottleChanged(), Space.Self);
    }

    private float OnThrottleChanged()
    {
        float spinSpeed = spinMultiplier * ((Mathf.Lerp(0, maxSpinSpeed, throttle) * randomSpinOffset) + randomSpinOffset / 2f) * Time.deltaTime;
        // if (throttle >= blurrPoint)
        // {
        //     transform.GetChild(0).gameObject.SetActive(true);
        // }
        // else
        // {
        //     transform.GetChild(0).gameObject.SetActive(false);
        // }
        Color colour = physicalProp.material.color;
        colour.a = Mathf.Lerp(1, 0, Mathf.InverseLerp(blurrPoint, physicalPropPoint, throttle));
        
        physicalProp.material.color = colour;
        colour.a = Mathf.InverseLerp(blurrPoint, physicalPropPoint, throttle);
        blurredProp.material.color = colour;
        return spinSpeed;
    }
}
