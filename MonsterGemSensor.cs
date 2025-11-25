/*
 * ====================================================================================
 * スクリプト名: MonsterGemSensor (感知・検知システム)
 * ====================================================================================
 * 
 * 【配置場所】
 * - MonsterGemのGameObjectにアタッチ
 * - MonsterGemAI、MonsterGemAttackと同じオブジェクトに配置
 * 
 * 【参照スクリプト】
 * - MonsterGemAI: このスクリプトのメソッドを呼び出して状態判定
 * - MonsterGemAttack: このスクリプトのCanSeePlayer()で視界チェック
 * 
 * 【主な機能】
 * 1. プレイヤーの感知範囲チェック
 * 2. 壁越しの視界判定(Raycast)
 * 3. 適正距離の判定(近すぎる/遠すぎる)
 * 4. 4方向CircleCastによるスタック検知
 * 5. ベクトル相殺問題の解決(複数方向同時検知時)
 * 
 * 【動作フロー】
 * AI呼び出し → 範囲・視界チェック → 距離判定 → スタック検知 → 方向返却
 * 
 * ====================================================================================
 */

using UnityEngine;

public class MonsterGemSensor : MonoBehaviour
{
    // ========================================
    // ターゲット
    // ========================================

    [Header("Target")]
    [SerializeField] private Transform player;  // プレイヤーのTransform

    // ========================================
    // 感知設定
    // ========================================

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 10f;  // 感知範囲
    [SerializeField] private float preferredDistance = 5f;  // 好ましい距離
    [SerializeField] private float comfortZone = 2f;  // 快適ゾーン(±範囲)

    // ========================================
    // 障害物検知
    // ========================================

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleLayer;  // 障害物レイヤー
    [SerializeField] private float wallCheckDistance = 1f;  // 壁チェック距離

    // ========================================
    // スタック検知 - CircleCast方式
    // ========================================

    [Header("Stuck Detection - CircleCast")]
    [SerializeField] private float stuckDistance = 0.15f;  // 壁に接触と判断する距離
    [SerializeField] private float checkRadius = 0.3f;  // チェックする円の半径

    // ========================================
    // 内部変数 - ログ制御
    // ========================================

    private float lastLogTime = 0f;  // 最後にログを出力した時間
    private float logInterval = 1f;  // ログ出力間隔(1秒に1回)

    // ========================================
    // プレイヤーが感知範囲内にいるかチェック
    // ========================================

    public bool IsPlayerInRange()
    {
        // プレイヤーまでの距離を計算
        float distance = Vector2.Distance(transform.position, player.position);
        // 感知範囲内ならtrue
        return distance <= detectionRange;
    }

    // ========================================
    // プレイヤーが視界内にいるかチェック(壁越し判定)
    // ========================================

    public bool CanSeePlayer()
    {
        // 感知範囲外ならfalse
        if (!IsPlayerInRange())
            return false;

        // プレイヤー方向の単位ベクトルを計算
        Vector2 direction = (player.position - transform.position).normalized;
        // プレイヤーまでの距離を計算
        float distance = Vector2.Distance(transform.position, player.position);

        // 自分からプレイヤーまでの間に壁があるかRaycastでチェック
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            direction,
            distance,
            obstacleLayer
        );

