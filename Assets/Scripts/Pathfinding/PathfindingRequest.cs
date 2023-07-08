using Atomix.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atomix.Pathfinding
{
    public struct PathfindingRequest
    {
        public Vector3 startPos;
        public Vector3 targetPos;
        public Action<bool, List<GridNode>> resultCallback;

        public PathfindingRequest(Vector3 startPos, Vector3 targetPos, Action<bool, List<GridNode>> resultCallback)
        {
            this.startPos = startPos;
            this.targetPos = targetPos;
            this.resultCallback = resultCallback;
        }
    }
}
