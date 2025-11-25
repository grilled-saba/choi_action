/*
 * ====================================================================================
 * スクリプト名: MonsterGemAI (AI制御・State Machine)
 * ====================================================================================
 * 
 * 【配置場所】
 * - MonsterGemのGameObjectにアタッチ
 * - MonsterGemSensor、MonsterGemMovementと同じオブジェクトに配置
 * 
 * 【参照スクリプト】
 * - MonsterGemSensor: プレイヤー検知、視界判定、スタック検知
 * - MonsterGemMovement: 移動制御、脱出処理
 * - MonsterGemAttack: AI状態を参照して攻撃判定
 * 
 * 【主な機能】
 * 1. State Machineによる5つの状態管理(Idle/Chase/Retreat/Return/Stuck)
 * 2. プレイヤーとの距離に応じた行動決定
 * 3. Leash(追跡範囲)による帰還処理
 * 4. CircleCastベースのスタック検知と脱出
 * 5. 経路探索失敗時の強制Stuck状態への移行
 * 6. 最終手段としてのテレポート機能
 * 
 * 【State遷移フロー】
 * Idle → Chase(プレイヤー検知) → Retreat(近すぎる) → Chase(適正距離)
 * 全状態 → Stuck(壁接触) → 脱出成功 → 元の状態
 * 全状態 → Return(Leash範囲外) → Idle(原点復帰)
 * Stuck → テレポート(脱出失敗時)
 * 
 * 【スタック検知の2つの方法】
 * 1. 通常Stuck: CircleCastで物理的な壁接触を検知
 * 2. 強制Stuck: 経路探索が2秒以上失敗した場合に強制移行
 * 
 * ====================================================================================
 */

using UnityEngine;

public class MonsterGemAI : MonoBehaviour
{
    // ========================================
    // State Machine列挙型
    // ========================================

    public enum AIState
    {
        Idle,       // 待機
        Chase,      // 追跡
        Retreat,    // 後退
        Return,     // 復帰
        Stuck       // 引っかかり
    }

    // ========================================
    // 参照
    // ========================================

    [Header("References")]
    [SerializeField] private Transform player;  // プレイヤーのTransform

    private MonsterGemSensor sensor;  // 感知システム
    private MonsterGemMovement movement;  // 移動システム

    // ========================================
    // Leash設定
    // ========================================

    [Header("Leash Settings")]
    [SerializeField] private float maxChaseDistance = 15f;  // 最大追跡距離(原点からの範囲)

    // ========================================
    // 反応設定
    // ========================================

    [Header("Reaction Settings")]
    [SerializeField] private float reactionThreshold = 1.5f;  // プレイヤー移動量の反応閾値
    [SerializeField] private float idleTime = 1f;  // 次回行動までの待機時間

    // ========================================
    // 距離設定
    // ========================================

    [Header("Distance Settings")]
    [SerializeField] private float preferredDistance = 5f;  // 好ましい距離
    [SerializeField] private float comfortZone = 2f;  // 快適ゾーン(±範囲)

    // ========================================
    // スタック復帰設定
    // ========================================

    [Header("Stuck Recovery")]
    [SerializeField] private float stuckRecoveryTime = 0.2f;  // 脱出試行間隔
    [SerializeField] private int maxStuckAttempts = 15;  // 最大脱出試行回数
    [SerializeField] private float teleportWarningTime = 3f;  // テレポート前の待機時間

    // ========================================
    // State Machine変数
    // ========================================

    private AIState currentState = AIState.Idle;  // 現在の状態
    private AIState previousState = AIState.Idle;  // 前の状態(Stuck復帰用)
    private float stuckTimer = 0f;  // Stuck状態の経過時間
    private int stuckAttempts = 0;  // 脱出試行回数

    // ========================================
    // テレポート関連
    // ========================================

    private bool isTeleporting = false;  // テレポート中フラグ
    private float teleportTimer = 0f;  // テレポートカウントダウン
    private bool isForcedStuck = false;  // 強制Stuck(経路なしによる)

    // ========================================
    // 基礎変数
    // ========================================

