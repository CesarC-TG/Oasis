using UnityEngine;
using UnityEngine.AI;
using Oasis.Combat;

namespace Oasis.Enemy
{
    /// <summary>
    /// Simple finite-state AI for Vaciado enemies.
    /// Patrols waypoints → chases player → attacks in melee range.
    /// Uses NavMeshAgent for navigation and OverlapSphereNonAlloc for detection.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyDamageable))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Waypoints")]
        public Transform[] PatrolPoints;

        [Header("Detection")]
        public float DetectionRange = 15f;
        public float AttackRange = 2f;
        public LayerMask PlayerLayer = -1;

        [Header("Combat")]
        public float AttackDamage = 20f;
        public float AttackCooldown = 1.5f;

        [Header("Speed")]
        public float PatrolSpeed = 2f;
        public float ChaseSpeed = 5f;

        public enum State { Patrol, Chase, Attack }
        public State CurrentState { get; private set; } = State.Patrol;

        private NavMeshAgent _agent;
        private EnemyDamageable _damageable;
        private Animator _animator;
        private IDamageable _playerTarget;
        private Transform _playerTransform;
        private int _currentPatrolIndex;
        private float _attackTimer;

        private readonly Collider[] _detectBuffer = new Collider[16];

        private static readonly int AnimIsPatrolling = Animator.StringToHash("IsPatrolling");
        private static readonly int AnimIsChasing    = Animator.StringToHash("IsChasing");
        private static readonly int AnimAttack        = Animator.StringToHash("Attack");
        private static readonly int AnimSpeed         = Animator.StringToHash("Speed");

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _damageable = GetComponent<EnemyDamageable>();
            _animator = GetComponent<Animator>();
            _agent.speed = PatrolSpeed;
        }

        void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTarget = player.GetComponent<IDamageable>();
                _playerTransform = player.transform;
            }

            GoToNextPatrolPoint();
        }

        void Update()
        {
            if (_damageable != null && _damageable.IsDead)
            {
                _agent.isStopped = true;
                return;
            }

            _attackTimer -= Time.deltaTime;

            switch (CurrentState)
            {
                case State.Patrol: UpdatePatrol(); break;
                case State.Chase:  UpdateChase();  break;
                case State.Attack: UpdateAttack(); break;
            }
        }

        void UpdatePatrol()
        {
            // Check if player is in detection range
            if (DetectPlayer())
            {
                ChangeState(State.Chase);
                return;
            }

            // Continue patrolling
            if (PatrolPoints == null || PatrolPoints.Length == 0) return;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                GoToNextPatrolPoint();
        }

        void UpdateChase()
        {
            if (_playerTransform == null) return;

            // Lost sight of player
            if (!DetectPlayer())
            {
                ChangeState(State.Patrol);
                GoToNextPatrolPoint();
                return;
            }

            // In attack range
            if (Vector3.Distance(transform.position, _playerTransform.position) <= AttackRange)
            {
                ChangeState(State.Attack);
                return;
            }

            _agent.isStopped = false;
            _agent.speed = ChaseSpeed;
            _agent.SetDestination(_playerTransform.position);
        }

        void UpdateAttack()
        {
            if (_playerTransform == null || _playerTarget == null) return;

            // Player out of attack range → chase
            if (Vector3.Distance(transform.position, _playerTransform.position) > AttackRange)
            {
                ChangeState(State.Chase);
                return;
            }

            // Lost sight entirely → patrol
            if (!DetectPlayer())
            {
                ChangeState(State.Patrol);
                GoToNextPatrolPoint();
                return;
            }

            _agent.isStopped = true;
            FaceTarget();

            // Attack on cooldown
            if (_attackTimer <= 0f)
            {
                _attackTimer = AttackCooldown;
                var data = DamageData.Physical(AttackDamage, gameObject);
                data.HitPoint = _playerTransform.position;
                _playerTarget.ApplyDamage(data);
            }
        }

        bool DetectPlayer()
        {
            if (_playerTransform == null) return false;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, DetectionRange, _detectBuffer, PlayerLayer);
            for (int i = 0; i < hits; i++)
            {
                if (_detectBuffer[i].CompareTag("Player"))
                    return true;
            }
            return false;
        }

        void FaceTarget()
        {
            if (_playerTransform == null) return;
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        void GoToNextPatrolPoint()
        {
            if (PatrolPoints == null || PatrolPoints.Length == 0) return;

            _agent.isStopped = false;
            _agent.speed = PatrolSpeed;
            _agent.SetDestination(PatrolPoints[_currentPatrolIndex].position);
            _currentPatrolIndex = (_currentPatrolIndex + 1) % PatrolPoints.Length;
        }

        void ChangeState(State newState)
        {
            if (CurrentState == newState) return;
            Debug.Log($"[EnemyAI] {gameObject.name}: {CurrentState} → {newState}");
            CurrentState = newState;

            // Sync animator parameters
            if (_animator != null)
            {
                _animator.SetBool(AnimIsPatrolling, newState == State.Patrol);
                _animator.SetBool(AnimIsChasing, newState == State.Chase);
                _animator.SetFloat(AnimSpeed, _agent.velocity.magnitude);

                if (newState == State.Attack)
                    _animator.SetTrigger(AnimAttack);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, DetectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, AttackRange);
        }
    }
}
