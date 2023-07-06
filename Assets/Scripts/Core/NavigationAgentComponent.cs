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
        public NavigationCore NavigationCore;

        [Header("Parameters")]
        // Maximum distance from a waypoint to be considered as achieved
        public float WaypointThreshold = .4f;
        public float SteeringDistance = .4f;
        public float MaximumSpeed = 3f;
        public int DetectionRadius = 10;
        public int DetectionAreaBonus = 1;
        public int TotalSearchIterations = 50;

        private Pathfinder _pathfinder;
        private Action<bool> _onArrivedEndPath;
        private CharacterController _characterController;

        private bool _isNavigating = false;

        public Transform DebugDestination;
        public int Debug_SearchAreaMultiplier;

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
                NavigateTo(DebugDestination.position, (result) => Debug.Log(result));
            }
        }

        public void NavigateTo(Vector3 destination, Action<bool> arrivedAtDestination, int searchAreaMultiplier = 0, int searchIterations = 0)
        {
            Debug_SearchAreaMultiplier = searchAreaMultiplier;

            if (searchIterations == 0)
                _onArrivedEndPath = arrivedAtDestination;

            if (searchIterations > TotalSearchIterations)
            {
                Debug.LogError("Too much moves for this navigation, abort to avoid stack overflow");
                arrivedAtDestination.Invoke(false);
                return;
            }

            searchIterations++;

            _pathfinder.FindPath(transform.position, destination, (found_complete_path, path) =>
            {
                if (path != null)
                {
                    if(path.Count > 0)
                    {
                        if (found_complete_path)
                        {
                            Debug.LogError("found_complete_path.");

                            // GOTO
                            searchAreaMultiplier = 0;
                            List<Vector3> _pathList = path.Select(t => t.WorldPosition).ToList();
                            _pathList.Add(destination);
                            StartCoroutine(NavigationRoutine(_pathList, (result) => _onArrivedEndPath.Invoke(result)));
                        }
                        else
                        {
                            Debug.LogError($"found_partial_path. lenght = {path.Count}");

                            // PARTIAL PATH :
                            // Go at destination, then compute grid in detection radius and redo until arrived at destination
                            StartCoroutine(NavigationRoutine(path.Select(t => t.WorldPosition).ToList(), (result) =>
                            {
                                float _currentDistance = (transform.position - destination).magnitude;
                                if (_currentDistance <= WaypointThreshold)
                                {
                                    arrivedAtDestination.Invoke(true);
                                }
                                else
                                {
                                    Debug.LogError("Arrived at end of partial path. Analyse navmesh and retry.");

                                    NavigationCore.CreatePotentialNodesInRange(transform.position, DetectionRadius + DetectionAreaBonus * searchAreaMultiplier);

                                    searchAreaMultiplier++;

                                    NavigateTo(destination, arrivedAtDestination, searchAreaMultiplier, searchIterations);
                                }                              

                            }));
                        }
                    }
                    else
                    {
                        float _currentDistance = (transform.position - destination).magnitude;
                        if (_currentDistance <= WaypointThreshold)
                        {
                            arrivedAtDestination.Invoke(true);
                        }
                        else
                        {
                            // Probly no path found to destination. Search around 
                            Debug.LogError("Path count was 0. Try search bigger area and retry.");
                            NavigationCore.CreatePotentialNodesInRange(transform.position, DetectionRadius + DetectionAreaBonus * searchAreaMultiplier);
                            searchAreaMultiplier++;
                            NavigateTo(destination, arrivedAtDestination, searchAreaMultiplier, searchIterations);
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

        public int PathIndex;
        public float DistanceToNextWp;

        public float TurnSmoothTime = 0.3f;
        private Vector3 _turnDampingVelocity = Vector3.zero;

        private IEnumerator NavigationRoutine(List<Vector3> path, Action<bool> arrivedAtDestination)
        {
            _isNavigating = true;

            int _pathIndex = 0;
            Vector3 _destination = path[path.Count - 1];
            Vector3 _direction = transform.forward;

            while (_pathIndex < path.Count)
            {
                PathIndex = _pathIndex;

                //Vector3 toNextWp = (transform.position - path[_pathIndex]);
                while ((transform.position - path[_pathIndex]).magnitude > WaypointThreshold)
                {
                    DistanceToNextWp = (transform.position - path[_pathIndex]).magnitude;

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

                    _direction = Vector3.SmoothDamp(_oldDir, _direction, ref _turnDampingVelocity, TurnSmoothTime);

                    Vector2 _testHorizontalVelocity = new Vector2(_direction.x, _direction.z);
                    if(_testHorizontalVelocity.magnitude <= .05f)
                    {
                        Debug.LogError("Overshooted waypoint ?");
                    }

                    _characterController.Move(_direction * MaximumSpeed * Time.deltaTime + Vector3.down * (10 - _direction.y * 10f) * Time.deltaTime);
                    FixLookAt(_steerPosition);

                    yield return null;
                }
                _pathIndex++;
            }

            _isNavigating = false;
            arrivedAtDestination.Invoke(true);
        }

        public void StopNavigation()
        {
            _isNavigating = false;
            StopAllCoroutines();
        }

        public void FixLookAt(Vector3 target)
        {
            target.y = 0;
            Vector3 mypos = transform.position;
            mypos.y = 0;
            transform.rotation = Quaternion.LookRotation(target - mypos);
        }
    }
}