    private Vector2 spawnPosition;  // 原点位置
    private Vector2 lastPlayerPosition;  // プレイヤーの前回位置
    private float idleTimer = 0f;  // 待機タイマー

    // ========================================
    // ログ間隔制御(用途別管理)
    // ========================================

    private float lastStuckLogTime = 0f;  // Stuck検知ログ用
    private float lastReturnLogTime = 0f;  // Return経路ログ用
    private float noPathTimer = 0f;  // 経路なしタイマー
    private float noPathThreshold = 2f;  // 2秒間経路なしでStuck強制遷移
    private float logInterval = 1f;  // 1秒に1回のみログ出力

    // ========================================
    // 公開プロパティ(Attackスクリプト用)
    // ========================================

    public bool IsPlayerDetected => currentState == AIState.Chase || currentState == AIState.Retreat;  // プレイヤー検知中
    public bool IsReturning => currentState == AIState.Return;  // 復帰中

    // ========================================
    // 初期化
    // ========================================

    void Start()
    {
        // コンポーネント取得
        sensor = GetComponent<MonsterGemSensor>();
        movement = GetComponent<MonsterGemMovement>();

        // 原点位置を保存
        spawnPosition = transform.position;
        // プレイヤー初期位置を記録
        lastPlayerPosition = player.position;
    }

    // ========================================
    // 毎フレーム更新
    // ========================================

    void Update()
    {
        // テレポート中は他のロジックをスキップ
        if (isTeleporting)
        {
            HandleStuck();  // HandleTeleportが内部で呼ばれる
            return;
        }

        // ========================================
        // 1段階: Stuckチェック(最優先!) - CircleCast方式に変更
        // ========================================

        bool isStuck = sensor.IsStuckToWall();  // 4方向CircleCastで物理的接触を検知

        // デバッグ: 1秒に1回のみ状態確認
        if (Time.time - lastStuckLogTime >= logInterval && isStuck && currentState != AIState.Stuck)
        {
            Debug.Log($"MonsterGem: Stuck検知です! 現在状態: {currentState}");
            lastStuckLogTime = Time.time;
        }

        // 壁に接触していて、かつStuck状態でない場合
        if (isStuck && currentState != AIState.Stuck)
        {
            previousState = currentState;  // 復帰用に現在状態を保存
            currentState = AIState.Stuck;  // Stuck状態へ遷移
            stuckTimer = 0f;  // タイマーリセット
            stuckAttempts = 0;  // 試行回数リセット
            noPathTimer = 0f;  // 経路なしタイマーリセット
            isForcedStuck = false;  // 通常Stuck(壁に実際に接触)
            Debug.Log($"MonsterGem: 状態変更 [{previousState} → Stuck]");
        }

        // ========================================
        // 2段階: 状態別行動
        // ========================================

        switch (currentState)
        {
            case AIState.Idle:
                HandleIdle();
                break;

            case AIState.Chase:
                HandleChase();
                break;

            case AIState.Retreat:
                HandleRetreat();
                break;

            case AIState.Return:
                HandleReturn();
                break;

            case AIState.Stuck:
                HandleStuck();
                break;
        }

        // ========================================
        // 3段階: Leashチェック(Stuck以外)
        // ========================================

        if (currentState != AIState.Stuck)
        {
            CheckLeash();
        }
    }

    // ========================================
    // Idle状態: 待機
    // ========================================

    void HandleIdle()
    {
        // プレイヤー検知試行
        if (sensor.CanSeePlayer())
        {
            currentState = AIState.Chase;  // Chase状態へ遷移
            Debug.Log("MonsterGem: プレイヤー検知! [Idle → Chase]");
        }
    }

    // ========================================
    // Chase状態: 追跡
    // ========================================

