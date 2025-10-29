using UnityEngine;

public class MonsterGemMovement : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectionRange = 10f;

    [Header("Distance Settings")]
    [SerializeField] private float preferredDistance = 5f; // 好みの距離
    [SerializeField] private float comfortZone = 2f; // この範囲内では動かずに留まる

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float acceleration = 5f; // 加速度
    [SerializeField] private float idleTime = 1f; // 目標地点到達後の待機時間

    [Header("Reaction Settings")]
    [SerializeField] private float reactionThreshold = 1.5f; // プレイヤーがこれだけ動いたら反応

    [Header("Obstacle Detection")]
    [SerializeField] private float raycastDistance = 1f;
    [SerializeField] private LayerMask obstacleLayer;

    private bool isPlayerDetected = false;
    private Rigidbody2D rb;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private float idleTimer = 0f;
    private Vector2 lastPlayerPosition;
    private Vector3 originalScale;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        lastPlayerPosition = player.position;

        // 元のスケール保存
        originalScale = transform.localScale;
    }


    void Update()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            isPlayerDetected = true;
        }

        if (isPlayerDetected)
        {
            float currentDistance = Vector2.Distance(transform.position, player.position);
            bool tooClose = currentDistance < (preferredDistance - comfortZone);
            bool tooFar = currentDistance > (preferredDistance + comfortZone);

            // 移動中でなくても、近すぎたら即座に後退
            if (tooClose)
            {
                // すでに後退中でも目標地点再計算
                DecideNewPosition();
                lastPlayerPosition = player.position;
                isMoving = true;
                idleTimer = 0f;
            }
            // 待機中でない時だけチェック（近づく場合）
            else if (!isMoving)
            {
                idleTimer += Time.deltaTime;

                if (idleTimer >= idleTime)
                {
                    float playerMovement = Vector2.Distance(player.position, lastPlayerPosition);
                    bool playerMovedSignificantly = playerMovement > reactionThreshold;

                    if (tooFar || (playerMovedSignificantly && tooFar))
                    {
                        DecideNewPosition();
                        lastPlayerPosition = player.position;
                        isMoving = true;
                        idleTimer = 0f;
                    }
                }
            }

            // 移動中なら
            if (isMoving)
            {
                MoveToTarget();
            }
            else
            {
                // 停止状態ではスムーズに減速
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * acceleration);
            }

            UpdateFacingDirection();
        }
    }

    void DecideNewPosition()
    {
        // プレイヤーとの方向ベクトル
        Vector2 directionFromPlayer = (transform.position - player.position).normalized;

        // 好みの距離だけ離れた位置を目標に設定
        targetPosition = (Vector2)player.position + directionFromPlayer * preferredDistance;

        // 障害物チェック及び回避
        Vector2 directionToTarget = (targetPosition - (Vector2)transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, raycastDistance, obstacleLayer);

        if (hit.collider != null)
        {
            // 障害物があれば上または下に回避
            Vector2 avoidDirection = Vector2.Perpendicular(directionToTarget);
            targetPosition = (Vector2)player.position + directionFromPlayer * preferredDistance + avoidDirection * 2f;
        }
    }

    void MoveToTarget()
    {
        Vector2 directionToTarget = (targetPosition - (Vector2)transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, targetPosition);

        // 目標地点に近づいたら減速
        if (distanceToTarget < 0.5f)
        {
            isMoving = false;
            return;
        }

        // スムーズに加速
        Vector2 desiredVelocity = directionToTarget * moveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.deltaTime * acceleration);
    }


    void UpdateFacingDirection()
    {
        // 元のサイズを保ちながら方向だけ変更
        if (player.position.x < transform.position.x)
        {
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }
        else
        {
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 認識範囲
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 好みの距離
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);

        // コンフォートゾーン
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, preferredDistance - comfortZone);
        Gizmos.DrawWireSphere(transform.position, preferredDistance + comfortZone);

        // 目標地点
        if (isMoving)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(targetPosition, 0.3f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
}
