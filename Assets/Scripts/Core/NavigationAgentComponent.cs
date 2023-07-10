using Atomix.Pathfinding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atomix.Core
{
    [RequireComponent(typeof(Pathfinder))]
    [RequireComponent(typeof(CharacterController))]
    public class NavigationAgentComponent : MonoBehaviour
    {
        // DEPENDENCY
        public NavigationCore NavigationCore;

        [Header("Parameters")]
        // Maximum distance from a waypoint to be considered as achieved
        public float WaypointThreshold = 4.5f;
        public float DestinationThreshold = 2.5f;
        public float SteeringDistance = 6f;
        public float MaximumSpeed = 3f;
        public int DetectionRadius = 12;
        public int DetectionAreaBonus = 8;
        public int TotalSearchIterations = 50;
        public float AvoidanceRadius = 2;
        public float AvoidanceForce = 1;
        public float TurnSmoothTime = 0.05f;
        public float SearchAreaMultiplier;
        public float SearchAreaDecay = 5;
        public float MaximumStuckTime = .75f;
        public float ApproachTimer = .25f;
        public float StuckOnPathThreshold = 0.05f;

        [Header("Debug")]
        public Transform DebugDestination;
        public bool ShowDebugPath;
        public int PathIndex;
        public float DistanceToNextWp;
        public float DistanceToDestination;

        // PRIVATES
        private Pathfinder _pathfinder;
        private bool _isNavigating = false;
        private Vector3 _turnDampingVelocity = Vector3.zero;
        private Vector3 _lastCheckPosition;
        private Action<bool> _onArrivedEndPath;
        private CharacterController _characterController;
        private List<GridNode> _currentPath = new List<GridNode>();
        private Vector3 _currentDestination;
        private Vector2Int _currentPosition;
        private float _stuckTimer;
        private float _lastCurrentDistance = 0;
        private Quaternion _turnRotationVelocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _pathfinder = GetComponent<Pathfinder>();

            // For debug, this component should be initialized externally 
            Initialize(NavigationCore);
        }

        public void Initialize(NavigationCore navigationCore)
        {
            NavigationCore = navigationCore;
            _pathfinder.Initialize(navigationCore);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                if (_isNavigating)
                    StopNavigation();

                Debug.LogError(this.gameObject + " execute navigating !");

                ExecuteNavigation(DebugDestination.position, (result) => Debug.Log(result));
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_isNavigating)
                    StopNavigation();

                Debug.LogError(this.gameObject + " execute navigating random!");

                Vector3 random = new Vector3(UnityEngine.Random.Range(-100, 100), 0, UnityEngine.Random.Range(-100, 100));
                float y = Terrain.activeTerrain.SampleHeight(random);
                random = new Vector3(random.x, y, random.z);

                ExecuteNavigation(random, (result) => Debug.Log(result));
            }

            if (_isNavigating)
            {
                DistanceToDestination = Vector3.Distance(_currentDestination, transform.position);
                Debug.DrawLine(transform.position + Vector3.up, _currentDestination, Color.cyan);
            }

            _characterController.Move(Vector3.down * 10 * Time.deltaTime);
        }

        public void ExecuteNavigation(Vector3 destination, Action<bool> arrivedAtDestination)
        {
            _stuckTimer = 0;
            _lastCurrentDistance = 0;

            if (NavigationCore.GetNodeByWorldPosition(destination).NodeState == NodeState.Walkable)
            {
                NavigateTo(destination, (result) => Debug.Log(result), 0, 0);
            }
            else
            {
                Debug.LogError("Destination is not walkable.");
                arrivedAtDestination.Invoke(false);
            }
        }

        private void NavigateTo(Vector3 destination, Action<bool> arrivedAtDestination, float searchAreaMultiplier = 0, int totalIterations = 0, int noPathFoundIterations = 0)
        {
            SearchAreaMultiplier = searchAreaMultiplier;
            _currentDestination = destination;

            if (totalIterations == 0)
                _onArrivedEndPath = arrivedAtDestination;

            if (totalIterations > TotalSearchIterations)
            {
                Debug.LogError("Too much moves for this navigation, abort to avoid stack overflow");
                arrivedAtDestination.Invoke(false);
                return;
            }

            totalIterations++;

            if (Vector3.Distance(transform.position, NavigationCore.WorldToGridWorldPosition(destination)) <= NavigationCore.DetectionThickness)
            {
                Debug.Log("No path computation needed here.");
                StartCoroutine(NavigationRoutine(new List<Vector3>() { destination }, (result) => _onArrivedEndPath.Invoke(result)));
            }
            else
            {
                _pathfinder.FindPathAsync(transform.position, destination, (found_complete_path, path) =>
                {
                    if (path != null)
                    {
                        _currentPath = path;

                        if (path.Count > 0)
                        {
                            noPathFoundIterations = 0;

                            List<Vector3> _pathList = path.Select(t => t.WorldPosition).ToList();
                            
                            if ((_pathList[_pathList.Count - 1] - destination).magnitude <= WaypointThreshold)
                            {
                                _pathList.Add(destination);
                            }

                            if (found_complete_path)
                            {
                                Debug.Log("found_complete_path.");

                                // GOTO
                                searchAreaMultiplier = 0;

                                StartCoroutine(NavigationRoutine(_pathList, (result) => _onArrivedEndPath.Invoke(result)));
                            }
                            else
                            {
                                Debug.Log($"found_partial_path. lenght = {path.Count}");

                                // PARTIAL PATH :
                                // Go at destination, then compute grid in detection radius and redo until arrived at destination
                                StartCoroutine(NavigationRoutine(_pathList, (result) =>
                                {
                                    // If the navigation routine returns false, it means the agent is stuck somewhere, so we prefer to abort the navigation completely rather than
                                    // trying again and again to pathfind and go to a destination that is currently impossible to achieve for some reason.
                                    if (!result)
                                    {
                                        Debug.LogError("Navigation routine unable to achieve given destination. Abort navigation.");
                                        _onArrivedEndPath.Invoke(result);
                                    }
                                    else
                                    {
                                        OnEndPartialPath(destination, arrivedAtDestination, totalIterations, noPathFoundIterations);
                                    }
                                }));
                            }
                        }
                        else
                        {
                            float _currentDistance = (transform.position - destination).magnitude;
                            if (_currentDistance <= WaypointThreshold)
                            {
                                Debug.Log("Path count was 0. Arrived at destination.");
                                arrivedAtDestination.Invoke(true);
                            }
                            else
                            {
                                if (noPathFoundIterations >= 3)
                                {
                                    Debug.Log("Too much wrong try iterations.");
                                    arrivedAtDestination.Invoke(false);
                                    return;
                                }

                                // Probly no path found to destination. Search around 
                                Debug.Log("Path count was 0. Try search bigger area and retry." + totalIterations + " " + _currentDistance);

                                NavigationCore.CreatePotentialNodesInRange(transform.position, Mathf.RoundToInt(DetectionRadius + DetectionAreaBonus * SearchAreaMultiplier));
                                SearchAreaMultiplier++;
                                noPathFoundIterations++;
                                NavigateTo(destination, arrivedAtDestination, SearchAreaMultiplier, totalIterations, noPathFoundIterations);
                            }

                        }

                    }
                    else
                    {
                        Debug.Log("No path avalaible.");
                        arrivedAtDestination.Invoke(false);
                    }

                });
            }

        }

        private void OnEndPartialPath(Vector3 destination, Action<bool> arrivedAtDestination, int searchIterations, int noPathFoundIterations)
        {
            float _currentDistance = (transform.position - destination).magnitude;
            if (_currentDistance <= WaypointThreshold)
            {
                arrivedAtDestination.Invoke(true);
            }
            else
            {
                // If a previous move didn't change the distance from destination to a small value, that has a high probability of the agent being stuck.
                // To avoid recomputing an impossible path until stack overflowing or the hard "Total iteration" cap, the navigation will interrupt in that specific case.
                // A value <= 0 will force the agent to search for a path, but it could be pretty CPU expensive
                // The highest the value, the more the agent will stop early if the path is "complicated (= needs to go back and explore a bigger area)"
                if (Mathf.Abs(_lastCurrentDistance - _currentDistance) < StuckOnPathThreshold)
                {
                    Debug.Log("Abort navigation, stuck on path." + Mathf.Abs(_lastCurrentDistance - _currentDistance));
                    arrivedAtDestination.Invoke(false);
                }
                else
                {
                    _lastCurrentDistance = _currentDistance;

                    Debug.Log("Arrived at end of partial path. Analyse navmesh and retry." + searchIterations + " " + _currentDistance);

                    NavigationCore.CreatePotentialNodesInRange(transform.position, Mathf.RoundToInt(DetectionRadius + DetectionAreaBonus * SearchAreaMultiplier));

                    SearchAreaMultiplier++;

                    NavigateTo(destination, arrivedAtDestination, SearchAreaMultiplier, searchIterations, noPathFoundIterations);
                }
            }
        }

        private IEnumerator NavigationRoutine(List<Vector3> path, Action<bool> arrivedAtDestination)
        {
            _isNavigating = true;

            float _destinationApproachTimer = 0;

            int _pathIndex = 0;
            Vector3 _destination = path[path.Count - 1];
            Vector3 _direction = transform.forward;

            while (_pathIndex < path.Count)
            {
                PathIndex = _pathIndex;

                while (true)
                {
                    DistanceToNextWp = (transform.position - path[_pathIndex]).magnitude;

                    // Getting a steering position that looks at a given distance from the agent along the path
                    // Allow to make the movement smoother as it will simulate a curve in the same logic as bezier curves.
                    float _steerDist = SteeringDistance;
                    Vector3 _steerPosition = Vector3.zero;
                    Vector3 _current = transform.position;
                    int _steerPathIndex = _pathIndex;

                    _steerPosition = GetSteerPosition(path, ref _steerDist, ref _current, ref _steerPathIndex);

                    Debug.DrawLine(transform.position + Vector3.up, path[_pathIndex] + Vector3.up, Color.green);
                    Debug.DrawLine(transform.position + Vector3.up, _steerPosition + Vector3.up, Color.red);

                    Vector3 _oldDir = _direction;
                    //Vector3 direction = path[_pathIndex] - transform.position;
                    _direction = _steerPosition - transform.position;
                    _direction.Normalize();

                    // COmpute avoidance of other agents /entities
                    _direction = ComputeAvoidance(_direction);

                    // Smooth daming the direction to smooth the movement a bit more.
                    _direction = Vector3.SmoothDamp(_oldDir, _direction, ref _turnDampingVelocity, TurnSmoothTime);

                    // Applying movement to contorller
                    _characterController.Move(_direction * (MaximumSpeed / (1 + _destinationApproachTimer * 100)) * Time.deltaTime);

                    if (_destinationApproachTimer == 0)
                        LookAt(_steerPosition);

                    // Decaying the search area multiplier that increases when an agent search for a path 
                    if (SearchAreaMultiplier > 0)
                        SearchAreaMultiplier -= Time.deltaTime * SearchAreaDecay;

                    // Checking if the agent hasn't move for a while to abort the navigation
                    if (IsStuck())
                    {
                        StopNavigation();
                        arrivedAtDestination.Invoke(false);
                        yield break;
                    }

                    yield return null;

                    // If above a certain threshold, allows the agent to pass waypoints at a certain range
                    if ((transform.position - _currentDestination).magnitude > WaypointThreshold)
                    {
                        if ((transform.position - path[_pathIndex]).magnitude <= WaypointThreshold)
                            break;
                    }
                    // When approaching final destination, the range is shorter.
                    else
                    {
                        // When in range from destination waypoint, the agent will enter in approach mode for a fixes duration
                        // During this time, its speed will decrease and he wont fiw his look at steering point anymore (avoid turning on self problem / overshooting)
                        if (_destinationApproachTimer > 0 || (transform.position - path[_pathIndex]).magnitude <= DestinationThreshold)
                        {
                            _destinationApproachTimer += Time.deltaTime;

                            if (_destinationApproachTimer > ApproachTimer)
                                break;
                        }
                    }

                }
                _pathIndex++;
            }

            _isNavigating = false;

            yield return null;

            arrivedAtDestination.Invoke(true);
        }

        private Vector3 GetSteerPosition(List<Vector3> path, ref float _steerDist, ref Vector3 _current, ref int _steerPathIndex)
        {
            Vector3 _steerPosition;
            do
            {
                Vector3 _steerDirection = (path[_steerPathIndex] - transform.position);

                float _steerDirMagn = _steerDirection.magnitude;
                if (_steerDist < _steerDirMagn)
                {
                    _steerPosition = _steerDirection.normalized * _steerDist + _current;
                    _steerDist = 0;
                }
                else
                {
                    _steerDist -= _steerDirMagn;
                    _current = path[_steerPathIndex];
                    _steerPosition = _steerDirection.normalized * _steerDist + _current;
                    _steerPathIndex++;
                }
            }
            while (_steerDist > 0 && _steerPathIndex < path.Count);
            return _steerPosition;
        }

        private Vector3 ComputeAvoidance(Vector3 _direction)
        {
            // Checking if overlapping whith other agents in a given range
            var cols = Physics.OverlapSphere(transform.position, AvoidanceRadius, LayerMask.GetMask("Agents"));

            // If too close from an agent, avoidance
            if (cols.Length > 0)
            {
                Vector3 _collisionBarycenter = Vector3.zero;
                int _collisionCount = 0;
                for (int i = 0; i < cols.Length; ++i)
                {
                    if (cols[i].gameObject != this.gameObject)
                    {
                        _collisionBarycenter += cols[i].gameObject.transform.position;
                        _collisionCount++;
                    }
                }

                if (_collisionCount > 0)
                {
                    // Computing an avoidance Vector as the barycenter of all other agents in range.                            
                    _collisionBarycenter /= _collisionCount;

                    Debug.DrawLine(transform.position + Vector3.up, _collisionBarycenter + Vector3.up, Color.blue);
                    // Taking the vector from agent position to avoidance barycenter and at it to direction
                    Vector3 _avoidanceDirection = transform.position - _collisionBarycenter;
                    Vector3 _avoidanceFinalVector = (_avoidanceDirection.normalized * AvoidanceForce * 1 / _avoidanceDirection.magnitude);
                    _direction += _avoidanceFinalVector;
                }
            }

            return _direction;
        }

        private float GetPathDistance(List<Vector3> path)
        {
            float dist = (transform.position - path[0]).magnitude;

            if (path.Count > 1)
            {
                for (int i = 1; i < path.Count; ++i)
                {
                    dist += (path[i] - path[i - 1]).magnitude;
                }
            }

            return dist;
        }

        public void StopNavigation()
        {
            _isNavigating = false;
            StopAllCoroutines();
        }

        public void LookAt(Vector3 target)
        {
            target.y = 0;
            Vector3 mypos = transform.position;
            mypos.y = 0;
            transform.rotation = SmoothDamp(transform.rotation, Quaternion.LookRotation(target - mypos), ref _turnRotationVelocity, TurnSmoothTime);
        }

        private bool IsStuck()
        {
            if ((_lastCheckPosition - transform.position).magnitude > .5f)
            {
                _lastCheckPosition = transform.position;
                _stuckTimer = 0;
            }
            else
            {
                _stuckTimer += Time.deltaTime;

                if (_stuckTimer > MaximumStuckTime)
                {
                    return true;
                }
            }

            return false;
        }

        public static Quaternion SmoothDamp(Quaternion rot, Quaternion target, ref Quaternion deriv, float time)
        {
            if (Time.deltaTime < Mathf.Epsilon) return rot;
            // account for double-cover
            var Dot = Quaternion.Dot(rot, target);
            var Multi = Dot > 0f ? 1f : -1f;
            target.x *= Multi;
            target.y *= Multi;
            target.z *= Multi;
            target.w *= Multi;
            // smooth damp (nlerp approx)
            var Result = new Vector4(
                Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time),
                Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time),
                Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time),
                Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time)
            ).normalized;

            // ensure deriv is tangent
            var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
            deriv.x -= derivError.x;
            deriv.y -= derivError.y;
            deriv.z -= derivError.z;
            deriv.w -= derivError.w;

            return new Quaternion(Result.x, Result.y, Result.z, Result.w);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (ShowDebugPath && _currentPath != null)
            {
                Gizmos.color = Color.blue;
                foreach (var v in _currentPath)
                {
                    Gizmos.DrawCube(v.WorldPosition, Vector3.one);
                }
            }

        }
#endif
    }
}
