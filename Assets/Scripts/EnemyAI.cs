using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.RuleTile.TilingRuleOutput;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.UI;

enum EnemyState { Idle = 0, Patrol = 1, Chase = 2, Attack = 3, Search = 4 }


public class EnemyAI : MonoBehaviour
{
    EnemyState currentState;

    [Header("Basic AI Properties")]
    [SerializeField] float detectionRange = 3f;
    [SerializeField] float attackRange = 0.5f;
    float normalSpeed = 2f;
    float baseSpeed = 2f;
    [SerializeField] float speed = 2f;
    [SerializeField] float patrolSpeed = 1f;
    [SerializeField] float chaseSpeed = 3f;
    [SerializeField] UnityEngine.Transform player;
    [SerializeField] int maxHealth = 3;
    int currentHealth;
    [SerializeField] private Slider healthSlider;
    float defaultSpeedMultiplier = 1f;
    float lastAttackTime;
    [SerializeField] float attackCooldown = 1f;

    [SerializeField] float abilityCooldown = 8f;
    [SerializeField] float burstSpeed = 6f;
    float lastAbilityTime;

    Animator animator;

    [Header("Visual Indicators")]
    [SerializeField] UnityEngine.Transform visualBody;
    [SerializeField] GameObject idleIcon;
    [SerializeField] GameObject Nose;


    [Header("Idle Interactions")]
    [SerializeField] float minIdleTime = 1f;
    [SerializeField] float maxIdleTime = 3f;

    float idleTimer = 0f;
    bool isWaiting = false;
    bool resumePatrolAfterIdle = false;


    [Header("Patrol")]
    [SerializeField] GameObject patrolParent;

    UnityEngine.Transform[] patrolPoints;
    int patrolIndex = 0;


    [Header("Pathfinding")]
    [SerializeField] Tilemap groundTilemap;
    NavMeshAgent agent;
    Vector2? lastKnownPlayerPosition = null;
    bool goToLastKnown = false;

    private void Awake()
    {
        
    }
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        resumePatrolAfterIdle = true;

        patrolPoints = patrolParent.GetComponentsInChildren<UnityEngine.Transform>()
            .Where(t => t != patrolParent.transform).ToArray().OrderBy(x => UnityEngine.Random.value).ToArray();

        animator = visualBody.GetComponent<Animator>();
        SetAnimatorState(EnemyState.Idle);

