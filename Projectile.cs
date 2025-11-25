/*
 * ====================================================================================
 * スクリプト名: Projectile (投射体)
 * ====================================================================================
 * 
 * 【配置場所】
 * - MonsterGemの子オブジェクト「AttackPoint」にアタッチ
 * - 攻撃時に生成される投射体Prefabに使用
 * 
 * 【参照スクリプト】
 * - PlayerHealth: プレイヤーへのダメージ処理
 * 
 * 【主な機能】
 * 1. 地面との衝突でバウンド動作
 * 2. プレイヤーとの接触でダメージ付与
 * 3. 最大バウンド回数到達で自動消滅
 * 4. 時間経過による自動消滅
 * 
 * 【動作フロー】
 * 生成 → 飛行 → 地面衝突(バウンド) → プレイヤー接触(ダメージ) → 消滅
 * 
 * ====================================================================================
 */

using UnityEngine;

public class Projectile : MonoBehaviour
{
    // ========================================
    // バウンド設定
    // ========================================

    [Header("Bounce Settings")]

    // 最大バウンド回数 (使用箇所: OnCollisionEnter2D)
    // この回数に達すると投射体が消滅
    [SerializeField] private int maxBounces = 3;

    // 初回バウンド時の高さ倍率 (使用箇所: OnCollisionEnter2D - 初回着地処理)
    // 元の速度のY成分に掛けて跳ね返り高さを決定
    [SerializeField] private float bounceHeightMultiplier = 0.4f;

    // バウンド時の速度減衰率 (使用箇所: OnCollisionEnter2D)
    // X軸速度に掛けて徐々に減速させる
    [SerializeField] private float velocityDamping = 0.7f;

    // ========================================
    // 衝突判定
    // ========================================

    [Header("Collision")]

    // 地面レイヤー (使用箇所: OnCollisionEnter2D)
    // このレイヤーとの衝突時のみバウンド処理を実行
    [SerializeField] private LayerMask groundLayer;

    // ========================================
    // 生存時間
    // ========================================

    [Header("Lifetime")]

    // 最大生存時間(秒) (使用箇所: Update)
    // この時間を超えると自動的に消滅
    [SerializeField] private float maxLifetime = 5f;

    // ========================================
    // ダメージ
    // ========================================

    [Header("Damage")]

    // プレイヤーに与えるダメージ量 (使用箇所: OnTriggerEnter2D)
    [SerializeField] private int damage = 10;

    // ========================================
    // 内部変数
    // ========================================

    // Rigidbody2Dへの参照 (使用箇所: Start, OnCollisionEnter2D)
    // バウンド時の速度制御に使用
    private Rigidbody2D rb;

    // 現在のバウンド回数 (使用箇所: OnCollisionEnter2D)
    // maxBouncesと比較して消滅判定
    private int currentBounces = 0;

    // 初回着地フラグ (使用箇所: OnCollisionEnter2D)
    // 初回と2回目以降でバウンド高さを変える
    private bool hasLanded = false;

    // 経過時間(秒) (使用箇所: Update)
    // maxLifetimeと比較して消滅判定
    private float lifetime = 0f;

    // ========================================
    // ゲーム開始時に1回実行
    // ========================================

    void Start()
    {
        // Rigidbody2Dコンポーネントを取得
        // バウンド処理で速度を制御するために必要
        rb = GetComponent<Rigidbody2D>();
    }

    // ========================================
    // 毎フレーム更新
    // ========================================

    void Update()
    {
        // 経過時間を加算
        // Time.deltaTime = 前フレームからの経過時間
        lifetime += Time.deltaTime;

        // 最大生存時間に達したかチェック
        if (lifetime >= maxLifetime)
        {
            // 投射体を破棄して消滅
            Destroy(gameObject);
        }
    }

    // ========================================
    // プレイヤー検知 (Trigger方式)
    // ========================================
    // AttackPointのCollider2DがTriggerに設定されている場合に動作
    // プレイヤーとの接触時にダメージを与えて消滅

    void OnTriggerEnter2D(Collider2D other)
    {
        // 接触したオブジェクトのタグが「Player」かチェック
        if (other.CompareTag("Player"))
        {
            // プレイヤーのHealthコンポーネントを取得
            // PlayerHealthスクリプトが必要
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            // Healthコンポーネントが存在する場合
            if (playerHealth != null)
            {
                // プレイヤーにダメージを与える
                // damage変数の値を渡す
                playerHealth.TakeDamage(damage);
            }

            // ダメージ付与後、投射体を即座に消滅
            // プレイヤーに当たったら役目終了
            Destroy(gameObject);
        }
    }

    // ========================================
    // 地面・壁との衝突処理 (物理衝突方式)
    // ========================================
    // AttackPointのCollider2DがTriggerではない場合に動作
    // 地面でバウンド、壁で速度減衰

    void OnCollisionEnter2D(Collision2D collision)
    {
        // ========================================
        // 【地面との衝突】バウンド処理
        // ========================================

        // 衝突したオブジェクトのレイヤーがgroundLayerに含まれるかチェック
        // ビット演算: (1 << layer) でレイヤーをビットマスクに変換
        // & groundLayer で含まれているか判定
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            // バウンド回数をカウント
            currentBounces++;

            // 最大バウンド回数に到達したかチェック
            if (currentBounces >= maxBounces)
            {
                // 最大回数到達で投射体を消滅
                Destroy(gameObject);
                return; // 以降の処理をスキップ
            }

            // ----------------------------------------
            // 初回着地時のバウンド処理
            // ----------------------------------------
            if (!hasLanded)
            {
                // 現在の速度を取得
                Vector2 bounceVelocity = rb.linearVelocity;

                // Y軸(高さ): 絶対値化 → bounceHeightMultiplierを掛けて跳ね返り高さを決定
                // Mathf.Abs()で常に正の値にして上向きにバウンド
                bounceVelocity.y = Mathf.Abs(bounceVelocity.y) * bounceHeightMultiplier;

                // X軸(水平): velocityDampingを掛けて減速
                // 0.7倍することで徐々に失速
                bounceVelocity.x *= velocityDamping;

                // 計算した速度をRigidbody2Dに適用
                rb.linearVelocity = bounceVelocity;

                // 初回着地フラグをtrueに変更
                // 次回から2回目以降の処理に移行
                hasLanded = true;
            }
            // ----------------------------------------
            // 2回目以降の着地時のバウンド処理
            // ----------------------------------------
            else
            {
                // 現在の速度を取得
                Vector2 bounceVelocity = rb.linearVelocity;

                // Y軸(高さ): 初回より低い跳ね返り(0.5倍)
                // バウンドするたびに低くなっていく
                bounceVelocity.y = Mathf.Abs(bounceVelocity.y) * 0.5f;

                // X軸(水平): velocityDampingで減速
                bounceVelocity.x *= velocityDamping;

                // 計算した速度をRigidbody2Dに適用
                rb.linearVelocity = bounceVelocity;
            }
        }
        // ========================================
        // 【壁などとの衝突】速度減衰のみ
        // ========================================
        else
        {
            // バウンド回数をカウント
            // 壁に当たった場合もカウント
            currentBounces++;

            // 最大バウンド回数に到達したかチェック
            if (currentBounces >= maxBounces)
            {
                // 最大回数到達で投射体を消滅
                Destroy(gameObject);
                return; // 以降の処理をスキップ
            }

            // 速度全体にvelocityDampingを掛けて減速
            // X軸、Y軸ともに減速して失速感を出す
            rb.linearVelocity *= velocityDamping;
        }
    }

}
