using UnityEngine;
using AIGame.Core;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

namespace Bobbily
{

    /// <summary>
    /// BobbilyAgent AI implementation.
    /// TODO: Describe your AI strategy here.
    /// </summary>
    public class BobbilyAgent : BaseAI
    {
        #region Fields: General
        #endregion

        #region Fields: Wandering & Movement
        private int maxAttempts = 30;
        private float wanderRadius = 60f;
        private const float ARRIVAL_THRESHOLD = 0.6f;
        private Vector3 currentDestination;
        private bool wander = true;
        #endregion

        #region Fields: Flag & Components
        private bool movingToFlag = false;
        #endregion

        #region Fields: Role & Combat
        private enum Role { Defender}
        private Role role = Role.Defender;
        private bool inCombat = false;

        private float safeDistance = 12f; // If many enemies whithin this range, hight threat
        private int dangerCountThreshold = 3; //If more than this number of enemies avoid!!
        #endregion

        protected override string SetName()
        {
            return "Bobbily";
        }

        /// <summary>
        /// Configure the agent's stats (speed, health, etc.).
        /// </summary>
        protected override void ConfigureStats()
        {
            // Configure your agent's stats
            if (role == Role.Defender)
            {
                AllocateStat(StatType.Speed, 5);
                AllocateStat(StatType.VisionRange, 6);
                AllocateStat(StatType.ProjectileRange, 6);
                AllocateStat(StatType.ReloadSpeed, 2);
                AllocateStat(StatType.DodgeCooldown, 3);
            }

        }

        /// <summary>
        /// Called once when the agent starts.
        /// Use this for initialization.
        /// </summary>
        protected override void StartAI()
        {
            //Initialize AI here
            Vector3 basePoint = GameManager.Instance.Objective.transform.position;
            Vector3 offset = Random.insideUnitSphere * 9f;  //Makes them not stand over eachother
            MoveTo(basePoint + new Vector3(offset.x, 0, offset.z));

            //Mkae them look in different directions
            transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        }

        /// <summary>
        /// Called every frame to make decisions.
        /// Implement your AI logic here.
        /// </summary>
        protected override void ExecuteAI()
        {
            if (!IsAlive || !NavMeshAgent.enabled || !NavMeshAgent.isOnNavMesh)
            {
                return;
            }

            var enemies = GetVisibleEnemiesSnapshot();

            #region Priority 1: too many enemies -> Retreat
            if (TooManyEnemies(enemies))
            {
                var ownFlag = CaptureTheFlag.Instance.GetOwnFlagPosition(this);
                if (ownFlag.HasValue)
                {
                    MoveTo(ownFlag.Value);
                    return;
                }
            }
            #endregion

            #region Priority 2: Engange in fight
            if (enemies.Count > 0)
            {
                var nearestEnemy = GetNearest(enemies);
                if (nearestEnemy.HasValue)
                {
                    StopMoving();
                    FaceTarget(nearestEnemy.Value.Position);
                    ThrowBallAt(nearestEnemy.Value);
                    return; //Attack takes priority
                }
            }

            #endregion

            #region Priority 3: Defender Behavior -> go to pointboks and take it
            Vector3 pointPos = GameManager.Instance.Objective.transform.position;
            if (Vector3.Distance(transform.position, pointPos) > 4f)
            {
                MoveTo(pointPos);
            }
            else if (HasReachedDestination())
            {
                currentDestination = PickRandomDestination();
                MoveTo(currentDestination);
            }

            #endregion

            #region Priority 4: Wander if idle
            if (HasReachedDestination())
            {
                currentDestination = PickRandomDestination();
                MoveTo(currentDestination);
            }
            #endregion
        }
        private void WhatNow()
        {
            if (!CurrentTarget.HasValue && GetVisibleEnemiesSnapshot().Count == 0)
            {
                inCombat = false;

                // Ensure NavMeshAgent is properly re-enabled
                if (NavMeshAgent.isStopped)
                {
                    NavMeshAgent.isStopped = false;
                }
                else
                {
                    wander = true;
                    currentDestination = PickRandomDestination();
                    MoveTo(currentDestination);
                }
            }
        }

        #region Methods: Combat
        private void OnEnemyDetected()
        {
            wander = false;
            NavMeshAgent.isStopped = true;

            if (GetVisibleEnemiesSnapshot().Count == 0)
            {
                WhatNow();
                return;
            }

            RefreshOrAcquireTarget();
            StopMoving();
            inCombat = true;
        }

        private void OnDeath()
        {
            wander = true;
            movingToFlag = false;
        }

        #endregion

        #region Methods: Wandering
        private bool HasReachedDestination()
        {

            if (NavMeshAgent.remainingDistance <= ARRIVAL_THRESHOLD)
            {
                return true;
            }
            else if (!NavMeshAgent.pathPending && !NavMeshAgent.hasPath && Vector3.Distance(transform.position, currentDestination) <= ARRIVAL_THRESHOLD)
            {
                return true;
            }

            return false;

        }

        /// <summary>
        /// Picks a random destination that the AI can walk to using NavMesh.
        /// </summary>
        /// <returns>A random walkable position, or the current position if no valid destination found</returns>
        private Vector3 PickRandomDestination()
        {
            Vector3 currentPosition = transform.position;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Generate a random direction
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += currentPosition;

                // Try to find a valid NavMesh position near the random point
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    // Additional check: make sure we can actually path to this destination
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(currentPosition, hit.position, NavMesh.AllAreas, path))
                    {
                        if (path.status == NavMeshPathStatus.PathComplete)
                        {
                            return hit.position;
                        }
                    }
                }
            }

            return currentPosition;
        }

        #endregion

        private bool TooManyEnemies(IReadOnlyList<PerceivedAgent> enemies)
        {
            int count = 0;
            foreach (var e in enemies)
            {
                if (Vector3.Distance(transform.position, e.Position) < safeDistance)
                {
                    count++;
                }
            }
            return count >= dangerCountThreshold;
        }

        private PerceivedAgent? GetNearest(IReadOnlyList<PerceivedAgent> enemies)
        {
            //var enemies = GetVisibleEnemiesSnapshot();
            if (enemies == null || enemies.Count == 0)
            {
                return null;
            }
            return enemies.OrderBy(e => Vector3.Distance(transform.position, e.Position)).First();
        }
    }
}