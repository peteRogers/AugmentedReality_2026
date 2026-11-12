using UnityEngine;

// Walks an object from point to point, always turning to face where it's going.
//
// Each time it reaches a point it picks the next one (either the next of your
// waypoints, or a random spot in the area), stops and turns on the spot to face
// it, then walks there, steering as it goes so it never slides sideways or
// backwards. Optionally waits a moment at each point before moving on.
//
// Put this on the MODEL (the child inside your prefab), not the prefab root.
// It moves in the parent's local space, where the paper runs from -0.5 to +0.5
// (when the prefab is built 1 unit wide and scaled by MarkerSpawner/TapToPlace).
public class PointWalker : MonoBehaviour
{
    [Header("Where to go")]
    [Tooltip("Optional. If set, walks these in order (their positions are used). If empty, picks random points in the area.")]
    [SerializeField] Transform[] waypoints;
    [SerializeField] bool loopWaypoints = true;     // false = ping-pong back and forth

    [Header("Random area (in prefab units, paper = -0.5 to 0.5)")]
    [SerializeField] float halfWidth = 0.4f;
    [SerializeField] float halfDepth = 0.4f;
    [SerializeField] float minHopDistance = 0.2f;   // random points at least this far away

    [Header("Movement")]
    [SerializeField] float walkSpeed = 0.15f;       // paper widths per second
    [SerializeField] float turnSpeed = 180f;        // degrees per second
    [Tooltip("Only start walking once facing within this many degrees of the target.")]
    [SerializeField] float startWalkingAngle = 15f;
    [SerializeField] float arriveDistance = 0.01f;

    [Header("Model facing")]
    [Tooltip("Use this if your model doesn't face +Z. E.g. 90, -90 or 180.")]
    [SerializeField] float modelYawOffset = 0f;

    [Header("Waiting at each point")]
    [SerializeField] Vector2 waitTime = new Vector2(0.5f, 1.5f);

    [Header("Animator (optional)")]
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";

    enum State { Turning, Walking, Waiting }

    State state;
    Vector3 target;
    float heading;          // direction of travel, degrees around Y (local)
    float waitTimer;
    float baseY;
    int waypointIndex;
    int waypointStep = 1;

    void Start()
    {
        baseY = transform.localPosition.y;
        heading = transform.localEulerAngles.y - modelYawOffset;
        PickNextTarget();
        state = State.Turning;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float speed = 0f;

        switch (state)
        {
            case State.Turning:
                // Turn on the spot until roughly facing the target.
                if (TurnTowardsTarget(dt) <= startWalkingAngle)
                    state = State.Walking;
                break;

            case State.Walking:
                // Keep steering while walking so it curves onto the target.
                // If it's somehow ended up facing well away (e.g. overshot),
                // stop and turn on the spot again instead of circling.
                if (TurnTowardsTarget(dt) > 60f)
                {
                    state = State.Turning;
                    break;
                }

                Vector3 pos = transform.localPosition;
                Vector3 flatPos = new Vector3(pos.x, 0f, pos.z);
                float distance = Vector3.Distance(flatPos, target);

                if (distance <= arriveDistance)
                {
                    state = State.Waiting;
                    waitTimer = Random.Range(waitTime.x, waitTime.y);
                    break;
                }

                // Move along the way we're facing (not straight at the target),
                // so the motion always matches the body's rotation.
                float step = Mathf.Min(walkSpeed * dt, distance);
                Vector3 forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                flatPos += forward * step;
                transform.localPosition = new Vector3(flatPos.x, baseY, flatPos.z);
                speed = walkSpeed;
                break;

            case State.Waiting:
                waitTimer -= dt;
                if (waitTimer <= 0f)
                {
                    PickNextTarget();
                    state = State.Turning;
                }
                break;
        }

        transform.localRotation = Quaternion.Euler(0f, heading + modelYawOffset, 0f);

        if (animator != null && !string.IsNullOrEmpty(speedParameter))
            animator.SetFloat(speedParameter, speed);
    }

    // Rotates the heading toward the target; returns how many degrees are left.
    float TurnTowardsTarget(float dt)
    {
        Vector3 pos = transform.localPosition;
        Vector3 toTarget = target - new Vector3(pos.x, 0f, pos.z);
        if (toTarget.sqrMagnitude < 0.000001f)
            return 0f;

        float desired = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        heading = Mathf.MoveTowardsAngle(heading, desired, turnSpeed * dt);
        return Mathf.Abs(Mathf.DeltaAngle(heading, desired));
    }

    void PickNextTarget()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            Transform wp = waypoints[waypointIndex];
            Vector3 p = wp != null ? ToParentSpace(wp.position) : Vector3.zero;
            target = new Vector3(p.x, 0f, p.z);
            AdvanceWaypointIndex();
            return;
        }

        // Random point in the area, not too close to where we are now.
        Vector3 here = transform.localPosition;
        here.y = 0f;
        for (int i = 0; i < 20; i++)
        {
            target = new Vector3(Random.Range(-halfWidth, halfWidth), 0f, Random.Range(-halfDepth, halfDepth));
            if (Vector3.Distance(target, here) >= minHopDistance)
                break;
        }
    }

    void AdvanceWaypointIndex()
    {
        if (waypoints.Length == 1) return;

        if (loopWaypoints)
        {
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
        }
        else
        {
            if (waypointIndex + waypointStep < 0 || waypointIndex + waypointStep >= waypoints.Length)
                waypointStep = -waypointStep;
            waypointIndex += waypointStep;
        }
    }

    Vector3 ToParentSpace(Vector3 worldPos)
    {
        return transform.parent != null ? transform.parent.InverseTransformPoint(worldPos) : worldPos;
    }

    void OnDrawGizmosSelected()
    {
        Transform space = transform.parent;
        Gizmos.matrix = space != null ? space.localToWorldMatrix : Matrix4x4.identity;

        if (waypoints == null || waypoints.Length == 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(0f, transform.localPosition.y, 0f),
                                new Vector3(halfWidth * 2f, 0f, halfDepth * 2f));
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Vector3 t = new Vector3(target.x, transform.localPosition.y, target.z);
            Gizmos.DrawLine(transform.localPosition, t);
            Gizmos.DrawWireSphere(t, 0.02f);
        }
    }
}
