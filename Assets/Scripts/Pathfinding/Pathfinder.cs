using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Atomix.Pathfinding
{
    public class Pathfinder : MonoBehaviour
    {
        [HideInInspector] public float HallwayPathRatio = 5;
        
        public Transform A;
        public Transform B;
        public List<Node> Path;

        public List<Node> closedSetDebug = new List<Node>();
        public List<Node> openSetDebug = new List<Node>();

        private NavigationCore _navigationCore;

        public void Initialize(NavigationCore navigationCore)
        {
            _navigationCore = navigationCore;

        }
        public void FindPath(Vector3 startPos, Vector3 targetPos, Action<bool, List<Node>> resultCallback)
        {
            closedSetDebug = new List<Node>();
            openSetDebug = new List<Node>();

            Vector2Int startCellPosition = _navigationCore.WorldToGridPosition(startPos);
            Vector2Int targetCellPosition = _navigationCore.WorldToGridPosition(targetPos);

            Debug.LogError($"FindPath from {startCellPosition} to {targetCellPosition}");

            Node startNode = _navigationCore.Grid[startCellPosition.x, startCellPosition.y]; //, startCellPosition.z];
            Node targetNode = _navigationCore.Grid[targetCellPosition.x, targetCellPosition.y];//, targetCellPosition.z];

            // Si un des nodes est nul on le créée pour obtenir non pas le chemin mais un nuage de points explorés par l'algorithme
            // dont on selectionnera le plus proche de la cible pour s'y rendre avant de scanner la zone alentour et de recommencer l'opération
            if(startNode == null) 
            {
                startNode = _navigationCore.CreateNodeOnPosition(startCellPosition.x, startCellPosition.y);
            }

            if (targetNode == null)
            {
                targetNode = _navigationCore.CreateNodeOnPosition(targetCellPosition.x, targetCellPosition.y);
            }

            Heap<Node> openSet = new Heap<Node>(_navigationCore.GridDimension.x  * 2 * _navigationCore.GridDimension.y * 2);// * grid.GridDimension.z);
            HashSet<Node> closedSet = new HashSet<Node>();
            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                Node current = openSet.RemoveFirst();
                closedSet.Add(current);
                closedSetDebug.Add(current);

                if (current == targetNode)
                {
                    resultCallback.Invoke(true, RetracePath(startNode, targetNode));
                }

                foreach (Node neighbour in _navigationCore.GetNeighbours(current))
                {                    
                    /*if (neighbour.CellType == CellType.Unwalkable 
                        && neighbour != targetNode 
                        && neighbour != startNode)
                    {
                        continue;
                    }*/

                    if(neighbour == null)
                    {
                        Debug.LogError("Neighbour was null");
                        continue;
                    }

                    if (closedSet.Contains(neighbour) || neighbour.CellType == CellType.Unwalkable)
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
            Node closestFromTarget = _navigationCore.FindClosestNodeFromList(closedSet.ToList(), targetNode);

            resultCallback.Invoke(false, RetracePartialPath(startNode, closestFromTarget));
        }

        private void ClearSet(Node[] set)
        {
            for (int i = 0; i < set.Length; ++i)
            {
                set[i] = null;
            }
        }

        private List<Node> RetracePath(Node startNode, Node endNode)
        {
            List<Node> path = new List<Node>();
            Node currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode);

                currentNode = currentNode.Parent;
            }

            path.Add(startNode);
            path.Reverse();

            return path;
        }

        private List<Node> RetracePartialPath(Node startNode, Node endNode)
        {
            List<Node> path = new List<Node>();
            Node currentNode = endNode;

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

        float GetHeuristic(Node nodeA, Node nodeB)
        {
            //return Mathf.Max(0, Mathf.Abs(nodeB.Position.x - nodeA.Position.x) + Mathf.Abs(nodeB.Position.z - nodeA.Position.z));
            return NavigationCore.GetManhattanDistance(nodeA.Position, nodeB.Position);
            //return GetDistance(nodeA, nodeB);
        }

        int GetDistance(Node nodeA, Node nodeB)
        {
            int dstX = Mathf.Abs(nodeA.Position.x - nodeB.Position.x);
            int dstY = Mathf.Abs(nodeA.Position.z - nodeB.Position.x);

            if (dstX > dstY)
                return 14 * dstY + 10 * (dstX - dstY);
            return 14 * dstX + 10 * (dstY - dstX);
        }

        private void OnGUI()
        {            
            if (GUI.Button(new Rect(0, 40, 200, 30), "Test Path"))
            {
                FindPath(A.position, B.position, (foundComplete, path) =>
                {
                    Path = path;
                } );
            }
        }

        void OnDrawGizmos()
        {
            if(Path != null)
            {
                foreach(var path in Path)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(path.WorldPosition, Vector3.one);
                }

                foreach (var node in closedSetDebug)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(node.WorldPosition, Vector3.one * .3f);
                }

               /* foreach (var node in openSetDebug)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(node.WorldPosition, Vector3.one);
                }*/
            }
        }
    }
}
