using UnityEngine;

namespace Atomix.Pathfinding
{
    public enum CellType
    {
        Walkable,
        Unwalkable,
    }

    public class Node : IHeapItem<Node>
    {
        public Vector3Int Position;
        public Vector3 WorldPosition;

        private CellType _cellType;
        public CellType CellType
        {
            get
            {
                return _cellType;
            }
            set
            {
                _cellType = value;
            }
        }

        public Node Parent;

        public float fCost
        {
            get
            {
                return HCost + GCost;
            }
        }

        public float HCost { get; set; }
        public float GCost { get; set; }

        private int _heapIndex;
        public int HeapIndex
        {
            get
            {
                return _heapIndex;
            }
            set
            {
                _heapIndex = value;
            }
        }

        public int CompareTo(Node nodeToCompare)
        {
            int compare = fCost.CompareTo(nodeToCompare.fCost);
            if (compare == 0)
            {
                compare = HCost.CompareTo(nodeToCompare.HCost);
            }
            return -compare;
        }
    }
}
