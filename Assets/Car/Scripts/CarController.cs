using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CarController
/// - 모든 자동차에 공통으로 붙이는 주행용 스크립트
/// - 능력치는 인스펙터에서 차마다 다르게 설정해서 사용
/// - 키보드 테스트용: 전진(↑), 후진(↓), 좌(A), 우(D)
/// - 실제 모바일에서는 이 네 개를 버튼으로 매핑하면 됨
/// - 방식: Rigidbody 기반 아케이드 자동차 컨트롤러 (속도에 비례해 회전, 그립 적용)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("필수 컴포넌트")]
    public Rigidbody rb;          // 자동차 물리 계산에 사용할 Rigidbody

    [Header("능력치 - 속도 관련")]
    public float maxForwardSpeed = 20f;   // 최고 전진 속도 (m/s)
    public float maxReverseSpeed = 10f;   // 최고 후진 속도 (m/s)
    public float acceleration = 10f;      // 가속력 (속도가 올라가는 힘, m/s^2)
    public float brakePower = 12f;        // 제동력 (입력 없거나 반대 방향일 때 감속량)

    [Header("능력치 - 조향 / 그립")]
    public float turnSpeed = 90f;         // 최대 회전 속도 (deg/sec, 속도에 비례해서 실제 회전량 결정)
    public float maxSteerAngle = 45f;     // 최대 조향각(논리상 한계, 여기서는 스케일링용)
    public float grip = 8f;               // 그립(옆으로 미끄러지는 속도를 줄이는 정도, 높을수록 잘 안 미끄러짐)

    [Header("능력치 - 물리 특성")]
    public float weight = 1200f;          // 무게(kg 느낌, Rigidbody mass에 반영)
    public float stability = 5f;          // 안정성(값이 클수록 무게 중심을 더 아래로 내려서 뒤집힘/들림 방지)

    [Header("능력치 - 오프로드")]
    [Range(0f, 1f)]
    public float offroadSpeedMultiplier = 0.6f; // 오프로드에서 속도 제한 비율 (예: 0.6이면 60% 속도)
    public string offroadTag = "Offroad";       // 오프로드 지형에 붙일 태그 이름

    // 내부 상태값 -----------------------------

    // 입력 상태(버튼 눌림 여부 저장용)
    private bool forwardPressed;   // 전진 버튼(↑) 눌림 여부
    private bool backwardPressed;  // 후진 버튼(↓) 눌림 여부
    private bool leftPressed;      // 좌회전 버튼(A) 눌림 여부
    private bool rightPressed;     // 우회전 버튼(D) 눌림 여부

    // 처리용 입력 값
    private float moveInput;       // -1(후진) ~ 0(정지) ~ 1(전진)
    private float steerInput;      // -1(좌) ~ 0 ~ 1(우)

    // 오프로드 여부
    private bool isOffroad = false; // 현재 오프로드 위에 있는지 여부

    void Awake()
    {
        // Rigidbody 자동 할당 (인스펙터에서 안 넣었을 경우)
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // 무게 능력치를 Rigidbody에 반영
        rb.mass = weight;

        // 회전 보간(부드러운 움직임)
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // 대부분의 아케이드 카 컨트롤러처럼
        // X/Z 회전(앞뒤/옆 기울기)은 막고, Y 회전(좌우 방향)만 허용
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // stability 값을 이용해서 무게 중심을 조금 아래로 내림
        // 값이 클수록 더 아래로 내려가서 덜 들리고 덜 뒤집힘
        float centerOffsetY = -Mathf.Clamp(stability, 0f, 10f) * 0.1f; // stability 5 => -0.5
        rb.centerOfMass = new Vector3(0f, centerOffsetY, 0f);
    }

    void Update()
    {
        // 키보드 장치가 있는지 먼저 체크 (에디터/PC용)
        if (Keyboard.current != null)
        {
            forwardPressed = Keyboard.current.upArrowKey.isPressed;    // 전진
            backwardPressed = Keyboard.current.downArrowKey.isPressed;  // 후진
            leftPressed = Keyboard.current.aKey.isPressed;          // 좌
            rightPressed = Keyboard.current.dKey.isPressed;          // 우
        }
        else
        {
            forwardPressed = false;
            backwardPressed = false;
            leftPressed = false;
            rightPressed = false;
        }

        // 전진/후진 동시에 누르면 무효
        if (forwardPressed && backwardPressed)
        {
            forwardPressed = false;
            backwardPressed = false;
        }

        // 좌/우 동시에 누르면 무효
        if (leftPressed && rightPressed)
        {
            leftPressed = false;
            rightPressed = false;
        }

        // 이동 입력 계산
        if (forwardPressed)
            moveInput = 1f;
        else if (backwardPressed)
            moveInput = -1f;
        else
            moveInput = 0f;

        // 방향 입력 계산 (전/후진 중일 때만 회전)
        if (moveInput != 0f)
        {
            if (leftPressed)
                steerInput = -1f;
            else if (rightPressed)
                steerInput = 1f;
            else
                steerInput = 0f;
        }
        else
        {
            steerInput = 0f;
        }
    }

    void FixedUpdate()
    {
        // 물리 관련 처리는 FixedUpdate에서 수행

        // 현재 속도를 로컬 좌표계 기준으로 가져옴
        // localVelocity.z = 전후 방향 속도, localVelocity.x = 좌우(옆) 방향 속도
        Vector3 velocity = rb.linearVelocity;
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        float forwardVelocity = localVelocity.z; // 현재 전/후진 속도

        // ---------------------------------------
        // 1. 목표 속도 계산 (실제 자동차 느낌)
        // ---------------------------------------
        float targetForwardSpeed = 0f;

        if (moveInput > 0f)
        {
            // 전진 목표 속도
            targetForwardSpeed = maxForwardSpeed;
        }
        else if (moveInput < 0f)
        {
            // 후진 목표 속도 (음수)
            targetForwardSpeed = -maxReverseSpeed;
        }
        else
        {
            // 입력이 없으면 자연 감속 (목표 속도 0)
            targetForwardSpeed = 0f;
        }

        // 오프로드일 경우 목표 속도 줄이기
        if (isOffroad)
        {
            targetForwardSpeed *= offroadSpeedMultiplier;
        }

        // forwardVelocity를 targetForwardSpeed 쪽으로 이동
        // 가속/제동을 분리해 처리 (차가 실제로 가속/감속하는 느낌)
        float accelRate = acceleration;
        float decelRate = brakePower;

        if (Mathf.Abs(targetForwardSpeed) > Mathf.Abs(forwardVelocity))
        {
            // 목표가 더 빠르면 가속
            forwardVelocity = Mathf.MoveTowards(
                forwardVelocity,
                targetForwardSpeed,
                accelRate * Time.fixedDeltaTime
            );
        }
        else
        {
            // 목표가 더 느리면 제동
            forwardVelocity = Mathf.MoveTowards(
                forwardVelocity,
                targetForwardSpeed,
                decelRate * Time.fixedDeltaTime
            );
        }

        // ---------------------------------------
        // 2. 그립 처리 (옆으로 미끄러지는 속도 줄이기)
        // ---------------------------------------
        // grip 값이 클수록 localVelocity.x(옆 미끄러짐)를 더 빨리 0으로 보정
        localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, grip * Time.fixedDeltaTime);

        // 변경된 전/후진 속도 다시 대입
        localVelocity.z = forwardVelocity;

        // 로컬 속도를 월드 속도로 바꿔서 Rigidbody에 적용
        rb.linearVelocity = transform.TransformDirection(localVelocity);

        // ---------------------------------------
        // 3. 조향(회전) 처리 (속도에 비례해서 회전량 결정)
        // ---------------------------------------

        // 실제 자동차처럼: 거의 안 움직이면 회전도 거의 안 함
        float speedMagnitude = Mathf.Abs(forwardVelocity);

        if (speedMagnitude > 0.1f && Mathf.Abs(steerInput) > 0f)
        {
            // 속도가 높을수록 회전량이 커지되, 너무 과하지 않게 정규화
            float speedFactor = Mathf.Clamp01(speedMagnitude / maxForwardSpeed);

            // 후진 시에는 방향 반대로 (실제 자동차와 동일)
            float directionSign = Mathf.Sign(forwardVelocity); // 전진: 1, 후진: -1

            // 이번 프레임에 회전할 각도(도 단위)
            float turnAmount = steerInput * turnSpeed * speedFactor * Time.fixedDeltaTime * directionSign;

            // Rigidbody를 통해 회전을 적용 (물리와 자연스럽게 동작)
            Quaternion deltaRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }

        // ---------------------------------------
        // 4. 추가 안정화는 Rigidbody 설정으로 해결
        // ---------------------------------------
        // - X/Z 회전은 Constraints에서 막음
        // - centerOfMass를 아래로 내려서 들리는 현상 최소화
        // 별도의 transform.rotation 보정은 하지 않음 (물리와 충돌 방지)
    }

    // ---------------------------------------
    // 5. 오프로드 판정용 트리거
    // ---------------------------------------
    // 오프로드 구간 콜라이더에 "Is Trigger" 체크 + offroadTag 태그를 붙이면 동작

    private void OnTriggerEnter(Collider other)
    {
        // 오프로드 구간에 진입했는지 확인
        if (other.CompareTag(offroadTag))
        {
            isOffroad = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 오프로드 구간을 벗어났는지 확인
        if (other.CompareTag(offroadTag))
        {
            isOffroad = false;
        }
    }
}
