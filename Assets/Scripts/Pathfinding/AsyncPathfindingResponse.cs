using System;
using System.Collections.Generic;

namespace Atomix.Pathfinding
{
    public partial class Pathfinder
    {
        struct AsyncPathfindingResponse
        {
            public bool IsCompletePath;
            public List<GridNode> Path;
            public Action<bool, List<GridNode>> OnComputedCallback;

            public AsyncPathfindingResponse(bool isCompletePath, List<GridNode> path, Action<bool, List<GridNode>> onComputedCallback)
            {
                IsCompletePath = isCompletePath;
                Path = path;
                OnComputedCallback = onComputedCallback;
            }
        }
    }
}
