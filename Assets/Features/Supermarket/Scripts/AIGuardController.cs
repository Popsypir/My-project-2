using UnityEngine;
using UnityEngine.AI;

namespace GamePhone.Shop
{
    /// <summary>
    /// Автономный охранник-НПС для сцены "Уборщик" - патрулирует магазин, а
    /// заметив вора (CustomerThief.IsThief), сам идёт ловить его тем же
    /// способом, что и раньше делал игрок (ItemDragController.TryGrabThief) -
    /// напрямую вызывает CustomerThief.GetCaught(), которая уже сама делает
    /// всё остальное (трясёт, роняет украденные товары на пол, вышвыривает
    /// вора). Убирать упавшие товары обратно на полки - это уже не его
    /// работа, этим занимается игрок-уборщик (см. ShopShelfStock).
    ///
    /// Специально НЕ гарантирует поимку каждого вора - если вор успевает
    /// дойти до двери раньше, чем охранник до него доберётся, вор сбегает
    /// как обычно (см. CustomerQueueController.ThiefWalkRoutine/NotifyEscaped) -
    /// никакой отдельной логики для "иногда упускать" не нужно, это само
    /// получается из гонки "кто успеет раньше".
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIGuardController : MonoBehaviour
    {
        [Header("Патруль")]
        [Tooltip("Точки маршрута - обходит по кругу, пока не за кем гнаться")]
        [SerializeField] private Transform[] _patrolPoints;
        [SerializeField] private float _patrolSpeed = 2f;
        [Tooltip("Сколько секунд стоит на точке, прежде чем идти к следующей")]
        [SerializeField] private float _patrolWaitTime = 2f;

        [Header("Погоня")]
        [SerializeField] private float _chaseSpeed = 3.8f;
        [Tooltip("На каком расстоянии до вора считается \"поймал\"")]
        [SerializeField] private float _catchDistance = 1.2f;
        [Tooltip("Как часто заново искать воров - не каждый кадр, не нужно")]
        [SerializeField] private float _thiefScanInterval = 0.5f;

        [Header("Анимация")]
        [Tooltip("Необязательно. Если не назначен - берётся автоматически с этого объекта или его детей")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _isWalkingParam = "isWalking";

        private NavMeshAgent _agent;
        private CustomerThief _targetThief;
        private int _patrolIndex;
        private float _patrolWaitTimer;
        private float _scanTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _thiefScanInterval;
                FindThiefIfFree();
            }

            if (_targetThief != null && _targetThief.IsThief)
                Chase();
            else
            {
                _targetThief = null;
                Patrol();
            }

            if (_animator != null)
                _animator.SetBool(_isWalkingParam, _agent.velocity.sqrMagnitude > 0.01f);
        }

        // Не переключается на другого вора, пока гонится за текущим - иначе
        // метался бы между несколькими сразу и не ловил вообще никого
        private void FindThiefIfFree()
        {
            if (_targetThief != null && _targetThief.IsThief) return;

            CustomerThief[] allThieves = FindObjectsByType<CustomerThief>(FindObjectsSortMode.None);
            CustomerThief nearest = null;
            float nearestSqrDist = float.MaxValue;

            foreach (CustomerThief thief in allThieves)
            {
                if (thief == null || !thief.IsThief) continue;

                float sqrDist = (thief.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = thief;
                }
            }

            _targetThief = nearest;
        }

        private void Chase()
        {
            _agent.speed = _chaseSpeed;
            _agent.SetDestination(_targetThief.transform.position);

            float dist = Vector3.Distance(transform.position, _targetThief.transform.position);
            if (dist <= _catchDistance)
            {
                _targetThief.GetCaught();
                _targetThief = null;
            }
        }

        private void Patrol()
        {
            if (_patrolPoints == null || _patrolPoints.Length == 0) return;

            _agent.speed = _patrolSpeed;

            bool reached = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;
            if (!reached)
            {
                _agent.SetDestination(_patrolPoints[_patrolIndex].position);
                return;
            }

            _patrolWaitTimer += Time.deltaTime;
            if (_patrolWaitTimer < _patrolWaitTime) return;

            _patrolWaitTimer = 0f;
            _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
            _agent.SetDestination(_patrolPoints[_patrolIndex].position);
        }
    }
}
