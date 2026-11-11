using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MarkerSpawner : MonoBehaviour
{
    [SerializeField] ARTrackedImageManager manager;
    [SerializeField] GameObject[] prefabs;      // names must match reference image names
    [SerializeField] bool scaleToImageWidth = true;

    // Last known "is the marker visible" for each image, so we only react to changes.
    readonly Dictionary<TrackableId, bool> visibleState = new Dictionary<TrackableId, bool>();

    void OnEnable()  => manager.trackablesChanged.AddListener(OnChanged);
    void OnDisable() => manager.trackablesChanged.RemoveListener(OnChanged);

    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var img in args.added)
        {
            var prefab = System.Array.Find(prefabs, p => p.name == img.referenceImage.name);
            if (!prefab)
            {
                Debug.LogWarning($"No prefab named '{img.referenceImage.name}'");
                continue;
            }
            var go = Instantiate(prefab, img.transform);
            if (scaleToImageWidth) go.transform.localScale = Vector3.one * img.size.x;
            visibleState[img.trackableId] = true;
        }

        foreach (var img in args.updated)
        {
            bool visible = img.trackingState == TrackingState.Tracking;

            // On a real device the image's size estimate can change after it's
            // first detected, so keep the content matched to the paper.
            if (scaleToImageWidth && visible)
                foreach (Transform child in img.transform)
                    child.localScale = Vector3.one * img.size.x;

            // Only do something when visibility actually changes.
            if (visibleState.TryGetValue(img.trackableId, out bool wasVisible) && wasVisible == visible)
                continue;
            visibleState[img.trackableId] = visible;

            foreach (Transform child in img.transform)
            {
                // Prefabs with a listener (like BirdWanderer) handle it themselves,
                // e.g. flying away. Everything else is just hidden/shown.
                var listeners = child.GetComponentsInChildren<IMarkerTrackingListener>(true);
                if (listeners.Length > 0)
                    foreach (var listener in listeners) listener.SetMarkerTracked(visible);
                else
                    child.gameObject.SetActive(visible);
            }
        }
    }
}
