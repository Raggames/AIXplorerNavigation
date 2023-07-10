using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Atomix.Pathfinding
{
    public partial class Pathfinder : MonoBehaviour
    {
        private List<GridNode> _path;
        private NavigationCore _navigationCore;
        private static bool _isComputing = false;
        private static object lockObject = new object();

        public void Initialize(NavigationCore navigationCore)
        {
            _navigationCore = navigationCore;
        }

        private static Queue<PathfindingRequest> _pathfindingRequests = new Queue<PathfindingRequest>();

        public void FindPath(PathfindingRequest request) => FindPath(request.startPos, request.targetPos, request.resultCallback);

        /// <summary>
        /// Compute a pathfinding on the main thread and once at a time
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="targetPos"></param>
        /// <param name="resultCallback"></param>

        public void FindPath(Vector3 startPos, Vector3 targetPos, Action<bool, List<GridNode>> resultCallback)
        {
            // As we can't easily multithread pathfinding computation
            // Allows only one computing of a pathfinding to execute on the thread at a time
            // When agents are many, it will improve performances drastically
            if (_isComputing)
            {
                _pathfindingRequests.Enqueue(new PathfindingRequest(startPos, targetPos, resultCallback, false));
                return;
            }

            _isComputing = true;

            Vector2Int startCellPosition = _navigationCore.WorldToGridPosition(startPos);
            Vector2Int targetCellPosition = _navigationCore.WorldToGridPosition(targetPos);

            GridNode startNode = _navigationCore.GetNodeByPosition(startCellPosition, true); //, startCellPosition.z];
            GridNode targetNode = _navigationCore.GetNodeByPosition(targetCellPosition, true);//, targetCellPosition.z];

            Heap<GridNode> openSet = new Heap<GridNode>(_navigationCore.GridDimension.x * 2 * _navigationCore.GridDimension.y * 2);// * grid.GridDimension.z);
            HashSet<GridNode> closedSet = new HashSet<GridNode>();
            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                GridNode current = openSet.RemoveFirst();
                closedSet.Add(current);
                //closedSetDebug.Add(current);

                if (current == targetNode)
                {
                    resultCallback.Invoke(true, RetracePath(startNode, targetNode));
                    OnPathfindingComputationEnded();
                }

                foreach (GridNode neighbour in _navigationCore.GetNeighbours(current))
                {
                    if (neighbour == null)
                    {
                        continue;
                    }

                    if (closedSet.Contains(neighbour) || neighbour.NodeState == NodeState.Unwalkable)
                    {
                        continue;
                    }

                    float newCostToNeighbour = current.GCost + GetHeuristic(current, neighbour);
                    if (newCostToNeighbour < neighbour.GCost || !openSet.Contains(neighbour))
                    {
                        neighbour.GCost = newCostToNeighbour;
                        neighbour.HCost = GetHeuristic(neighbour, targetNode);
                        neighbour.Parent = current;

                        //Debug.LogError($"Set parent from {neighbour.Position} to {current.Position}");
                        if (!openSet.Contains(neighbour))
                        {
                            openSet.Add(neighbour);
                        }
                    }
                }
            }

            // Selection du node le plus proche de targetNode parmis le nuage de points explorés depuis le startNode (aka closedSet)
            GridNode closestFromTarget = _navigationCore.FindClosestNodeFromList(closedSet.ToList(), targetNode);
            resultCallback.Invoke(false, RetracePartialPath(startNode, closestFromTarget));

            OnPathfindingComputationEnded();
        }

        public async void FindPathAsync(PathfindingRequest request) => FindPathAsync(request.startPos, request.targetPos, request.resultCallback);

        /// <summary>
        /// Compute a pathfinding on another thread but once at a time
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="targetPos"></param>
        /// <param name="resultCallback"></param>
        public async void FindPathAsync(Vector3 startPos, Vector3 targetPos, Action<bool, List<GridNode>> resultCallback)
        {
            // When agents are many, it will improve performances drastically
            if (_isComputing)
            {
                _pathfindingRequests.Enqueue(new PathfindingRequest(startPos, targetPos, resultCallback, true));

                return;
            }

            _isComputing = true;

            Vector2Int startCellPosition = _navigationCore.WorldToGridPosition(startPos);
            Vector2Int targetCellPosition = _navigationCore.WorldToGridPosition(targetPos);

            GridNode startNode = _navigationCore.GetNodeByPosition(startCellPosition, true); //, startCellPosition.z];
            GridNode targetNode = _navigationCore.GetNodeByPosition(targetCellPosition, true);//, targetCellPosition.z];

            var pathfindingAsyncResponse = await Task.Run(() => ComputePath(startNode, targetNode, resultCallback));

            OnPathfindingComputationEnded();

            pathfindingAsyncResponse.OnComputedCallback.Invoke(pathfindingAsyncResponse.IsCompletePath, pathfindingAsyncResponse.Path);
        }

        private AsyncPathfindingResponse ComputePath(GridNode startNode, GridNode targetNode, Action<bool, List<GridNode>> resultCallback)
        {
            Heap<GridNode> openSet = new Heap<GridNode>(_navigationCore.GridDimension.x * 2 * _navigationCore.GridDimension.y * 2);// * grid.GridDimension.z);
            HashSet<GridNode> closedSet = new HashSet<GridNode>();
            openSet.Add(startNode);

            lock (lockObject)
            {
                while (openSet.Count > 0)
                {
                    GridNode current = openSet.RemoveFirst();
                    closedSet.Add(current);
                    //closedSetDebug.Add(current);

                    if (current == targetNode)
                    {
                        return new AsyncPathfindingResponse(true, RetracePath(startNode, targetNode), resultCallback);
                    }

                    foreach (GridNode neighbour in _navigationCore.GetNeighbours(current))
                    {
                        if (neighbour == null)
                        {
                            continue;
                        }

                        if (closedSet.Contains(neighbour) || neighbour.NodeState == NodeState.Unwalkable)
                        {
                            continue;
                        }

                        float newCostToNeighbour = current.GCost + GetHeuristic(current, neighbour);
                        if (newCostToNeighbour < neighbour.GCost || !openSet.Contains(neighbour))
                        {
                            neighbour.GCost = newCostToNeighbour;
                            neighbour.HCost = GetHeuristic(neighbour, targetNode);
                            neighbour.Parent = current;

                            //Debug.LogError($"Set parent from {neighbour.Position} to {current.Position}");
                            if (!openSet.Contains(neighbour))
                            {
                                openSet.Add(neighbour);
                            }
                        }
                    }
                }

                // Selection du node le plus proche de targetNode parmis le nuage de points explorés depuis le startNode (aka closedSet)
                GridNode closestFromTarget = _navigationCore.FindClosestNodeFromList(closedSet.ToList(), targetNode);
                return new AsyncPathfindingResponse(false, RetracePartialPath(startNode, closestFromTarget), resultCallback);
            }
        }

        private void OnPathfindingComputationEnded()
        {
            _isComputing = false;

            if (_pathfindingRequests.Count > 0)
            {
                var request = _pathfindingRequests.Dequeue();

                if (request.IsAsync)
                    FindPathAsync(request);
                else
                    FindPath(request);
            }
        }

        private void ClearSet(GridNode[] set)
        {
            for (int i = 0; i < set.Length; ++i)
            {
                set[i] = null;
            }
        }

        private List<GridNode> RetracePath(GridNode startNode, GridNode endNode)
        {
            List<GridNode> path = new List<GridNode>();
            GridNode currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode);

                currentNode = currentNode.Parent;
            }

            path.Add(startNode);
            path.Reverse();

            return path;
        }

        private List<GridNode> RetracePartialPath(GridNode startNode, GridNode endNode)
        {
            List<GridNode> path = new List<GridNode>();
            GridNode currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode);

                if (currentNode.Parent == null)
                {
                    break;
                }

                currentNode = currentNode.Parent;
            }

            path.Reverse();

            return path;
        }

        float GetHeuristic(GridNode nodeA, GridNode nodeB)
        {
            return NavigationCore.GetManhattanDistance(nodeA.Position, nodeB.Position);
        }

        int GetDistance(GridNode nodeA, GridNode nodeB)
        {
            int dstX = Mathf.Abs(nodeA.Position.x - nodeB.Position.x);
            int dstY = Mathf.Abs(nodeA.Position.z - nodeB.Position.x);

            if (dstX > dstY)
                return 14 * dstY + 10 * (dstX - dstY);
            return 14 * dstX + 10 * (dstY - dstX);
        }

        void OnDrawGizmos()
        {
            if (_path != null)
            {
                foreach (var path in _path)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(path.WorldPosition, Vector3.one);
                }
            }
        }
    }
}