    void HandleChase()
    {
        // 視界チェック
        if (!sensor.CanSeePlayer())
        {
            movement.Stop();  // 見失ったら停止
            return;
        }

        // 距離判定
        bool tooClose = sensor.IsTooClose();  // 近すぎるか
        bool tooFar = sensor.IsTooFar();  // 遠すぎるか

        // 近すぎる場合
        if (tooClose)
        {
            currentState = AIState.Retreat;  // Retreat状態へ遷移
            Debug.Log("MonsterGem: 近すぎます! [Chase → Retreat]");
            HandleRetreat();  // 即座に後退処理
        }
        // 快適ゾーン内の場合
        else if (!tooFar)
        {
            idleTimer += Time.deltaTime;  // 待機タイマー加算

            // 待機時間経過後
            if (idleTimer >= idleTime)
            {
                // プレイヤーの移動量を計算
                float playerMovement = Vector2.Distance(player.position, lastPlayerPosition);

                // プレイヤーが閾値以上動いて、かつ遠すぎる場合
                if (playerMovement > reactionThreshold && tooFar)
                {
                    Vector2 approachPos = CalculateApproachPosition();  // 接近位置計算
                    movement.MoveTo(approachPos);  // 移動開始
                    lastPlayerPosition = player.position;  // 位置更新
                    idleTimer = 0f;  // タイマーリセット
                }
            }
        }
        // 遠すぎる場合
        else
        {
            idleTimer += Time.deltaTime;  // 待機タイマー加算

            // 待機時間経過後
            if (idleTimer >= idleTime)
            {
                Vector2 approachPos = CalculateApproachPosition();  // 接近位置計算
                movement.MoveTo(approachPos);  // 移動開始
                lastPlayerPosition = player.position;  // 位置更新
                idleTimer = 0f;  // タイマーリセット
            }
        }
    }

    // ========================================
    // Retreat状態: 後退
    // ========================================

    void HandleRetreat()
    {
        // 視界チェック
        if (!sensor.CanSeePlayer())
        {
            movement.Stop();  // 見失ったら停止
            return;
        }

        // 距離判定
        bool tooClose = sensor.IsTooClose();  // 近すぎるか

        // まだ近すぎる場合
        if (tooClose)
        {
            // 継続して後退
            idleTimer += Time.deltaTime;  // 待機タイマー加算

            // 待機時間経過後
            if (idleTimer >= idleTime)
            {
                Vector2 retreatPos = CalculateRetreatPosition();  // 後退位置計算
                movement.MoveTo(retreatPos);  // 移動開始
                lastPlayerPosition = player.position;  // 位置更新
                idleTimer = 0f;  // タイマーリセット
            }
        }
        // 適正距離に達した場合
        else
        {
            currentState = AIState.Chase;  // Chase状態へ遷移
            Debug.Log("MonsterGem: 適正距離に到達! [Retreat → Chase]");
        }
    }

    // ========================================
    // Return状態: 復帰
    // ========================================

    void HandleReturn()
    {
        // 原点までの距離を計算
        float distanceToSpawn = Vector2.Distance(transform.position, spawnPosition);

        // 原点に十分近づいたか
        if (distanceToSpawn < 1.5f)
        {
            movement.Stop();  // 停止
            currentState = AIState.Idle;  // Idle状態へ遷移
            noPathTimer = 0f;  // タイマーリセット
            Debug.Log("MonsterGem: 原点復帰完了! [Return → Idle]");
        }
        else
        {
            // 原点への経路が空いているかチェック
            if (sensor.IsPathClear(spawnPosition))
            {
                noPathTimer = 0f;  // 経路あり、タイマーリセット

                // 待機時間経過後に移動
                idleTimer += Time.deltaTime;
                if (idleTimer >= idleTime)
                {
                    movement.MoveTo(spawnPosition);  // 原点へ移動
                    idleTimer = 0f;  // タイマーリセット
                }
            }
            else
            {
                // 経路なし、タイマー加算
                noPathTimer += Time.deltaTime;

                // 1秒に1回のみログ出力
                if (Time.time - lastReturnLogTime >= logInterval)
                {
                    Debug.LogWarning($"MonsterGem: 復帰経路なし! ({noPathTimer:F1}秒経過)");
                    lastReturnLogTime = Time.time;
                }

                // 一定時間経路がない場合、強制的にStuck状態へ遷移
                if (noPathTimer >= noPathThreshold)
                {
                    Debug.LogWarning("MonsterGem: 復帰経路長時間なし! 強制的にStuck状態遷移");
                    previousState = currentState;  // 復帰用に状態保存
                    currentState = AIState.Stuck;  // Stuck状態へ遷移
                    stuckTimer = 0f;  // タイマーリセット
                    stuckAttempts = 0;  // 試行回数リセット
                    noPathTimer = 0f;  // 経路なしタイマーリセット
                    isForcedStuck = true;  // 強制Stuck表示!
                }
            }
        }
    }

