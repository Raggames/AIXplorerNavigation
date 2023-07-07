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
        public float DestinationThreshold = 2f;
        public float SteeringDistance = .4f;
        public float MaximumSpeed = 3f;
        public int DetectionRadius = 10;
        public int DetectionAreaBonus = 1;
        public int TotalSearchIterations = 50;
        public float AvoidanceForce = 1;
        public float TurnSmoothTime = 0.3f;
        public float SearchAreaMultiplier;
        public float SearchAreaDecay = 5;
        public float MaximumStuckTime = .75f;
        public float ApproachTimer = .25f;

        [Header("Debug")]
        public Transform DebugDestination;
        public int PathIndex;
        public float DistanceToNextWp;
        public float DistanceToDestination;

        // PRIVATES
        private bool _isNavigating = false;
        private Vector3 _turnDampingVelocity = Vector3.zero;
        private Pathfinder _pathfinder;
        private Action<bool> _onArrivedEndPath;
        private CharacterController _characterController;
        private List<Node> _currentPath = new List<Node>();
        private Vector3 _currentDestination;
        private Vector2Int _currentPosition;
        private Vector3 _lastCheckPosition;
        private float _stuckTimer;

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
                StopNavigation();
                Debug.LogError("DEST=" + DebugDestination.position);
                NavigateTo(DebugDestination.position, (result) => Debug.Log(result), 0, 0);
            }

            if (_isNavigating)
            {
                DistanceToDestination = Vector3.Distance(_currentDestination, transform.position);
                Debug.DrawLine(transform.position + Vector3.up, _currentDestination, Color.cyan);
            }

            _characterController.Move(Vector3.down * 10 * Time.deltaTime);

            /* Vector2Int pos = NavigationCore.WorldToGridPosition(transform.position);

             if(pos != _currentPosition)
             {
                 NavigationCore.UpdateNodeStateOnWorldPosition(_currentPosition, NodeState.Walkable);
                 NavigationCore.UpdateNodeStateOnWorldPosition(pos, NodeState.Unwalkable);
             }

             _currentPosition = pos;*/
        }


        /*private void NavigationCoreEventHandler_OnNodeStateUpdate(NavigationCore arg1, Node arg2)
        {
            if (_isNavigating && arg1 == NavigationCore && _currentPath.Contains(arg2))
            {
                StopNavigation();
                NavigateTo(_currentDestination, null);
            }
        }*/


        public void NavigateTo(Vector3 destination, Action<bool> arrivedAtDestination, float searchAreaMultiplier = 0, int totalIterations = 0, int noPathFoundIterations = 0)
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

            _pathfinder.FindPath(transform.position, destination, (found_complete_path, path) =>
            {
                if (path != null)
                {
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
                            Debug.LogError("found_complete_path.");

                            // GOTO
                            searchAreaMultiplier = 0;

                            StartCoroutine(NavigationRoutine(_pathList, (result) => _onArrivedEndPath.Invoke(result)));
                        }
                        else
                        {
                            Debug.LogError($"found_partial_path. lenght = {path.Count}");

                            // PARTIAL PATH :
                            // Go at destination, then compute grid in detection radius and redo until arrived at destination
                            StartCoroutine(NavigationRoutine(_pathList, (result) =>
                            {
                                OnEndPartialPath(destination, arrivedAtDestination, totalIterations, noPathFoundIterations);
                            }));
                        }
                    }
                    else
                    {                        
                        float _currentDistance = (transform.position - destination).magnitude;
                        if (_currentDistance <= WaypointThreshold)
                        {
                            arrivedAtDestination.Invoke(true);
                            Debug.LogError("Path count was 0. Arrived at destination.");
                        }
                        else
                        {
                            if (noPathFoundIterations >= 3)
                            {
                                Debug.LogError("Too much wrong try iterations.");
                                arrivedAtDestination.Invoke(false);
                                return;
                            }

                            // Probly no path found to destination. Search around 
                            Debug.LogError("Path count was 0. Try search bigger area and retry." + totalIterations + " " + _currentDistance);

                            NavigationCore.CreatePotentialNodesInRange(transform.position, DetectionRadius + DetectionAreaBonus * SearchAreaMultiplier);
                            SearchAreaMultiplier++;
                            noPathFoundIterations++;
                            NavigateTo(destination, arrivedAtDestination, SearchAreaMultiplier, totalIterations, noPathFoundIterations);
                        }

                    }

                }
                else
                {
                    Debug.LogError("No path avalaible.");
                    arrivedAtDestination.Invoke(false);
                }

            });
        }

        private float _lastCurrentDistance = 0;

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
                if(Mathf.Abs(_lastCurrentDistance - _currentDistance) < .05f)
                {
                    Debug.LogError("Abort navigation, stuck on path.");
                    arrivedAtDestination.Invoke(false);
                }
                else
                {
                    _lastCurrentDistance = _currentDistance;

                    Debug.LogError("Arrived at end of partial path. Analyse navmesh and retry." + searchIterations + " " + _currentDistance);

                    NavigationCore.CreatePotentialNodesInRange(transform.position, DetectionRadius + DetectionAreaBonus * SearchAreaMultiplier);

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

                //Vector3 toNextWp = (transform.position - path[_pathIndex]);
                while (true)
                {
                    
                    DistanceToNextWp = (transform.position - path[_pathIndex]).magnitude;

                    // Getting a steering position that looks at a given distance from the agent along the path
                    // Allow to make the movement smoother as it will simulate a curve in the same logic as bezier curves.
                    float _steerDist = SteeringDistance;
                    Vector3 _steerPosition = Vector3.zero;
                    Vector3 _current = transform.position;
                    int _steerPathIndex = _pathIndex;

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

                    Debug.DrawLine(transform.position + Vector3.up, path[_pathIndex] + Vector3.up, Color.green);
                    Debug.DrawLine(transform.position + Vector3.up, _steerPosition + Vector3.up, Color.red);

                    Vector3 _oldDir = _direction;
                    //Vector3 direction = path[_pathIndex] - transform.position;
                    _direction = _steerPosition - transform.position;
                    _direction.Normalize();

                    // Checking if overlapping whith other agents in a given range
                    var cols = Physics.OverlapSphere(transform.position, 5, LayerMask.GetMask("Agents"));

                    // If too close from an agent, avoidance
                    if (cols.Length > 0)
                    {
                        Vector3 _avoidanceVector = Vector3.zero;
                        int _avoidCount = 0;
                        for (int i = 0; i < cols.Length; ++i)
                        {
                            if (cols[i].gameObject != this.gameObject)
                            {
                                _avoidanceVector += cols[i].gameObject.transform.position;
                                _avoidCount++;
                            }
                        }


                        if (_avoidCount > 0)
                        {
                            // Computing an avoidance Vector as the barycenter of all other agents in range.                            
                            _avoidanceVector /= _avoidCount;

                            Debug.DrawLine(transform.position + Vector3.up, _avoidanceVector + Vector3.up, Color.blue);
                            // Taking the vector from agent position to avoidance barycenter and at it to direction
                            Vector3 _avoidanceDirection = transform.position - _avoidanceVector;
                            _direction += _avoidanceDirection.normalized * AvoidanceForce * 1 / _avoidanceDirection.magnitude;
                        }
                    }

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
            transform.rotation = Quaternion.LookRotation(target - mypos);
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
    }
}
