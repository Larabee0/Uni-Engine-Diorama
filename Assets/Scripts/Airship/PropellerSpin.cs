using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropellerSpin : MonoBehaviour
{
    [Range(0,100)]
    [SerializeField] private float spinSpeed = 10;

    [Range(0, 100)]
    [SerializeField] private float spinMultiplier = 10;
    [SerializeField] private float randomSpinOffset;
    private float angle = 0;

    void Start()
    {
        randomSpinOffset = Random.value/10f;
    }

    void Update()
    {
        angle += Time.deltaTime * spinMultiplier * (spinSpeed + randomSpinOffset);
        angle = angle > 360f ? 0f : angle;
        transform.localRotation =Quaternion.Euler(angle, -90f, 90);
    }
}
