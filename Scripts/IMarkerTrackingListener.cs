// Put this on a component inside a marker prefab to be told when the marker
// is lost or found again. MarkerSpawner then leaves showing/hiding that prefab
// to the component instead of switching it off immediately.
public interface IMarkerTrackingListener
{
    void SetMarkerTracked(bool tracked);
}
