using Atomix.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Atomix.Helpers
{
    public class MouseGridDetection : MonoBehaviour
    {
        public float DetectionRadius = 5;
        public List<Vector2Int> currentNodesInRadius = new List<Vector2Int>();

        private NavigationCore _navigationCore;

        private void Awake()
        {
            _navigationCore = FindObjectOfType<NavigationCore>();
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl))
            {
                currentNodesInRadius.Clear();

                Ray camRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                // For debug purposes
                Debug.DrawRay(camRay.origin, camRay.direction * 50, Color.blue, 15);
                if (Physics.Raycast(camRay, out var hitDebug, float.MaxValue))
                {
                    Debug.DrawLine(hitDebug.point - Vector3.forward * 1f, hitDebug.point + Vector3.forward * 1f, Color.red, .1f);
                    Debug.DrawLine(hitDebug.point - Vector3.right * 1f, hitDebug.point + Vector3.right * 1f, Color.red, .1f);
                    Debug.DrawLine(hitDebug.point - Vector3.up * 1f, hitDebug.point + Vector3.up * 1f, Color.red, .1f);

                    currentNodesInRadius = _navigationCore.FindNodesPositionsInRange(hitDebug.point, DetectionRadius);
                    _navigationCore.CreatePotentialNodesInRange(hitDebug.point, DetectionRadius);
                    _navigationCore.CurrentClosestNodePosition = _navigationCore.WorldToGridPosition(hitDebug.point);
                }         
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(420, 0, 200, 40), "Hold LEFT CTRL to paint terrain navigation grid.");
        }

        private void OnDrawGizmos()
        {
            for(int i = 0; i < currentNodesInRadius.Count; i++)
            {
                Gizmos.color = Color.yellow;

                Vector3 pos = _navigationCore.GridToWorldPositionFlattened(currentNodesInRadius[i].x, currentNodesInRadius[i].y);

                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 1000, Vector3.down, out hit))
                {
                    pos.y = hit.point.y;
                }

                Gizmos.DrawSphere(pos, .7f);
            }
        }
    }
}
