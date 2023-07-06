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

[ExecuteInEditMode]
public class NavigationCore : MonoBehaviour
{
    public static NavigationCore Instance { get; private set; }

    [Header("Grid Generation Parameters")]
    public float NodeRadius = 1;
    public Vector2Int GridDimension = new Vector2Int(10, 10);
    public bool BakeOnAwake;

    [Header("Grid Detection Parameters")]
    public LayerMask WalkableLayers;

    private ConcurrentBag<Node> _navigationGrid = new ConcurrentBag<Node>();
    private Node[,] _grid;

    public Node[,] Grid => _grid;

    public Vector2Int CurrentClosestNodePosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;

        CreateGrid();
    }

    public void CreateGrid()
    {
        _grid = new Node[2 * GridDimension.x, 2 * GridDimension.y];

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

    public Node CreateNodeOnPosition(int x, int y)
    {
        int indexX = x - GridDimension.x;
        int indexZ = y - GridDimension.y;
        _grid[x, y] = new Node() { Position = new Vector3Int(x, 0, y), WorldPosition = GridToWorldPositionFlattened(indexX, indexZ) };

        RaycastHit hit;
        if (Physics.SphereCast(_grid[x, y].WorldPosition + Vector3.up * 1000, 1, Vector3.down, out hit))
        {
            _grid[x, y].WorldPosition = new Vector3(_grid[x, y].WorldPosition.x, hit.point.y, _grid[x, y].WorldPosition.z);

            if ((WalkableLayers & (1 << hit.collider.gameObject.layer)) != 0)
            {
                // yup
                _grid[x, y].CellType = CellType.Walkable;
            }
            else
            {
                // nope
                _grid[x, y].CellType = CellType.Unwalkable;
            }
        }

        return _grid[x, y];
    }

    #region Cells operations

    public List<Node> FindNodesInRange(Vector3 position, float range = 5)
    {
        List<Node> nodes = new List<Node>();

        float rangeSquared = range * range;
        foreach (var cell in _navigationGrid)
        {
            if ((cell.Position - position).sqrMagnitude <= rangeSquared)
                nodes.Add(cell);
        }
        return nodes;
    }

    public List<Vector2Int> FindNodesPositionsInRange(Vector3 position, float range = 5, bool toGround = false)
    {
        List<Vector2Int> nodes = new List<Vector2Int>();
        position.y = 0;
        float rangeSquared = range * range;

        for (int x = 0; x < 2 * GridDimension.x; ++x)
        {
            for (int y = 0; y < 2 * GridDimension.y; ++y)
            {
                int indexX = x - GridDimension.x;
                int indexZ = y - GridDimension.y;

                Vector3 gridWorldPosition = GridToWorldPositionFlattened(indexX, indexZ);

                if ((gridWorldPosition - position).sqrMagnitude <= rangeSquared)
                {
                    nodes.Add(new Vector2Int(indexX, indexZ));
                }
            }
        }

        return nodes;
    }

    public void CreatePotentialNodesInRange(Vector3 position, float range = 5)
    {
        float rangeSquared = range * range;
        position.y = 0;

        for (int x = 0; x < 2 * GridDimension.x; ++x)
        {
            for (int y = 0; y < 2 * GridDimension.y; ++y)
            {
                if (_grid[x, y] == null)
                {
                    int indexX = x - GridDimension.x;
                    int indexZ = y - GridDimension.y;

                    Vector3 gridWorldPosition = GridToWorldPositionFlattened(indexX, indexZ);
                    if ((gridWorldPosition - position).sqrMagnitude <= rangeSquared)
                    {
                        CreateNodeOnPosition(x, y);
                    }
                }
            }
        }
    }

    public Node FindClosestNodeFromList(List<Node> input, Node target)
    {
        float min = float.MaxValue;
        Node closest = null;
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

    /*public async Task<Node> FindClosestNodeAsync(Vector3 position)
    {
        //return await Task.Run(() => FindClosestNode(position));
    }*/


    #endregion

    public Vector3 GridToWorldPositionFlattened(int x, int z)
    {
        int indexX = x + GridDimension.x;
        int indexZ = z + GridDimension.y;

        /* if (_grid[indexX, indexZ] != null)
             return _grid[indexX, indexZ].WorldPosition;*/

        Vector3 centerVector = new Vector3(GridDimension.x, 0, GridDimension.y) * NodeRadius;

        return new Vector3(transform.position.x, 0, transform.position.z)
            - centerVector
            + new Vector3(indexX * NodeRadius,
            0,
            indexZ * NodeRadius);//gridPosition.z * CellDimension + ((float)CellDimension / 2))- (GridDimension * CellDimension / 2);
    }

    public float percentX;
    public float percentY;
    public Vector2Int WorldToGridPosition(Vector3 position)
    {
        percentX = (-transform.position.x + position.x + (GridDimension.x * 2 * NodeRadius) / 2) / (GridDimension.x * 2 * NodeRadius);
        percentY = (-transform.position.z + position.z + (GridDimension.y * 2 * NodeRadius) / 2) / (GridDimension.y * 2 * NodeRadius);
        //float percentZ = (-transform.position.z + position.z + (GridDimension * CellDimension) / 2) / (GridDimension.z * CellDimension);

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);
        //percentZ = Mathf.Clamp01(percentZ);

        return new Vector2Int(
            Mathf.RoundToInt((GridDimension.x * 2) * percentX),
            Mathf.RoundToInt((GridDimension.y * 2) * percentY));
        //Mathf.RoundToInt((GridDimension.z - 1) * percentZ));
    }

    public bool IsInGrid(Vector3Int position)
    {
        return position.x >= 0 && position.x < GridDimension.x * 2
            && position.z >= 0 && position.z < GridDimension.y * 2;
        //&& position.z >= 0 && position.z < GridDimension.x;
    }

    public bool IsWorldPositionInGrid(Vector3 worldPosition, float margin = 0)
    {
        float percentX = (-transform.position.x + worldPosition.x + (GridDimension.x * NodeRadius) / 2) / (GridDimension.x * NodeRadius);
        float percentY = (-transform.position.z + worldPosition.z + (GridDimension.y * NodeRadius) / 2) / (GridDimension.y * NodeRadius);
        //float percentZ = (-transform.position.z + worldPosition.z + (GridDimension.z * CellDimension) / 2) / (GridDimension.z * CellDimension);

        return percentX - margin >= 0 && percentX + margin <= 1
            && percentY - margin >= 0 && percentY + margin <= 1;
        //&& percentZ - margin >= 00 && percentY + margin <= 1;
    }

    public Node GetNode(int x, int y)
    {
        int indexX = x + GridDimension.x;
        int indexZ = y + GridDimension.y;

        return _grid[indexX, indexZ];//, position.z];
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

    public static Vector3Int GetQuarterDirection(Vector3 posA, Vector3 posB)
    {
        Vector3 directionnal = posB - posA;
        int x = 0;
        int y = 0;
        int z = 0;

        if (directionnal.x != 0)
        {
            x = Mathf.Abs(directionnal.x) > Mathf.Abs(directionnal.z) ? (int)Mathf.Sign(directionnal.x) : 0;
        }

        if (directionnal.y != 0)
        {
            y = (int)Mathf.Sign(directionnal.y);
        }

        if (directionnal.z != 0)
        {
            z = Mathf.Abs(directionnal.z) > Mathf.Abs(directionnal.x) ? (int)Mathf.Sign(directionnal.z) : 0;
        }

        return new Vector3Int(x, y, z);
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

    public List<Node> GetNeighbours(Node from)
    {
        List<Node> neighbours = new List<Node>();
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

    private void AddNeighbours(List<Node> neighbours, Vector3Int[] positions)
    {
        for (int i = 0; i < positions.Length; ++i)
        {
            if (IsInGrid(positions[i])
                && _grid[positions[i].x, positions[i].z] != null)
            {
                neighbours.Add(_grid[positions[i].x, positions[i].z]);
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
    private void OnDrawGizmos()
    {
        if (DoDebugDraw)
        {
            if (_navigationGrid != null)
            {
                foreach (var cell in _navigationGrid)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(cell.Position, .35f);
                }
            }

            if (_grid == null)
                CreateGrid();

            if (_grid != null)
            {
                Gizmos.color = Color.yellow;
                //Gizmos.DrawWireCube(transform.position, GridDimension * CellDimension));
                for (int x = -GridDimension.x; x < GridDimension.x; ++x)
                {
                    for (int y = -GridDimension.y; y < GridDimension.y; ++y)
                    {
                        if (x + GridDimension.x == CurrentClosestNodePosition.x && y + GridDimension.y == CurrentClosestNodePosition.y)
                        {
                            Gizmos.color = Color.green;
                        }
                        else
                        {
                            Node n = GetNode(x, y);

                            if (n != null)
                            {
                                if (n.CellType == CellType.Walkable)
                                {
                                    if (DrawWalkable)
                                    {
                                        Gizmos.color = Color.green;
                                        Gizmos.DrawSphere(n.WorldPosition, .5f);
                                    }
                                }
                                else if (DrawUnwalkable)
                                {
                                    Gizmos.color = Color.black;
                                    Gizmos.DrawSphere(n.WorldPosition, .5f);
                                }
                            }
                            else
                            {
                                if (DrawGrid)
                                {
                                    Gizmos.color = Color.red;
                                    Gizmos.DrawSphere(GridToWorldPositionFlattened(x, y), .5f);
                                }
                            }
                        }

                        //Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);
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