    // ========================================
    // Stuck状態: 引っかかり - CircleCast基盤に修正
    // ========================================

    void HandleStuck()
    {
        // テレポート中は別処理
        if (isTeleporting)
        {
            HandleTeleport();
            return;
        }

        // 強制Stuckの場合、脱出試行をスキップして直接テレポート準備
        if (isForcedStuck)
        {
            Debug.Log("MonsterGem: 強制Stuck状態! 脱出試行なしでテレポートへ直行...");
            isTeleporting = true;  // テレポートフラグON
            teleportTimer = 0f;  // タイマーリセット
            movement.Stop();  // 移動停止
            isForcedStuck = false;  // フラグリセット
            return;
        }

        stuckTimer += Time.deltaTime;  // タイマー加算

        // 設定された間隔ごとに脱出試行
        if (stuckTimer >= stuckRecoveryTime)
        {
            stuckAttempts++;  // 試行回数カウント

            // どの方向に壁があるか確認
            Vector2 stuckDirection = sensor.GetStuckDirection();

            // 壁方向が取得できた場合
            if (stuckDirection != Vector2.zero)
            {
                // 試行回数に比例して強度増加
                float forceMultiplier = 1f + (stuckAttempts * 0.5f);

                // 壁方向の逆方向へ脱出!
                movement.UnstuckInDirection(stuckDirection, forceMultiplier);

                Debug.Log($"脱出試行 {stuckAttempts}/{maxStuckAttempts}回 (壁方向: {stuckDirection}, 強度: {forceMultiplier}x)");
            }

            stuckTimer = 0f;  // タイマーリセット

            // 最大試行回数超過時、テレポート準備
            if (stuckAttempts >= maxStuckAttempts)
            {
                Debug.LogWarning($"脱出失敗! {teleportWarningTime}秒後に原位置へテレポートします...");
                isTeleporting = true;  // テレポートフラグON
                teleportTimer = 0f;  // タイマーリセット
                movement.Stop();  // 移動停止
            }
        }

        // 脱出確認: 壁から離れたか?
        if (!sensor.IsStuckToWall())
        {
            stuckAttempts = 0;  // 試行回数リセット
            noPathTimer = 0f;  // 経路なしタイマーリセット
            currentState = previousState;  // 元の状態へ復帰
            Debug.Log($"脱出完了! [Stuck → {previousState}]");
        }
    }

    // ========================================
    // テレポート処理 - 単純反転(連続なし)
    // ========================================

    void HandleTeleport()
    {
        teleportTimer += Time.deltaTime;  // カウントダウン加算

        // 1秒ごとにカウントログ
        if (Time.time - lastStuckLogTime >= 1f)
        {
            Debug.Log($"テレポートまで {teleportWarningTime - teleportTimer:F1}秒残り...");
            lastStuckLogTime = Time.time;
        }

        // 警告時間経過後、即座にテレポート!
        if (teleportTimer >= teleportWarningTime)
        {
            // 瞬間移動
            Vector3 oldPos = transform.position;
            transform.position = spawnPosition;

            Debug.Log($"MonsterGem: テレポート実行! [{oldPos} → {spawnPosition}]");

            // 状態即座に初期化
            isTeleporting = false;  // テレポートフラグOFF
            teleportTimer = 0f;  // タイマーリセット
            stuckAttempts = 0;  // 試行回数リセット
            noPathTimer = 0f;  // 経路なしタイマーリセット
            isForcedStuck = false;  // 強制Stuckフラグリセット
            currentState = AIState.Idle;  // Idle状態へ

            Debug.Log("MonsterGem: 状態初期化完了! [Stuck → Idle]");
        }
    }

