using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private int maxBounces = 3;
    [SerializeField] private float bounceHeightMultiplier = 0.4f;
    [SerializeField] private float velocityDamping = 0.7f;

    [Header("Collision")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Damage")]
    [SerializeField] private int damage = 10;

    private Rigidbody2D rb;
    private int currentBounces = 0;
    private bool hasLanded = false;
    private float lifetime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        lifetime += Time.deltaTime;
        if (lifetime >= maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    // Triggerでプレイヤー検知
    void OnTriggerEnter2D(Collider2D other)
    {
        // Playerタグチェック
        if (other.CompareTag("Player"))
        {
            // プレイヤーのダメージ処理関数呼び出し
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            // 投射体を即座に消滅
            Destroy(gameObject);
        }
    }

    // Groundでのみ物理衝突
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Ground Layerと衝突した場合
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            currentBounces++;

            // 最大バウンス回数に達したら消滅
            if (currentBounces >= maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            // 最初の着地
            if (!hasLanded)
            {
                Vector2 bounceVelocity = rb.linearVelocity;
                bounceVelocity.y = Mathf.Abs(bounceVelocity.y) * bounceHeightMultiplier;
                bounceVelocity.x *= velocityDamping;
                rb.linearVelocity = bounceVelocity;

                hasLanded = true;
            }
            // 2回目以降の着地
            else
            {
                Vector2 bounceVelocity = rb.linearVelocity;
                bounceVelocity.y = Mathf.Abs(bounceVelocity.y) * 0.5f;
                bounceVelocity.x *= velocityDamping;
                rb.linearVelocity = bounceVelocity;
            }
        }
        // Ground以外(壁など)と衝突した場合
        else
        {
            currentBounces++;

            // 最大バウンス回数に達したら消滅
            if (currentBounces >= maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            // 減速のみ適用
            rb.linearVelocity *= velocityDamping;
        }
    }
}
