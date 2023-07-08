using UnityEngine;

namespace Atomix.Pathfinding
{
    public enum NodeState
    {
        Walkable,
        Unwalkable,
    }

    public class GridNode : IHeapItem<GridNode>
    {
        public Vector3Int Position;
        public Vector3 WorldPosition;

        private NodeState _cellType;
        public NodeState NodeState
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

        public GridNode Parent;

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

        public int CompareTo(GridNode nodeToCompare)
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
