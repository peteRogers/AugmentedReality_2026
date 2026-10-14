using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Tap (or click, in XR Simulation) on a detected plane to place a prefab there.
// Put this on XR Origin (AR Rig), which already has an AR Raycast Manager.
public class TapToPlace : MonoBehaviour
{
    [SerializeField] ARRaycastManager raycastManager;
    [SerializeField] GameObject[] prefabs;          // one is picked per tap
    [SerializeField] bool randomPrefab = false;     // false = cycle through in order
    [SerializeField] float size = 0.2f;             // metres wide, for prefabs built 1 unit wide
    [SerializeField] bool faceCamera = true;        // turn each object to face you
    [SerializeField] int maxObjects = 10;           // oldest is removed beyond this

    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    readonly Queue<GameObject> placed = new Queue<GameObject>();
    int nextIndex;

    void Update()
    {
        var pointer = Pointer.current;               // touch on phone, mouse in the editor
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (prefabs == null || prefabs.Length == 0) return;

        // Ignore taps on UI buttons.
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (!raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon)) return;

        Pose pose = hits[0].pose;                    // closest plane hit
        GameObject prefab = PickPrefab();
        GameObject go = Instantiate(prefab, pose.position, pose.rotation);
        go.transform.localScale = Vector3.one * size;

        if (faceCamera && Camera.main)
        {
            Vector3 up = pose.rotation * Vector3.up;
            Vector3 toCam = Vector3.ProjectOnPlane(Camera.main.transform.position - pose.position, up);
            if (toCam.sqrMagnitude > 0.0001f)
                go.transform.rotation = Quaternion.LookRotation(toCam, up);
        }

        placed.Enqueue(go);
        while (placed.Count > maxObjects) Destroy(placed.Dequeue());
    }

    GameObject PickPrefab()
    {
        if (randomPrefab) return prefabs[Random.Range(0, prefabs.Length)];
        var p = prefabs[nextIndex];
        nextIndex = (nextIndex + 1) % prefabs.Length;
        return p;
    }

    // Call from a UI button to clear everything placed so far.
    public void ClearAll()
    {
        while (placed.Count > 0) Destroy(placed.Dequeue());
    }
}
