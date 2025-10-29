using UnityEngine;
using System.Collections;

public class MonsterGemAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint; // 発射位置（自分自身）

    [Header("Attack Settings")]
    [SerializeField] private float minAttackDelay = 1f;
    [SerializeField] private float maxAttackDelay = 2.5f;
    [SerializeField] private float attackRange = 15f; // 攻撃可能距離

    [Header("Projectile Settings")]
    [SerializeField] private float throwForce = 10f;
    [SerializeField] private float aimRandomness = 15f; // 照準誤差（±度）

    [Header("Double Attack")]
    [SerializeField] private float doubleAttackChance = 0.2f; // 20%の確率
    [SerializeField] private float doubleAttackInterval = 0.3f; // 連続攻撃の間隔

    private float attackTimer = 0f;
    private float nextAttackTime = 0f;

    void Start()
    {
        nextAttackTime = Random.Range(minAttackDelay, maxAttackDelay);
    }

    void Update()
    {
        attackTimer += Time.deltaTime;

        if (attackTimer >= nextAttackTime)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // 攻撃範囲内にいれば攻撃
            if (distanceToPlayer <= attackRange)
            {
                // 連続攻撃の確率チェック
                if (Random.value <= doubleAttackChance)
                {
                    StartCoroutine(DoubleAttack());
                }
                else
                {
                    FireProjectile();
                }
            }

            // 次の攻撃時間設定
            attackTimer = 0f;
            nextAttackTime = Random.Range(minAttackDelay, maxAttackDelay);
        }
    }

    void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        // プレイヤー方向計算
        Vector2 directionToPlayer = (player.position - transform.position).normalized;

        // ランダム誤差追加
        float randomAngle = Random.Range(-aimRandomness, aimRandomness);
        float angleToPlayer = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
        float finalAngle = angleToPlayer + randomAngle;

        // 投射体生成
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            // 放物線投げ
            Vector2 throwDirection = new Vector2(
                Mathf.Cos(finalAngle * Mathf.Deg2Rad),
                Mathf.Sin(finalAngle * Mathf.Deg2Rad)
            );

            rb.linearVelocity = throwDirection * throwForce;
        }
    }

    IEnumerator DoubleAttack()
    {
        FireProjectile();
        yield return new WaitForSeconds(doubleAttackInterval);
        FireProjectile();
    }

    // 攻撃範囲の可視化
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
