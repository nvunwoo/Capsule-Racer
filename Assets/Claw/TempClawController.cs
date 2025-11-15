using UnityEngine;

/// <summary>
/// TempClawController
/// - 인형뽑기 기계 집게처럼 위/아래로 반복 이동하는 임시 장애물 컨트롤러
/// - 설치된 초기 위치를 기준으로 아래로 내려갔다가 다시 원래 위치로 돌아오는 동작을 반복
/// - 내려가기 전, 올라간 후에는 3~10초 사이 랜덤 시간 동안 대기
/// - 목표 지점에 가까워질수록 자동으로 감속되도록 구현
/// </summary>
[DisallowMultipleComponent]
public class TempClawController : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("초기 위치 기준으로 얼마나 아래로 내려갈지 (월드 기준 Y- 방향, 단위: 미터)")]
    public float downDistance = 2f;   // 내려가는 거리

    [Tooltip("기본 이동 속도 (값이 클수록 전체 이동이 빨라짐, 단위: m/s 정도 느낌)")]
    public float moveSpeed = 2f;      // 기본 이동 속도

    [Header("대기 시간 설정 (초)")]
    [Tooltip("내려가기/올라가기 사이에 최소 대기 시간 (초)")]
    public float minWaitTime = 3f;

    [Tooltip("내려가기/올라가기 사이에 최대 대기 시간 (초)")]
    public float maxWaitTime = 10f;

    [Header("감속 관련 설정 (고급 설정, 필요하면 조절)")]
    [Tooltip("감속이 시작되는 거리. 목표 지점과 이 거리 이하로 가까워지면 점점 속도가 줄어든다.")]
    public float slowDownDistance = 0.5f;

    [Tooltip("감속 시 최소 속도 비율 (0에 가까울수록 끝에 아주 천천히 움직임)")]
    [Range(0.05f, 1f)]
    public float minSpeedFactor = 0.2f;

    // 내부에서 사용할 위치들
    private Vector3 _startPosition;   // 집게의 초기 위치 (위치 기준점)
    private Vector3 _downPosition;    // 내려갈 목표 위치

    private void Awake()
    {
        // 초기 위치 저장
        _startPosition = transform.position;

        // 월드 기준 아래 방향으로 downDistance만큼 이동한 위치를 내려갈 목표 지점으로 사용
        _downPosition = _startPosition + Vector3.down * downDistance;

        // min/max 대기 시간이 잘못되어 있으면 자동 보정
        if (maxWaitTime < minWaitTime)
        {
            float temp = minWaitTime;
            minWaitTime = maxWaitTime;
            maxWaitTime = temp;
        }

        if (slowDownDistance <= 0f)
        {
            slowDownDistance = downDistance * 0.3f; // 기본값: 내려가는 거리의 30% 지점부터 감속
        }
    }

    private void OnEnable()
    {
        // 반복 동작 코루틴 시작
        StartCoroutine(ClawRoutine());
    }

    private void OnDisable()
    {
        // 오브젝트 비활성화 시 코루틴 정리
        StopAllCoroutines();
    }

    /// <summary>
    /// 집게의 메인 동작 루프:
    /// 1) 랜덤 대기
    /// 2) 아래로 이동
    /// 3) 다시 랜덤 대기
    /// 4) 위로 이동
    /// 를 무한 반복
    /// </summary>
    private System.Collections.IEnumerator ClawRoutine()
    {
        while (true)
        {
            // 1. 랜덤 시간 대기
            float wait1 = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(wait1);

            // 2. 아래로 이동 (초기 위치 -> downPosition)
            yield return MoveToPosition(_downPosition);

            // 3. 다시 랜덤 시간 대기
            float wait2 = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(wait2);

            // 4. 위로 이동 (downPosition -> 초기 위치)
            yield return MoveToPosition(_startPosition);

            // 이 후 while 루프에 의해 반복
        }
    }

    /// <summary>
    /// 지정된 목표 위치까지 이동시키는 코루틴
    /// - moveSpeed를 기본 속도로 사용
    /// - 목표 지점에 가까워질수록 자동 감속 (slowDownDistance와 minSpeedFactor를 사용)
    /// </summary>
    private System.Collections.IEnumerator MoveToPosition(Vector3 targetPos)
    {
        while (true)
        {
            Vector3 currentPos = transform.position;
            Vector3 toTarget = targetPos - currentPos;
            float distance = toTarget.magnitude;

            // 목표 지점에 충분히 가까워졌으면 위치를 딱 맞추고 종료
            if (distance < 0.01f)
            {
                transform.position = targetPos;
                yield break;
            }

            // 방향 벡터 (정규화)
            Vector3 dir = toTarget / distance;

            // 남은 거리 기반 속도 감속 비율 계산
            // distance >= slowDownDistance 일 때는 1.0 근처, 가까워질수록 minSpeedFactor까지 줄어듦
            float t = Mathf.Clamp01(distance / slowDownDistance);
            float speedFactor = Mathf.Lerp(minSpeedFactor, 1f, t);

            // 이번 프레임 이동량 = 기본 속도 * 감속 비율 * deltaTime
            float step = moveSpeed * speedFactor * Time.deltaTime;

            // 목표보다 더 많이 나가지 않도록 MoveTowards 사용
            transform.position = Vector3.MoveTowards(currentPos, targetPos, step);

            yield return null; // 다음 프레임까지 대기
        }
    }

#if UNITY_EDITOR
    // 에디터에서 이동 범위를 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        // 시작 위치와 내려갈 위치를 선으로 표시 (에디터에서 보기 좋게)
        Vector3 startPos = Application.isPlaying ? _startPosition : transform.position;
        Vector3 downPos = startPos + Vector3.down * downDistance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPos, downPos);

        Gizmos.DrawSphere(startPos, 0.05f);
        Gizmos.DrawSphere(downPos, 0.05f);
    }
#endif
}
