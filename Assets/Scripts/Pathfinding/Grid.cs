/*using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Atomix.Pathfinding
{
    public class Grid : MonoBehaviour
    {
        public Vector3Int POSITION_DEBUG;
        [ReadOnly] public string POSITION_CELLTYPE;

        public Vector3Int GridDimension;
        public int CellDimension = 1;

        public float HalfCellDimension
        {
            get
            {
                return (float)CellDimension / 2;
            }
        }

        public Cell[,,] GridArray;

        [Button("CreateGrid")]
        // Start is called before the first frame update
        public void CreateGrid()
        {
            GridArray = new Cell[GridDimension.x, GridDimension.y, GridDimension.z];
            for (int x = 0; x < GridDimension.x; ++x)
            {
                for (int y = 0; y < GridDimension.y; ++y)
                {
                    for (int z = 0; z < GridDimension.z; ++z)
                    {
                        GridArray[x, y, z] = new Cell()
                        {
                            Position = new Vector3Int(x, y, z),
                            CellType = CellType.Free,
                            IsWalkable = true,
                        };

                    }
                }
            }
        }

        public Vector3 GridToWorldPosition(Vector3Int gridPosition)
        {
            return transform.position
                + new Vector3(gridPosition.x * CellDimension + ((float)CellDimension / 2), gridPosition.y * CellDimension + ((float)CellDimension / 2), gridPosition.z * CellDimension + ((float)CellDimension / 2))
                - (GridDimension * CellDimension / 2);
        }

        public Vector3 GridAnchorToWorldPosition(Vector3Int gridPosition)
        {
            return GridToWorldPosition(gridPosition) - Vector3.one * CellDimension;
        }

        public Vector3Int WorldToGridPosition(Vector3 position)
        {
            float percentX = (-transform.position.x + position.x + (GridDimension.x * CellDimension) / 2) / (GridDimension.x * CellDimension);
            float percentY = (-transform.position.y + position.y + (GridDimension.y * CellDimension) / 2) / (GridDimension.y * CellDimension);
            float percentZ = (-transform.position.z + position.z + (GridDimension.z * CellDimension) / 2) / (GridDimension.z * CellDimension);

            percentX = Mathf.Clamp01(percentX);
            percentY = Mathf.Clamp01(percentY);
            percentZ = Mathf.Clamp01(percentZ);

            return new Vector3Int(
                Mathf.RoundToInt((GridDimension.x - 1) * percentX),
                Mathf.RoundToInt((GridDimension.y - 1) * percentY),
                Mathf.RoundToInt((GridDimension.z - 1) * percentZ)
                );
        }

        /// <summary>
        /// Ne pas utiliser sur les rooms qui peuvent être pivotées
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Vector3Int WorldAnchorToGridPosition(Vector3 position)
        {
            return WorldToGridPosition(position + Vector3.one * HalfCellDimension);
        }

        public bool IsInGrid(Vector3Int position)
        {
            return position.x >= 0 && position.x < GridDimension.x
                && position.y >= 0 && position.y < GridDimension.y
                && position.z >= 0 && position.z < GridDimension.x;
        }

        public bool IsWorldPositionInGrid(Vector3 worldPosition, float margin = 0)
        {
            float percentX = (-transform.position.x + worldPosition.x + (GridDimension.x * CellDimension) / 2) / (GridDimension.x * CellDimension);
            float percentY = (-transform.position.y + worldPosition.y + (GridDimension.y * CellDimension) / 2) / (GridDimension.y * CellDimension);
            float percentZ = (-transform.position.z + worldPosition.z + (GridDimension.z * CellDimension) / 2) / (GridDimension.z * CellDimension);

            return percentX - margin >= 0 && percentX + margin <= 1
                && percentY - margin >= 0 && percentY + margin <= 1
                && percentZ - margin >= 00 && percentY + margin <= 1;
        }

        public Cell GetCell(Vector3Int position)
        {
            if (IsInGrid(position))
            {
                return GridArray[position.x, position.y, position.z];
            }

            return null;
        }

        public List<Cell> GetCellsInBox(Vector3Int center, Vector3Int dimensions)
        {
            List<Cell> cells = new List<Cell>();

            for (int x = -dimensions.x; x <= dimensions.x; ++x)
            {
                for (int y = -dimensions.y; y <= dimensions.y; ++y)
                {
                    for (int z = -dimensions.z; z <= dimensions.z; ++z)
                    {
                        Cell toAdd = GetCell(center + new Vector3Int(x, y, z));
                        if (toAdd != null)
                            cells.Add(toAdd);
                    }
                }
            }

            return cells;
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

        public void AnchorObject(Vector3Int position, Transform toAnchor)
        {
            toAnchor.position = GridToWorldPosition(position) - Vector3.one * HalfCellDimension;
        }

        public void AnchorConstructionBlock(Vector3Int position, ConstructionBlock constructionBlock)
        {
            AnchorObject(position, constructionBlock.transform);
            GridArray[position.x, position.y, position.z].ConstructionBlock = constructionBlock;
        }

        public List<Cell> GetNeighboursHandleStairs(Cell from)
        {
            List<Cell> neighbours = new List<Cell>();

            if (from.parent != null && from.parent.Position.y != from.Position.y)
            {
                Vector3Int dir = GetDirection(from.parent.Position, from.Position);
                dir.y = 0;

                List<Vector3Int> pos = new List<Vector3Int>();

                if (from.parent.Position.y < from.Position.y)
                    pos.Add(from.Position + dir * 2 + Vector3Int.up);
                else
                    pos.Add(from.Position + dir * 2 + Vector3Int.down);

                if (dir != new Vector3Int(1, 0, 0))
                    pos.Add(from.Position + new Vector3Int(1, 0, 0));

                if (dir != new Vector3Int(-1, 0, 0))
                    pos.Add(from.Position + new Vector3Int(-1, 0, 0));

                if (dir != new Vector3Int(0, 0, 1))
                    pos.Add(from.Position + new Vector3Int(0, 0, 1));

                if (dir != new Vector3Int(0, 0, 1))
                    pos.Add(from.Position + new Vector3Int(0, 0, -1));

                AddNeighbours(neighbours, pos.ToArray());
            }
            else
            {
                Vector3Int[] positions = new Vector3Int[12];

                positions[0] = from.Position + new Vector3Int(-1, 0, 0);
                positions[1] = from.Position + new Vector3Int(1, 0, 0);
                positions[2] = from.Position + new Vector3Int(0, 0, 1);
                positions[3] = from.Position + new Vector3Int(0, 0, -1);

                positions[4] = from.Position + new Vector3Int(-3, 0, 0) + Vector3Int.up;
                positions[5] = from.Position + new Vector3Int(3, 0, 0) + Vector3Int.up;
                positions[6] = from.Position + new Vector3Int(0, 0, 3) + Vector3Int.up;
                positions[7] = from.Position + new Vector3Int(0, 0, -3) + Vector3Int.up;

                positions[8] = from.Position + new Vector3Int(-3, 0, 0) + Vector3Int.down;
                positions[9] = from.Position + new Vector3Int(3, 0, 0) + Vector3Int.down;
                positions[10] = from.Position + new Vector3Int(0, 0, 3) + Vector3Int.down;
                positions[11] = from.Position + new Vector3Int(0, 0, -3) + Vector3Int.down;

                AddNeighbours(neighbours, positions);
            }

            return neighbours;
        }

        public List<Cell> GetNeighbours(Cell from)
        {
            List<Cell> neighbours = new List<Cell>();
            Vector3Int[] positions = new Vector3Int[12];

            positions[0] = from.Position + new Vector3Int(-1, 0, 0);
            positions[1] = from.Position + new Vector3Int(1, 0, 0);
            positions[2] = from.Position + new Vector3Int(0, 0, 1);
            positions[3] = from.Position + new Vector3Int(0, 0, -1);
            AddNeighbours(neighbours, positions);
            return neighbours;
        }

        private void AddNeighbours(List<Cell> neighbours, Vector3Int[] positions)
        {
            for (int i = 0; i < positions.Length; ++i)
            {
                if (positions[i].x >= 0 && positions[i].x < GridDimension.x
                    && positions[i].y >= 0 && positions[i].y < GridDimension.y
                    && positions[i].z >= 0 && positions[i].z < GridDimension.z)
                {
                    neighbours.Add(GridArray[positions[i].x, positions[i].y, positions[i].z]);
                }
            }
        }

        public void Traverse(Action<Vector3Int> onCell)
        {
            for (int x = 0; x < GridDimension.x; ++x)
            {
                for (int y = 0; y < GridDimension.y; ++y)
                {
                    for (int z = 0; z < GridDimension.z; ++z)
                    {
                        onCell.Invoke(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        public List<Cell> path = new List<Cell>();
        public bool DrawPath;
        public bool DrawRoom;
        public bool DrawGrid;
        public bool DrawHallway;
        public bool DrawStair;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            HallwayBlock block = Selection.activeGameObject?.GetComponent<HallwayBlock>();
            if (block != null)
            {
                for (int i = 0; i < block.PathPositions.Count; ++i)
                {
                    Cell cell = GetCell(block.PathPositions[i]);
                    if (cell.stairSet != null && cell.stairSet[0] != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawCube(GridToWorldPosition(cell.stairSet[0].Position), 1.03f * Vector3.one * CellDimension);
                        Gizmos.DrawCube(GridToWorldPosition(cell.stairSet[1].Position), 1.03f * Vector3.one * CellDimension);
                        Gizmos.DrawCube(GridToWorldPosition(cell.stairSet[2].Position), 1.03f * Vector3.one * CellDimension);
                        Gizmos.color = Color.blue;
                        Gizmos.DrawCube(GridToWorldPosition(cell.stairSet[3].Position), 1.03f * Vector3.one * CellDimension);

                    }
                    Gizmos.color = Color.yellow;

                    Gizmos.DrawCube(GridToWorldPosition(block.PathPositions[i]), 1.03f * Vector3.one * CellDimension);
                }
            }

            if (GridArray != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(transform.position, GridDimension * CellDimension);

                for (int x = 0; x < GridDimension.x; ++x)
                {
                    for (int y = 0; y < GridDimension.y; ++y)
                    {
                        for (int z = 0; z < GridDimension.z; ++z)
                        {
                            if (GridArray[x, y, z].CellType == CellType.RoomDoor && DrawRoom)
                            {
                                Gizmos.color = Color.green;
                                Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);
                            }
                            else if(GridArray[x, y, z].CellType == CellType.Room && DrawRoom)
                            {
                                Gizmos.color = Color.black;
                                Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);

                            }
                            else if (GridArray[x, y, z].isHallway && DrawHallway)
                            {
                                Gizmos.color = Color.red;
                                Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);
                            }
                            else if (GridArray[x, y, z].IsStair && DrawStair)
                            {
                                Gizmos.color = Color.blue;
                                Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);
                            }

                            if (DrawGrid)
                            {
                                Gizmos.color = Color.red;
                                Gizmos.DrawSphere(GridToWorldPosition(GridArray[x, y, z].Position) - Vector3.one * HalfCellDimension, 1);
                                //Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);
                            }

                            *//*else
                            {
                                Gizmos.color = Color.white;
                                Gizmos.DrawWireCube(GridToWorldPosition(GridArray[x, y, z].Position), Vector3.one * CellDimension);
                            }*//*
                        }
                    }
                }

                if (path != null && DrawPath)
                {
                    for (int i = 0; i < path.Count; ++i)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(GridToWorldPosition(path[i].Position), Vector3.one * CellDimension);
                    }

                }

                if (IsInGrid(POSITION_DEBUG))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawCube(GridToWorldPosition(POSITION_DEBUG), Vector3.one * CellDimension);
                    POSITION_CELLTYPE = GridArray[POSITION_DEBUG.x, POSITION_DEBUG.y, POSITION_DEBUG.z]?.CellType.ToString();
                }
            }
        }
#endif
    }

}

*/