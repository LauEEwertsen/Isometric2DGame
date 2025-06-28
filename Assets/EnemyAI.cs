using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.RuleTile.TilingRuleOutput;

enum EnemyState { Idle, Patrol, Chase, Attack, Search}
//enum IsoDirection { NE, NW, SW, SE }

public class EnemyAI : MonoBehaviour
{
    EnemyState currentState;

    [SerializeField] UnityEngine.Transform player;
    [SerializeField] float detectionRange = 3f;
    [SerializeField] float attackRange = 0.5f;
    [SerializeField] float speed = 2f;

    [SerializeField] Tilemap groundTilemap;

    [SerializeField] UnityEngine.Transform visualBody;
    [SerializeField] GameObject idleIcon;
    [SerializeField] Rigidbody2D _rb;

    [SerializeField] float minIdleTime = 1f;
    [SerializeField] float maxIdleTime = 3f;
    float idleTimer = 0f;
    bool isWaiting = false;
    bool resumePatrolAfterIdle = false;

    [SerializeField] GameObject patrolParent;
    UnityEngine.Transform[] patrolPoints;

    int patrolIndex = 0;

    float speedMultiplier = 1f;
    float defaultSpeedMultiplier = 1f;

    Vector2? lastKnownPlayerPosition = null;
    bool goToLastKnown = false;

    private void Awake()
    {
        
    }
    private void Start()
    {
        resumePatrolAfterIdle = true;
        currentState = EnemyState.Idle;

        patrolPoints = patrolParent.GetComponentsInChildren<UnityEngine.Transform>()
            .Where(t => t != patrolParent.transform).ToArray().OrderBy(x => UnityEngine.Random.value).ToArray();

        foreach (var transform in patrolPoints)
        {
            Debug.Log(transform.name + " - " + transform.position);
        }
    }

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
            case EnemyState.Search:
                HandleSearch();
                break;
        }

        if (currentState != EnemyState.Idle) idleIcon.SetActive(false);

        CheckTransitions();

        CheckCurrentTile();
    }

    void CheckTransitions()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                if (checkIfDetectedPlayer())
                {
                    currentState = EnemyState.Chase;
                    isWaiting = false;
                }
                break;

            case EnemyState.Patrol:
                if (checkIfDetectedPlayer())
                {
                    currentState = EnemyState.Chase;
                }
                break;

            case EnemyState.Chase:
                if (distance <= attackRange)
                {
                    currentState = EnemyState.Attack;
                }
                else if (!checkIfDetectedPlayer())
                {
                    if (lastKnownPlayerPosition.HasValue)
                    {
                        currentState = EnemyState.Idle; // We’ll use idle as a short pause before moving
                        resumePatrolAfterIdle = false;
                        goToLastKnown = true;
                        isWaiting = false;
                    }
                    else
                    {
                        currentState = EnemyState.Idle;
                        resumePatrolAfterIdle = true;
                        isWaiting = false;
                    }
                }
                break;

            case EnemyState.Attack:
                if (distance > attackRange)
                {
                    currentState = EnemyState.Chase;
                }
                break;
        }
    }



    void HandlePatrol()
    {       
        Vector2 targetPos = patrolPoints[patrolIndex].position;
        Vector2 moveDir = (targetPos - (Vector2)transform.position).normalized;

        if (Vector2.Distance(transform.position, targetPos) < 0.1f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            currentState = EnemyState.Idle;
            resumePatrolAfterIdle = true;
            isWaiting = false;
            return;
        }

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

        if (!isWaiting)
        {
            idleTimer = UnityEngine.Random.Range(minIdleTime, maxIdleTime);
            isWaiting = true;
        }

        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            isWaiting = false;

            if (goToLastKnown && lastKnownPlayerPosition.HasValue)
            {
                currentState = EnemyState.Search;
                goToLastKnown = false;
                return;
            }

            if (resumePatrolAfterIdle && patrolPoints.Length > 0)
            {
                resumePatrolAfterIdle = false;
                currentState = EnemyState.Patrol;
            }
        }
    }

    void HandleSearch()
    {
        // Check if player is visible again
        if (checkIfDetectedPlayer())
        {
            currentState = EnemyState.Chase;
            goToLastKnown = false;
            return;
        }

        // If no player in sight, move toward last known position
        if (!lastKnownPlayerPosition.HasValue)
        {
            currentState = EnemyState.Idle;
            resumePatrolAfterIdle = true;
            return;
        }

        Vector2 target = lastKnownPlayerPosition.Value;
        Vector2 moveDir = (target - (Vector2)transform.position).normalized;
        movingToTarget(moveDir, target);

        if (Vector2.Distance(transform.position, target) < 0.1f)
        {
            lastKnownPlayerPosition = null;
            currentState = EnemyState.Idle;
            resumePatrolAfterIdle = true;
        }
    }

    bool checkIfDetectedPlayer()
    {
        Collider2D visualBodyCollider = visualBody.GetComponent<BoxCollider2D>();
        visualBodyCollider.enabled = false;

        Vector2 directionToPlayer = (player.position - visualBody.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(visualBody.transform.position, directionToPlayer, detectionRange);

        visualBodyCollider.enabled = true;

        Debug.Log(hit.distance);

        Debug.DrawRay(visualBody.position, directionToPlayer * detectionRange, Color.red); // For debugging in scene view

        if (hit.collider != null && hit.collider.CompareTag("Player")) 
        {
            lastKnownPlayerPosition = player.position;
            return true;
        }   
        return false;
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
}


