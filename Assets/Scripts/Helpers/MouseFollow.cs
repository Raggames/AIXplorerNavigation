using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseFollow : MonoBehaviour
{
    public Transform Target;

    // Update is called once per frame
    void Update()
    {
        Ray camRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        // For debug purposes
        Debug.DrawRay(camRay.origin, camRay.direction * 50, Color.blue, 15);
        if (Physics.Raycast(camRay, out var hitDebug, float.MaxValue))
        {
            Target.transform.position = hitDebug.point;
        }
    }
}
