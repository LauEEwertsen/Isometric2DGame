using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.RuleTile.TilingRuleOutput;

enum EnemyState { Idle, Patrol, Chase, Attack }
enum IsoDirection { NE, NW, SW, SE }

public class EnemyAI : MonoBehaviour
{
    EnemyState currentState;

    [SerializeField] UnityEngine.Transform player;
    [SerializeField] float detectionRange = 3f;
    [SerializeField] float attackRange = 0.5f;
    [SerializeField] float speed = 2f;

    [SerializeField] Tilemap groundTilemap;

    [SerializeField] private UnityEngine.Transform visualBody;
    [SerializeField] GameObject idleIcon;
    [SerializeField] Rigidbody2D _rb;


    [SerializeField] UnityEngine.Transform[] patrolPoints;

    int patrolIndex = 0;

    float speedMultiplier = 1f;
    float defaultSpeedMultiplier = 1f;

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.Patrol:
                HandlePatrol();
                break;
            case EnemyState.Chase:
                HandleChase();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
        }

        if (currentState != EnemyState.Idle) idleIcon.SetActive(false);

        CheckTransitions();

        CheckCurrentTile();
    }

    void CheckTransitions()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= attackRange)
            currentState = EnemyState.Attack;
        else if (distance <= detectionRange)
            currentState = EnemyState.Chase;
        else if (patrolPoints.Length > 0)
            currentState = EnemyState.Patrol;
        else
            currentState = EnemyState.Idle;
    }

    void HandlePatrol()
    {
        Vector2 targetPos = patrolPoints[patrolIndex].position;
        Vector2 moveDir = (targetPos - (Vector2)transform.position).normalized;

        if (Vector2.Distance(transform.position, targetPos) < 0.1f)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;

        movingToTarget(moveDir, targetPos);
    }

    void HandleChase()
    {
        Vector2 moveDir = (player.position - transform.position).normalized;
        movingToTarget(moveDir, player.position);
    }

    void HandleAttack()
    {
        Debug.Log("Attacking!");
        // Will be used more in 4th task to reduce player HP here
    }

    void HandleIdle()
    {
        _rb.linearVelocity = Vector2.zero;
        idleIcon.SetActive(true);
    }

    void CheckCurrentTile()
    {
        Vector3 worldPos = transform.position;
        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
        TileBase tile = groundTilemap.GetTile(cellPos);

        if (tile is SpeedTile speedTile)
        {
            speedMultiplier = speedTile.speedMultiplier;
        }
        else
        {
            speedMultiplier = defaultSpeedMultiplier;
        }
    }

    void movingToTarget(Vector2 moveDir, Vector2 target)
    {

        //Vector2 isoDir = ToIsometricDirection(moveDir);

        //_rb.linearVelocity = moveDir * speed * speedMultiplier;
        FaceIsometricDirection(moveDir);


        transform.position = Vector2.MoveTowards(transform.position, target, speed * speedMultiplier * Time.deltaTime);
    }

    void FaceIsometricDirection(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float[] allowedAngles = { 45f, 135f, 225f, 315f };
        float closestAngle = allowedAngles
            .OrderBy(a => Mathf.Abs(Mathf.DeltaAngle(angle, a)))
            .First();

        visualBody.rotation = Quaternion.Euler(300f, 0f, closestAngle);
    }

    Vector2 ToIsometricDirection(Vector2 directionToTarget)
    {
        return new Vector2(
            directionToTarget.x + directionToTarget.y,
            (directionToTarget.y - directionToTarget.x) / 2f
        ).normalized;
    }
}