        currentHealth = maxHealth;
    }

    private void Update()
    {
        healthSlider.value = (float)currentHealth / maxHealth;
    }

    private void FixedUpdate()
    {
        if (Time.time > lastAbilityTime + abilityCooldown && currentState == EnemyState.Chase)
        {
            Debug.Log("Activates Burst Speed");
            baseSpeed = burstSpeed;
            lastAbilityTime = Time.time;
            StartCoroutine(ResetSpeed());
        }

        switch (currentState)
        {
            case EnemyState.Idle:
                SetAnimatorState(EnemyState.Idle);
                HandleIdle();
                break;
            case EnemyState.Patrol:
                SetAnimatorState(EnemyState.Patrol);
                HandlePatrol();
                break;
            case EnemyState.Chase:
                SetAnimatorState(EnemyState.Chase);
                HandleChase();
                break;
            case EnemyState.Attack:
                SetAnimatorState(EnemyState.Attack);
                HandleAttack();
                break;
            case EnemyState.Search:
                SetAnimatorState(EnemyState.Search);
                HandleSearch();
                break;
        }

        if (currentState != EnemyState.Idle) idleIcon.SetActive(false);

        if (agent.hasPath && agent.remainingDistance > 0.05f)
        {
            Vector2 moveDir = (agent.steeringTarget - transform.position).normalized;
            FaceIsometricDirection(moveDir);
        }

        agent.speed = speed;

        CheckCurrentTile();
        CheckTransitions();
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
                        currentState = EnemyState.Search; //Use idle as short pause
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

        if (Vector2.Distance(transform.position, targetPos) < 0.1f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            currentState = EnemyState.Idle;
            resumePatrolAfterIdle = true;
            isWaiting = false;
            return;
        }

        movingToTarget(targetPos, patrolSpeed);
    }


    void HandleChase()
    {
        movingToTarget(player.position, chaseSpeed);
    }

    void HandleAttack()
    {
        if (Time.time > lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            Debug.Log("Attacking!");
            player.GetComponent<PlayerController>()?.TakeDamage(1);
            
        }           
    }

    void HandleIdle()
    {
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

            if (resumePatrolAfterIdle && patrolParent != null)
            {
                LoadAndShufflePatrolPoints();
                resumePatrolAfterIdle = false;
                currentState = EnemyState.Patrol;
            }
        }
    }

    void HandleSearch()
    {
        // Reacquire player
        if (checkIfDetectedPlayer())
        {
            currentState = EnemyState.Chase;
            goToLastKnown = false;
            return;
        }

        if (!lastKnownPlayerPosition.HasValue)
        {
            currentState = EnemyState.Idle;
            resumePatrolAfterIdle = true;
            return;
        }

        Vector2 target = lastKnownPlayerPosition.Value;

        // Move toward last known position
        movingToTarget(target, chaseSpeed);

        // If reached destination, start idle timer
        if (Vector2.Distance(transform.position, target) < 0.1f)
        {
            lastKnownPlayerPosition = null;
            goToLastKnown = false;
            currentState = EnemyState.Idle;
            resumePatrolAfterIdle = true;
            isWaiting = false;
        }
    }

    bool checkIfDetectedPlayer()
    {
        //Collider2D visualBodyCollider = visualBody.GetComponent<BoxCollider2D>();
        //visualBodyCollider.enabled = false;

        Vector2 directionToPlayer = (player.position - visualBody.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(Nose.transform.position, directionToPlayer, detectionRange);

        //visualBodyCollider.enabled = true;

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
        worldPos.z = 0;  // Ensure it's the correct tilemap slice
        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
        TileBase tile = groundTilemap.GetTile(cellPos);

        if (tile is SpeedTile speedTile)
        {
            speed = baseSpeed * speedTile.speedMultiplier;
        }
        else
        {
            speed = baseSpeed * defaultSpeedMultiplier; 
        }
    }


    void movingToTarget(Vector2 target, float overrideSpeed = -1f)
    {
        baseSpeed = overrideSpeed;

        agent.SetDestination(target);
    }

    void FaceIsometricDirection(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Snap to closest isometric direction (45°, 135°, etc.)
        float[] allowedAngles = { 45f, 135f, 225f, 315f };
        float closestAngle = allowedAngles
            .OrderBy(a => Mathf.Abs(Mathf.DeltaAngle(angle, a)))
            .First();

        visualBody.rotation = Quaternion.Euler(300f, 0f, closestAngle);
    }

    private void LoadAndShufflePatrolPoints()
    {
        patrolPoints = patrolParent.GetComponentsInChildren<UnityEngine.Transform>()
            .Where(t => t != patrolParent.transform) // Exclude the parent
            .OrderBy(x => UnityEngine.Random.value) // Randomize order
            .ToArray();

        patrolIndex = 0;
    }

    void SetAnimatorState(EnemyState state)
    {
        if (animator != null)
            animator.SetInteger("AIState", (int)state);
    }

    public void TakeDamage(int damage)
    {
        Debug.Log("Enemy hit");
        currentHealth -= damage;
        if (currentHealth <= 0) Die();
        else
            lastKnownPlayerPosition = player.position;
            HandleSearch();

    }

    void Die()
    {
        Debug.Log("Enemy died");
        // Optional: trigger animation or particle here
        //Destroy(gameObject);
        Time.timeScale = 0;
    }
    IEnumerator ResetSpeed()
    {
        yield return new WaitForSeconds(2f);
        baseSpeed = normalSpeed;
    }
}