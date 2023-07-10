using Atomix.Pathfinding;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.Rendering.UI;
using Unity.Burst.CompilerServices;
using System.Diagnostics;

public static class NavigationCoreEventHandler
{
    public static event Action<NavigationCore, Atomix.Pathfinding.GridNode> OnNodeStateUpdate;
    public static void NodeStateUpdateRequest(NavigationCore navigationCore, Atomix.Pathfinding.GridNode node) => OnNodeStateUpdate?.Invoke(navigationCore, node);
}

[ExecuteInEditMode]
public class NavigationCore : MonoBehaviour
{
    [Header("Grid Generation Parameters")]
    public float NodeRadius = 1;
    public float DetectionThickness = 2f;
    public Vector2Int GridDimension = new Vector2Int(10, 10);
    public bool BakeOnAwake;

    [Header("Grid Detection Parameters")]
    public LayerMask WalkableLayers;

    public Vector2Int CurrentClosestNodePosition;
    public int GridDictionnaryLenght;

    public Dictionary<Vector2Int, GridNode> GridDictionnary { get; set; } = new Dictionary<Vector2Int, GridNode>();

    private void Awake()
    {
        CreateGrid();
    }

    private void Update()
    {
        GridDictionnaryLenght = GridDictionnary.Count;
    }

    public void CreateGrid()
    {
        GridDictionnary = new Dictionary<Vector2Int, GridNode>();

        if (BakeOnAwake)
        {
            for (int x = 0; x < 2 * GridDimension.x; ++x)
            {
                for (int y = 0; y < 2 * GridDimension.y; ++y)
                {
                    CreateNodeOnPosition(x, y);
                }
            }
        }
    }

    public GridNode GetNodeByPosition(Vector2Int gridPosition, bool createIfNull = false)
    {
        Atomix.Pathfinding.GridNode node = null;
        if (GridDictionnary.TryGetValue(gridPosition, out node))
            return node;

        if (createIfNull)
            node = CreateNodeOnPosition(gridPosition.x, gridPosition.y);
        else return null;

        return node;
    }

    /// <summary>
    /// Returns the closest node from a given world position. 
    /// The node will be computed if it doesn't exists yet.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public GridNode GetNodeByWorldPosition(Vector3 position)
    {
        return GetNodeByPosition(WorldToGridPosition(position), true);
    }

    public GridNode CreateNodeOnPosition(int x, int y)
    {
        int indexX = x - GridDimension.x;
        int indexZ = y - GridDimension.y;
        var position = new Vector2Int(x, y);

        GridDictionnary.Add(position, new GridNode() { Position = new Vector3Int(x, 0, y), WorldPosition = GridToWorldPositionFlattened(x, y) });

        RaycastHit hit;
        if (Physics.SphereCast(GridDictionnary[position].WorldPosition + Vector3.up * 1000, DetectionThickness, Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask("Default", "Obstacle")))
        {
            GridDictionnary[position].WorldPosition = new Vector3(GridDictionnary[position].WorldPosition.x, hit.point.y, GridDictionnary[position].WorldPosition.z);

            if ((WalkableLayers & (1 << hit.collider.gameObject.layer)) != 0)
            {
                GridDictionnary[position].NodeState = NodeState.Walkable;
            }
            else
            {
                GridDictionnary[position].NodeState = NodeState.Unwalkable;
            }
        }

        return GridDictionnary[position];
    }

    public void UpdateNodeStateOnWorldPosition(Vector3 position, NodeState nodeState)
    {
        UpdateNodeStateOnWorldPosition(WorldToGridPosition(position), nodeState);
    }

    public void UpdateNodeStateOnWorldPosition(Vector2Int coordinates, NodeState nodeState)
    {
        GridDictionnary[coordinates].NodeState = nodeState;
        NavigationCoreEventHandler.NodeStateUpdateRequest(this, GridDictionnary[coordinates]);
    }

    public List<Vector2Int> FindNodesPositionsInRange(Vector3 position, float range = 5, bool toGround = false)
    {
        List<Vector2Int> nodes = new List<Vector2Int>();
        position.y = 0;
        float rangeSquared = range * range;

        Vector2Int gridPosition = WorldToGridPosition(position);

        for (int x = -gridPosition.x; x < gridPosition.x; ++x)
        {
            for (int y = -gridPosition.y; y < -gridPosition.y; ++y)
            {
                Vector3 gridWorldPosition = GridToWorldPositionFlattened(gridPosition.x + x, gridPosition.y + y);

                if ((gridWorldPosition - position).sqrMagnitude <= rangeSquared)
                {
                    nodes.Add(new Vector2Int(gridPosition.x + x, gridPosition.y + y));
                }
            }
        }

        return nodes;
    }

    public void CreatePotentialNodesInRange(Vector3 position, int range = 5)
    {
        float rangeSquared = range * range;
        position.y = 0;


        Vector2Int gridPosition = WorldToGridPosition(position);

        for (int x = -range; x < range; ++x)
        {
            for (int y = -range; y < range; ++y)
            {

                if (!GridDictionnary.ContainsKey(new Vector2Int(gridPosition.x + x, gridPosition.y + y)))
                {
                    Vector3 gridWorldPosition = GridToWorldPositionFlattened(gridPosition.x + x, gridPosition.y + y);

                    if ((gridWorldPosition - position).sqrMagnitude <= rangeSquared)
                    {
                        CreateNodeOnPosition(gridPosition.x + x, gridPosition.y + y);
                    }
                }
            }
        }
        
    }

