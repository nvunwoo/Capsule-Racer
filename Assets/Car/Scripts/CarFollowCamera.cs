using UnityEngine;

public class CarFollowCamera : MonoBehaviour
{
    public Transform target;        // 따라갈 자동차
    public Vector3 offset = new Vector3(0f, 3f, -6f);  // 자동차 기준 카메라 위치 오프셋

    [Header("위치 관성 설정")]
    public float positionSmoothTime = 0.15f;   // 값이 클수록 더 천천히 따라감
    private Vector3 positionVelocity = Vector3.zero;

    [Header("회전 관성 설정")]
    public float rotationSmoothSpeed = 5f;     // 값이 작을수록 더 느리게 회전함

    void LateUpdate()
    {
        if (target == null) return;

        // 1) 자동차 기준으로 카메라가 가야 할 '목표 위치'
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // 2) 위치를 바로 teleport 하지 않고, 부드럽게 따라가기 (관성 느낌)
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        // 3) 카메라가 바라볼 방향 (자동차를 바라보도록)
        Vector3 lookDir = target.position - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

            // 4) 회전을 바로 맞추지 않고, 서서히 따라감 (좌/우 회전이 늦게 따라오는 핵심)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }
    }
}
