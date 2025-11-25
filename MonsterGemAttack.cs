/*
 * ====================================================================================
 * スクリプト名: MonsterGemAttack (攻撃制御)
 * ====================================================================================
 * 
 * 【配置場所】
 * - MonsterGemの子オブジェクト「AttackPoint」にアタッチ
 * - MonsterGemAI、MonsterGemSensorと連携
 * 
 * 【参照スクリプト】
 * - MonsterGemSensor: プレイヤーが視界内にいるかチェック
 * - MonsterGemAI: 親オブジェクトから取得(将来の拡張用)
 * - Projectile: 生成する投射体Prefab
 * 
 * 【主な機能】
 * 1. プレイヤーが視界内かつ攻撃範囲内にいる場合に攻撃
 * 2. ランダムな攻撃間隔でタイミングに変化をつける
 * 3. 20%の確率で二連続攻撃を実行
 * 4. 照準にランダムなズレを追加して人間らしさを演出
 * 
 * 【動作フロー】
 * タイマー監視 → 視界・範囲チェック → 通常攻撃 or 二連攻撃 → Projectile発射
 * 
 * ====================================================================================
 */

using UnityEngine;
using System.Collections;

public class MonsterGemAttack : MonoBehaviour
{
    // ========================================
    // ターゲット
    // ========================================

    [Header("Target")]
    [SerializeField] private Transform player;  // プレイヤーのTransform

    // ========================================
    // 攻撃設定
    // ========================================

    [Header("Attack Settings")]
    [SerializeField] private GameObject projectilePrefab;  // 投射体Prefab
    [SerializeField] private Transform firePoint;  // 発射位置
    [SerializeField] private float attackRange = 15f;  // 攻撃範囲

    // ========================================
    // タイミング設定
    // ========================================

    [Header("Timing")]
    [SerializeField] private float minAttackDelay = 1f;  // 最小攻撃間隔
    [SerializeField] private float maxAttackDelay = 2.5f;  // 最大攻撃間隔
    [SerializeField] private float doubleAttackChance = 0.2f;  // 二連攻撃確率(20%)
    [SerializeField] private float doubleAttackInterval = 0.3f;  // 二連攻撃の間隔

    // ========================================
    // 投射体設定
    // ========================================

    [Header("Projectile Settings")]
    [SerializeField] private float throwForce = 10f;  // 投射力
    [SerializeField] private float aimRandomness = 15f;  // 照準ランダム角度(±度)

    // ========================================
    // 内部変数
    // ========================================

    private MonsterGemSensor sensor;  // 視界判定用
    private MonsterGemAI ai;  // AI参照(将来の拡張用)
    private float attackTimer = 0f;  // 経過時間カウント
    private float nextAttackTime;  // 次回攻撃までの時間

    // ========================================
    // ゲーム開始時に1回実行
    // ========================================

    void Start()
    {
        // 親オブジェクトからSensorコンポーネントを取得
        sensor = GetComponentInParent<MonsterGemSensor>();
        // 親オブジェクトからAIコンポーネントを取得
        ai = GetComponentInParent<MonsterGemAI>();

        // プレイヤーが未設定の場合、タグで検索
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        // 発射位置が未設定の場合、自分自身を使用
        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 初回攻撃時間をランダムに設定
        nextAttackTime = Random.Range(minAttackDelay, maxAttackDelay);

        // 初期化確認用デバッグログ
        Debug.Log($"MonsterGemAttack Start - Sensor: {sensor != null}, AI: {ai != null}, Player: {player != null}");
    }

    // ========================================
    // 毎フレーム更新
    // ========================================

    void Update()
    {
        // 必要なコンポーネントが欠けている場合は処理しない
        if (player == null || sensor == null || ai == null)
            return;

        // 壁の向こう側にいる場合は攻撃しない
        if (!sensor.CanSeePlayer())
            return;

        // プレイヤーまでの距離を計算
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        // 攻撃範囲外なら処理しない
        if (distanceToPlayer > attackRange)
            return;

        // 攻撃タイマーを加算
        attackTimer += Time.deltaTime;

        // 次回攻撃時間に到達したかチェック
        if (attackTimer >= nextAttackTime)
        {
            // 20%の確率で二連攻撃
            if (Random.value < doubleAttackChance)
            {
                StartCoroutine(DoubleAttack());
            }
            // 80%の確率で通常攻撃
            else
            {
                FireProjectile();
            }

            // タイマーをリセット
            attackTimer = 0f;
            // 次回攻撃時間をランダムに再設定
            nextAttackTime = Random.Range(minAttackDelay, maxAttackDelay);
        }
    }

    // ========================================
    // 投射体発射
    // ========================================

    void FireProjectile()
    {
        // 必須要素が欠けている場合は発射しない
        if (projectilePrefab == null || firePoint == null || player == null)
            return;

        // プレイヤー方向の単位ベクトルを計算
        Vector2 directionToPlayer = (player.position - firePoint.position).normalized;
        // ベクトルから角度を計算(ラジアン → 度数法)
        float angleToPlayer = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;

        // 照準にランダムなズレを追加(人間らしさ演出)
        float randomAngle = Random.Range(-aimRandomness, aimRandomness);
        // 最終的な発射角度を計算
        float finalAngle = angleToPlayer + randomAngle;

        // 角度から投射方向ベクトルを作成(度数法 → ラジアン)
        Vector2 throwDirection = new Vector2(
            Mathf.Cos(finalAngle * Mathf.Deg2Rad),
            Mathf.Sin(finalAngle * Mathf.Deg2Rad)
        );

        // 投射体を生成(発射位置、回転なし)
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        // 投射体のRigidbody2Dを取得
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        // Rigidbody2Dがあれば瞬間的な力を加えて発射
        if (rb != null)
        {
            rb.AddForce(throwDirection * throwForce, ForceMode2D.Impulse);
        }
    }

    // ========================================
    // 二連攻撃コルーチン
    // ========================================

    IEnumerator DoubleAttack()
    {
        FireProjectile();  // 1発目
        yield return new WaitForSeconds(doubleAttackInterval);  // 間隔待機
        FireProjectile();  // 2発目
    }

    // ========================================
    // Gizmosで視覚化
    // ========================================


    void OnDrawGizmosSelected()
    {
        // 攻撃範囲を赤色のワイヤー球で表示
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