    public GridNode FindClosestNodeFromList(List<GridNode> input, GridNode target)
    {
        float min = float.MaxValue;
        Atomix.Pathfinding.GridNode closest = null;
        foreach (var cell in input)
        {
            float sqr = (cell.Position - target.Position).sqrMagnitude;
            if (sqr < min)
            {
                closest = cell;
                min = sqr;
            }
        }
        return closest;
    }

    public Vector3 GridToWorldPositionFlattened(int x, int z)
    {       
        return new Vector3(x * NodeRadius, 0, z * NodeRadius);
    }

    /// <summary>
    /// Returns the closest grid position from a given world position
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public Vector2Int WorldToGridPosition(Vector3 position)
    {
        float x = position.x / NodeRadius;
        float z = position.z / NodeRadius;

        return new Vector2Int(
            Mathf.RoundToInt(x),
            Mathf.RoundToInt(z));
    }

    /// <summary>
    /// Returns the real world position of the closest grid node from a given world position.
    /// The method will compute the gridNode if it doesn't exists yet
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public Vector3 WorldToGridWorldPosition(Vector3 position)
    {
        Vector2Int closestGridPosition = WorldToGridPosition(position);

        GridNode gridNode = GetNodeByPosition(closestGridPosition, true);
        return gridNode.WorldPosition;
    }

    public bool IsInGrid(Vector3Int position)
    {
        return position.x >= -GridDimension.x * 2 && position.x < GridDimension.x * 2
            && position.z >= -GridDimension.y * 2 && position.z < GridDimension.y * 2;
        //&& position.z >= 0 && position.z < GridDimension.x;
    }

    public Vector3Int GetDirection(Vector3Int posA, Vector3Int posB)
    {
        Vector3Int directionnal = posB - posA;
        if (directionnal.x != 0)
        {
            directionnal.x = (int)Mathf.Sign(directionnal.x);
        }

        if (directionnal.y != 0)
        {
            directionnal.y = (int)Mathf.Sign(directionnal.y);
        }

        if (directionnal.z != 0)
        {
            directionnal.z = (int)Mathf.Sign(directionnal.z);
        }

        return directionnal;
    }

    public static int GetManhattanDistance(Vector3Int from, Vector3Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y) + Mathf.Abs(from.z - to.z);
    }

    public static int GetManhattanHorizontalDistance(Vector3Int from, Vector3Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.z - to.z);
    }

    public int GetDistance(Vector3Int from, Vector3Int to)
    {
        return Mathf.FloorToInt(Vector3Int.Distance(from, to));
    }

    public List<GridNode> GetNeighbours(GridNode from)
    {
        List<GridNode> neighbours = new List<GridNode>();
        Vector3Int[] positions = new Vector3Int[8];

        positions[0] = from.Position + new Vector3Int(-1, 0, 0);
        positions[1] = from.Position + new Vector3Int(1, 0, 0);
        positions[2] = from.Position + new Vector3Int(0, 0, 1);
        positions[3] = from.Position + new Vector3Int(0, 0, -1);
        positions[4] = from.Position + new Vector3Int(-1, 0, 1);
        positions[5] = from.Position + new Vector3Int(1, 0, -1);
        positions[6] = from.Position + new Vector3Int(1, 0, 1);
        positions[7] = from.Position + new Vector3Int(-1, 0, -1);

        AddNeighbours(neighbours, positions);
        return neighbours;
    }

    private void AddNeighbours(List<GridNode> neighbours, Vector3Int[] positions)
    {
        for (int i = 0; i < positions.Length; ++i)
        {
            GridNode node = null;
            if (GridDictionnary.TryGetValue(new Vector2Int(positions[i].x, positions[i].z), out node))
            {
                neighbours.Add(node);
            }
        }
    }

    public void Traverse(Action<Vector3Int> onCell)
    {
        for (int x = 0; x < GridDimension.x; ++x)
        {
            for (int y = 0; y < GridDimension.y; ++y)
            {
                onCell.Invoke(new Vector3Int(x, y));
            }
        }
    }


    public bool DoDebugDraw = true;
    public bool DrawGrid = false;
    public bool DrawWalkable = false;
    public bool DrawUnwalkable = true;


#if UNITY_EDITOR

    public Mesh DebugGridMesh;

    private void OnDrawGizmos()
    {
        if (DoDebugDraw)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(GridDimension.x * 2 * NodeRadius, 100, GridDimension.y * 2 * NodeRadius));

            if (GridDictionnary != null)
            {
                foreach (var n in GridDictionnary)
                {
                    if (n.Value != null)
                    {
                        if (n.Value.NodeState == NodeState.Walkable)
                        {
                            if (DrawWalkable)
                            {
                                Gizmos.color = Color.green;
                                Gizmos.DrawWireSphere(n.Value.WorldPosition, DetectionThickness);
                            }
                        }
                        else if (DrawUnwalkable)
                        {
                            Gizmos.color = Color.black;
                            Gizmos.DrawWireSphere(n.Value.WorldPosition, DetectionThickness);
                        }
                    }
                }

            }
        }

    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 200, 30), "Reload Grid"))
        {
            CreateGrid();
        }
    }
#endif

}
