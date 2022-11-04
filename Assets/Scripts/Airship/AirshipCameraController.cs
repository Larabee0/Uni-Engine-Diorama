using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Unity.Mathematics;

public class AirshipCameraController : MonoBehaviour
{
    private CinemachineVirtualCamera virtualCamera;
    private CinemachineOrbitalTransposer orbitalTransposer;
    [SerializeField] private float verticalSpeed = 10;
    [SerializeField] private float2 verticalExtremes;
    [SerializeField] private float neutralZ;
    [SerializeField] private float extremeZ;

    [SerializeField] private float timeFromZeroY;
    // Start is called before the first frame update
    void Start()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();

        orbitalTransposer = virtualCamera.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        // neutralZ = orbitalTransposer.m_FollowOffset.z;
        // extremeZ =neutralZ / 2f;

    }

    // Update is called once per frame
    void Update()
    {

        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector3 followOffset = orbitalTransposer.m_FollowOffset;
        followOffset.y += verticalSpeed * mouseY * Time.deltaTime;

        followOffset.y = math.clamp(followOffset.y, verticalExtremes.x, verticalExtremes.y);

        if(followOffset.y < 0)
        {
            timeFromZeroY=math.unlerp(verticalExtremes.x, 0, followOffset.y);
        }
        else
        {
            timeFromZeroY=math.unlerp(verticalExtremes.y, 0, followOffset.y);
        }
        followOffset.x = math.lerp(extremeZ,neutralZ,timeFromZeroY);
        followOffset.x = neutralZ;
        orbitalTransposer.m_FollowOffset=followOffset;
    }
}
