using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 10f;
    [SerializeField] float lifetime = 2f;
    [SerializeField] int damage = 1;


    private Vector2 direction;

    public void SetDirection(Vector2 dir) => direction = dir;

    void Start() => Destroy(gameObject, lifetime);

    void Update() => transform.Translate(direction * speed * Time.deltaTime);

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Hit");
        if (other.CompareTag("Creature"))
        {
            Debug.Log("Hit Creature");
            other.GetComponentInParent<EnemyAI>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