        // 壁に当たらなければtrue(視界内)
        return hit.collider == null;
    }

    // ========================================
    // プレイヤーが近すぎるかチェック
    // ========================================

    public bool IsTooClose()
    {
        // プレイヤーまでの距離を計算
        float distance = Vector2.Distance(transform.position, player.position);
        // 好ましい距離 - 快適ゾーン より近いならtrue
        return distance < (preferredDistance - comfortZone);
    }

    // ========================================
    // プレイヤーが遠すぎるかチェック
    // ========================================

    public bool IsTooFar()
    {
        // プレイヤーまでの距離を計算
        float distance = Vector2.Distance(transform.position, player.position);
        // 好ましい距離 + 快適ゾーン より遠いならtrue
        return distance > (preferredDistance + comfortZone);
    }

    // ========================================
    // 指定方向に壁があるかチェック
    // ========================================

    public bool IsWallAhead(Vector2 direction)
    {
        // 指定方向にRaycastを発射
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            direction,
            wallCheckDistance,
            obstacleLayer
        );

        // 壁に当たればtrue
        return hit.collider != null;
    }

    // ========================================
    // 目標位置までの経路が空いているかチェック
    // ========================================

    public bool IsPathClear(Vector2 targetPosition)
    {
        // 目標位置に障害物があるかCircleでチェック
        Collider2D targetCheck = Physics2D.OverlapCircle(
            targetPosition,
            0.3f,
            obstacleLayer
        );

        // 目標位置自体に障害物があればfalse
        if (targetCheck != null)
            return false;

        // 目標方向の単位ベクトルを計算
        Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
        // 目標までの距離を計算
        float distance = Vector2.Distance(transform.position, targetPosition);

        // 自分から目標まで間に壁があるかRaycastでチェック
        RaycastHit2D pathCheck = Physics2D.Raycast(
            transform.position,
            direction,
            distance,
            obstacleLayer
        );

        // 壁に当たらなければtrue(経路クリア)
        return pathCheck.collider == null;
    }

    // ========================================
    // プレイヤーまでの距離を取得
    // ========================================

    public float GetDistanceToPlayer()
    {
        return Vector2.Distance(transform.position, player.position);
    }

    // ========================================
    // CircleCast方式: 4方向チェック後、接触方向を返却
    // ========================================
    // ベクトル相殺問題の解決策を含む

    public Vector2 GetStuckDirection()
    {
        // チェックする4方向を定義
        Vector2[] directions = new Vector2[]
        {
            Vector2.up,     // 上
            Vector2.down,   // 下
            Vector2.left,   // 左
            Vector2.right   // 右
        };

        Vector2 totalStuckDirection = Vector2.zero;  // 接触方向の合成ベクトル
        int stuckCount = 0;  // 接触した方向の数
        bool shouldLog = Time.time - lastLogTime >= logInterval;  // ログ出力タイミング

        string detectedDirections = "";  // デバッグ用: 検知された方向リスト

        // 各方向をCircleCastでチェック
        foreach (Vector2 dir in directions)
        {
            // 円形の当たり判定を指定方向に飛ばす
            RaycastHit2D hit = Physics2D.CircleCast(
                transform.position,
                checkRadius,
                dir,
                stuckDistance,
                obstacleLayer
            );

            // この方向に壁があれば
            if (hit.collider != null)
            {
                totalStuckDirection += dir;  // 方向ベクトルを加算
                stuckCount++;  // カウント増加

                // ログ出力タイミングなら方向を記録
                if (shouldLog)
                {
                    detectedDirections += $"{dir} ";
                }
            }
        }

        // 1秒に1回、検知されたすべての方向を一度に出力
        if (shouldLog && stuckCount > 0)
        {
            Debug.Log($"壁検知! {stuckCount}方向 - {detectedDirections}| 合成: {totalStuckDirection}");
            lastLogTime = Time.time;
        }

        // 重要: stuckCountが0より大きければ無条件でstuck(ベクトル相殺問題解決)
        if (stuckCount > 0)
        {
            // 合成ベクトルが(0,0)ならランダム方向を使用
            if (totalStuckDirection.magnitude < 0.1f)
            {
                Debug.LogWarning($"壁{stuckCount}方向検知! ベクトル相殺発生 → ランダム脱出");
                return Random.insideUnitCircle.normalized;
            }

            // 合成ベクトルを正規化して返す(壁の方向)
            return totalStuckDirection.normalized;
        }

        return Vector2.zero;  // 壁なし
    }

    // ========================================
    // 簡易チェック用(AIで使用)
    // ========================================
    // ベクトル相殺問題を完全回避

    public bool IsStuckToWall()
    {
        // チェックする4方向を定義
        Vector2[] directions = new Vector2[]
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        // 各方向をCircleCastでチェック
        foreach (Vector2 dir in directions)
        {
            RaycastHit2D hit = Physics2D.CircleCast(
                transform.position,
                checkRadius,
                dir,
                stuckDistance,
                obstacleLayer
            );

            // 1方向でも壁に接触していればtrue
            if (hit.collider != null)
            {
                return true;
            }
        }

        return false;  // どの方向も壁なし
    }

    // ========================================
    // Gizmosで視覚化
    // ========================================

    void OnDrawGizmosSelected()
    {
        if (player == null)
            return;

        // 感知範囲(黄色)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 好ましい距離(緑色)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);

        // 快適ゾーン(半透明緑色)
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, preferredDistance - comfortZone);
        Gizmos.DrawWireSphere(transform.position, preferredDistance + comfortZone);

        // プレイヤーとの線(視界内:シアン、壁越し:赤)
        if (IsPlayerInRange())
        {
            if (CanSeePlayer())
                Gizmos.color = Color.cyan;
            else
                Gizmos.color = Color.red;

            Gizmos.DrawLine(transform.position, player.position);
        }

        // CircleCast 4方向チェック範囲表示(シアン)
        Gizmos.color = Color.cyan;

        Vector2[] directions = new Vector2[]
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        // 各方向のチェック範囲を描画
        foreach (Vector2 dir in directions)
        {
            Vector2 endPos = (Vector2)transform.position + dir * stuckDistance;

            Gizmos.DrawWireSphere(endPos, checkRadius);  // 円形範囲
            Gizmos.DrawLine(transform.position, endPos);  // 中心から線
        }

        // 現在接触している方向表示(プレイ中のみ)
        if (Application.isPlaying)
        {
            Vector2 stuckDir = GetStuckDirection();
            if (stuckDir != Vector2.zero)
            {
                // 接触方向(赤色)
                Gizmos.color = Color.red;
                Gizmos.DrawLine(
                    transform.position,
                    (Vector2)transform.position + stuckDir * 1f
                );

                // 脱出方向(緑色)
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    transform.position,
                    (Vector2)transform.position - stuckDir * 1.5f
                );
            }
        }
    }
}