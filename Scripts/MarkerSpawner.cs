using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MarkerSpawner : MonoBehaviour
{
    [SerializeField] ARTrackedImageManager manager;
    [SerializeField] GameObject[] prefabs;      // names must match reference image names
    [SerializeField] bool scaleToImageWidth = true;

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
        }

        foreach (var img in args.updated)
        {
            bool visible = img.trackingState == TrackingState.Tracking;
            foreach (Transform child in img.transform) child.gameObject.SetActive(visible);
        }
    }
}