    // ========================================
    // Leashチェック: 追跡範囲監視
    // ========================================

    void CheckLeash()
    {
        // 原点からの距離を計算
        float distanceFromSpawn = Vector2.Distance(transform.position, spawnPosition);

        // 最大追跡距離を超え、かつReturn状態でない場合
        if (distanceFromSpawn > maxChaseDistance && currentState != AIState.Return)
        {
            currentState = AIState.Return;  // Return状態へ遷移
            noPathTimer = 0f;  // 経路なしタイマーリセット
            Debug.Log("MonsterGem: 追跡放棄! [→ Return]");
        }
    }

    // ========================================
    // 後退位置計算
    // ========================================

    Vector2 CalculateRetreatPosition()
    {
        // プレイヤーから離れる方向を計算
        Vector2 directionFromPlayer = (transform.position - player.position).normalized;
        // 好ましい距離だけ離れた位置を計算
        Vector2 desiredPosition = (Vector2)player.position + directionFromPlayer * preferredDistance;

        // 経路が空いていない場合
        if (!sensor.IsPathClear(desiredPosition))
        {
            // 垂直方向を計算
            Vector2 perpendicularDir = Vector2.Perpendicular(directionFromPlayer);

            // 左方向の代替位置を試行
            Vector2 leftPosition = (Vector2)player.position + directionFromPlayer * (preferredDistance * 0.8f) + perpendicularDir * 2f;
            if (sensor.IsPathClear(leftPosition))
            {
                return leftPosition;
            }

            // 右方向の代替位置を試行
            Vector2 rightPosition = (Vector2)player.position + directionFromPlayer * (preferredDistance * 0.8f) - perpendicularDir * 2f;
            if (sensor.IsPathClear(rightPosition))
            {
                return rightPosition;
            }

            // 両方ダメなら最小距離で妥協
            float minDistance = preferredDistance - comfortZone;
            return (Vector2)player.position + directionFromPlayer * minDistance;
        }

        return desiredPosition;  // 理想位置を返す
    }

    // ========================================
    // 接近位置計算
    // ========================================

    Vector2 CalculateApproachPosition()
    {
        // プレイヤーへの方向を計算
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        // 好ましい距離だけ手前の位置を計算
        Vector2 desiredPosition = (Vector2)player.position - directionToPlayer * preferredDistance;

        // 経路が空いていない場合
        if (!sensor.IsPathClear(desiredPosition))
        {
            // 垂直方向を計算
            Vector2 perpendicularDir = Vector2.Perpendicular(directionToPlayer);

            // 左方向の代替位置を試行
            Vector2 leftPosition = (Vector2)transform.position + directionToPlayer * 2f + perpendicularDir * 2f;
            if (sensor.IsPathClear(leftPosition))
            {
                return leftPosition;
            }

            // 右方向の代替位置を試行
            Vector2 rightPosition = (Vector2)transform.position + directionToPlayer * 2f - perpendicularDir * 2f;
            if (sensor.IsPathClear(rightPosition))
            {
                return rightPosition;
            }

            // 両方ダメなら短距離で妥協
            return (Vector2)transform.position + directionToPlayer * 1.5f;
        }

        return desiredPosition;  // 理想位置を返す
    }

    // ========================================
    // Gizmosで視覚化
    // ========================================

    void OnDrawGizmosSelected()
    {
        // Leash範囲(オレンジ半透明)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Vector2 drawSpawnPos = Application.isPlaying ? spawnPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(drawSpawnPos, maxChaseDistance);

        // 原点位置(赤色球)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(spawnPosition, 0.5f);
        }

        // 現在状態表示(プレイ中のみ)
        if (Application.isPlaying)
        {
            string statusText = $"State: {currentState}";  // 状態表示
            if (isTeleporting)
            {
                statusText += $"\nTeleport: {teleportTimer:F1}s";  // テレポートカウント表示
            }
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, statusText);
        }
    }
}