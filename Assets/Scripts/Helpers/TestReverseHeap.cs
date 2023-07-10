using Atomix.Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestReverseHeap : MonoBehaviour
{
    public Heap<GridNode> Heap = new Heap<GridNode>(500);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            NavigationCore core = FindObjectOfType<NavigationCore>();   

            foreach(var node in core.GridDictionnary)
            {
                Heap.Add(node.Value);
            }

            while(Heap.Count > 0)
            {
                var item = Heap.RemoveFirst();
                Debug.LogError("FCost + " + item.fCost + " HCost " + item.HCost);
            }
        }
    }
}
