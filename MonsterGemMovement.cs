/*
 * ====================================================================================
 * スクリプト名: MonsterGemMovement (移動制御)
 * ====================================================================================
 * 
 * 【配置場所】
 * - MonsterGemのGameObjectにアタッチ
 * - MonsterGemAIと同じオブジェクトに配置
 * 
 * 【参照スクリプト】
 * - MonsterGemAI: このスクリプトのメソッドを呼び出して移動を制御
 * 
 * 【主な機能】
 * 1. 目標地点への移動処理
 * 2. スムーズな停止処理
 * 3. スタック時の脱出処理(力を加えて押し出す)
 * 4. 移動方向に応じた左右反転
 * 
 * 【動作フロー】
 * AI呼び出し → MoveTo(目標設定) → MoveToTarget(移動実行) → Stop(停止)
 * スタック時 → UnstuckInDirection(脱出方向に力を加える)
 * 
 * ====================================================================================
 */

using UnityEngine;

public class MonsterGemMovement : MonoBehaviour
{
    // ========================================
    // 移動設定
    // ========================================

    [Header("Movement Settings")]

    // 移動速度 (使用箇所: MoveToTarget)
    [SerializeField] private float moveSpeed = 3f;

    // 加速度 (使用箇所: MoveToTarget, SmoothStop)
    [SerializeField] private float acceleration = 5f;

    // ========================================
    // 内部変数
    // ========================================

    // Rigidbody2Dへの参照 (使用箇所: Start, 全移動メソッド)
    private Rigidbody2D rb;

    // 目標位置 (使用箇所: MoveTo, MoveToTarget)
    private Vector2 targetPosition;

    // 移動中フラグ (使用箇所: Update, MoveTo, Stop, MoveToTarget)
    private bool isMoving = false;

    // 初期スケール値 (使用箇所: Start, UpdateFacingDirection)
    private Vector3 originalScale;

    // ========================================
    // 初期化
    // ========================================

    void Start()
    {
        // Rigidbody2Dコンポーネントを取得
        rb = GetComponent<Rigidbody2D>();
        // 重力を無効化(横スクロールゲームで空中浮遊)
        rb.gravityScale = 0f;
        // 左右反転用に初期スケールを保存
        originalScale = transform.localScale;
    }

    // ========================================
    // 毎フレーム更新
    // ========================================

    void Update()
    {
        // 移動中の場合
        if (isMoving)
        {
            // 目標地点へ移動
            MoveToTarget();
        }
        // 停止中の場合
        else
        {
            // 滑らかに停止
            SmoothStop();
        }
    }

    // ========================================
    // AIから呼び出し: 特定位置へ移動
    // ========================================

    public void MoveTo(Vector2 target)
    {
        // 目標位置を設定
        targetPosition = target;
        // 移動フラグをON
        isMoving = true;
    }

    // ========================================
    // AIから呼び出し: 停止
    // ========================================

    public void Stop()
    {
        // 移動フラグをOFF(SmoothStopが自動実行される)
        isMoving = false;
    }

    // ========================================
    // スタック脱出: 特定方向へ力を加えて押し出す
    // ========================================
    // MonsterGemAIのCircleCastで検知した障害物方向の逆に力を加える

    public void UnstuckInDirection(Vector2 stuckDirection, float multiplier = 1f)
    {
        // 壁の方向の反対 = 脱出方向
        Vector2 escapeDirection = -stuckDirection;

        // 完全に正反対だけだと単調なので角度にランダム性を追加(±15度)
        float randomAngle = Random.Range(-15f, 15f);
        // 脱出方向をランダム角度で回転
        escapeDirection = Rotate(escapeDirection, randomAngle);

        // 速度を初期化して瞬間的な力を加える
        rb.linearVelocity = Vector2.zero;
        // Impulseモードで瞬間的に力を加える(multiplierで強度調整)
        rb.AddForce(escapeDirection * 10f * multiplier, ForceMode2D.Impulse);

        // デバッグログで脱出情報を出力
        Debug.Log($"脱出! 壁の方向: {stuckDirection}, 脱出方向: {escapeDirection}, 強度: {multiplier}x");

        // 一時的に移動を停止(脱出の力だけで動く)
        isMoving = false;
    }

    // ========================================
    // ベクトル回転ヘルパー関数
    // ========================================

    private Vector2 Rotate(Vector2 v, float degrees)
    {
        // 度数法をラジアンに変換
        float radians = degrees * Mathf.Deg2Rad;
        // 回転行列の要素を計算
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        // 2D回転行列を適用して新しいベクトルを返す
        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    // ========================================
    // 目標地点まで移動
    // ========================================

    void MoveToTarget()
    {
        // 現在地から目標地点までの距離を計算
        float distanceToTarget = Vector2.Distance(transform.position, targetPosition);

        // 目標地点に十分近づいたら停止
        if (distanceToTarget < 1.0f)
        {
            isMoving = false;
            return;
        }

        // 目標方向の単位ベクトルを計算
        Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
        // 目標速度を計算(方向 × 速度)
        Vector2 desiredVelocity = direction * moveSpeed;
        // 現在速度から目標速度へ滑らかに補間(加速度を適用)
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.deltaTime * acceleration);

        // 移動方向に応じて左右反転
        UpdateFacingDirection();
    }

    // ========================================
    // 滑らかに停止
    // ========================================

    void SmoothStop()
    {
        // 現在速度をゼロへ滑らかに補間(慣性で自然に停止)
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * acceleration);
    }

    // ========================================
    // 移動方向に応じて左右反転
    // ========================================

    void UpdateFacingDirection()
    {
        // 左方向に移動中
        if (rb.linearVelocity.x < -0.1f)
        {
            // X軸を負の値にして左向き(絶対値で元のサイズを維持)
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }
        // 右方向に移動中
        else if (rb.linearVelocity.x > 0.1f)
        {
            // X軸を正の値にして右向き
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }
        // -0.1f ~ 0.1fの範囲はほぼ静止状態なので反転しない
    }

    // ========================================
    // Gizmosで視覚化
    // ========================================

    void OnDrawGizmosSelected()
    {
        // 移動中の場合のみ描画
        if (isMoving)
        {
            // 目標地点をシアン色の球で表示
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(targetPosition, 0.3f);

            // 現在地から目標地点への線を青色で表示
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
}