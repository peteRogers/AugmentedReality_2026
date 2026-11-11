using UnityEngine;

// A bird that:
//   1. flies in from the sky and lands on the marker,
//   2. wanders around on foot (like Wanderer),
//   3. every so often takes off, circles above the marker, and lands again,
//   4. flies away if the marker is lost, and flies back in when it's found.
//
// Put this on the MODEL (a direct child of the prefab root), not the root.
// It moves in the root's local space, where the marker runs from -0.5 to +0.5.
//
// Animations: drives the Animator Controller's own parameters:
//   isFlying (bool)  - true = take off / fly, false = land
//   speed    (float) - walking speed, used for idle <-> walking
public class BirdWanderer : MonoBehaviour, IMarkerTrackingListener
{
    [Header("Animator")]
    [SerializeField] Animator animator;
    [SerializeField] string flyState = "fly";            // state to start in when arriving from the sky
    [SerializeField] string flyingParameter = "isFlying";
    [SerializeField] string speedParameter = "speed";

    [Header("Flight")]
    [SerializeField] float flySpeed = 1f;          // marker widths per second
    [SerializeField] float turnSmoothing = 6f;     // higher = turns faster in the air
    [SerializeField] float maxPitch = 25f;         // max nose up/down tilt

    [Header("Arriving from the sky")]
    [SerializeField] float startHeight = 1.5f;
    [SerializeField] float startDistance = 1.5f;
    [SerializeField] float swoopHeight = 0.2f;     // extra height in the middle of the approach
    [Range(0.5f, 1f)] [SerializeField] float landAnimAt = 0.8f; // start landing anim at this fraction of the approach
    [SerializeField] float landSettleTime = 0.8f;  // time for the landing anim to finish
    [SerializeField] bool landAtRandomSpot = true; // false = always land in the middle

    [Header("Flying around now and then")]
    [SerializeField] Vector2 flyAroundAfter = new Vector2(8f, 12f); // seconds on the ground before taking off
    [SerializeField] float takeOffDelay = 0.3f;    // seconds of takeOff anim before leaving the ground
    [SerializeField] float cruiseHeight = 0.6f;    // circling height above the marker
    [SerializeField] float circleRadius = 0.5f;
    [SerializeField] Vector2 circleTime = new Vector2(4f, 7f);

    [Header("Marker lost")]
    [SerializeField] float lostDelay = 1f;         // ignore tracking blips shorter than this
    [SerializeField] float leaveDistance = 2f;
    [SerializeField] float leaveHeight = 1.5f;

    [Header("Area (in prefab units, marker = -0.5 to 0.5)")]
    [SerializeField] float halfWidth = 0.4f;
    [SerializeField] float halfDepth = 0.4f;
    [SerializeField] float edgeMargin = 0.12f;

    [Header("Walking")]
    [SerializeField] float walkSpeed = 0.15f;
    [Range(0f, 1f)] [SerializeField] float speedVariation = 0.4f;
    [SerializeField] float maxTurnRate = 90f;
    [SerializeField] float wanderFrequency = 0.3f;
    [SerializeField] float edgeTurnRate = 200f;

    [Header("Pauses")]
    [SerializeField] Vector2 walkTime = new Vector2(3f, 8f);
    [Range(0f, 1f)] [SerializeField] float pauseChance = 0.5f;
    [SerializeField] Vector2 pauseTime = new Vector2(0.5f, 2f);
    [SerializeField] Vector2 idleAfterLanding = new Vector2(1f, 2f);

    enum Phase { FlyingIn, Landing, Walking, TakingOff, Circling, Leaving, Gone }
    Phase phase;

    // Current flight path (a curve from A through-ish B to C)
    Vector3 pathA, pathB, pathC;
    float pathT, pathDuration;

    float phaseTimer;      // landing settle / circling time / time until next fly-around
    float groundTimer;     // takeOff anim time before lifting off
    bool landAnimStarted;
    float circleAngle, circleDir;

    bool markerTracked = true;
    float lostTimer;

    // Walking
    float heading;
    float noiseSeed;
    float noiseTime;
    float walkTimer;
    float pauseTimer;
    float groundY;
    float currentSpeed;

    void Start()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();

        // The height the prefab places the bird at is treated as "the ground".
        groundY = transform.localPosition.y;
        noiseSeed = Random.Range(0f, 1000f);

        if (animator)
        {
            // Don't reset to idle if the bird gets hidden and shown again.
            animator.keepAnimatorStateOnDisable = true;
            // This script does all the moving; root motion would push it off the marker.
            animator.applyRootMotion = false;

            if (!animator.HasState(0, Animator.StringToHash(flyState)))
                Debug.LogWarning($"BirdWanderer: no state called '{flyState}' in the Animator " +
                                 "(check spelling and capitals).", this);
        }

        ArriveFromSky();
    }

    void Update()
    {
        // Marker gone for long enough? Fly away.
        if (!markerTracked && phase != Phase.Leaving && phase != Phase.Gone)
        {
            lostTimer -= Time.deltaTime;
            if (lostTimer <= 0f) BeginLeave();
        }

        switch (phase)
        {
            case Phase.FlyingIn:  UpdateFlyingIn();  break;
            case Phase.Landing:   UpdateLanding();   break;
            case Phase.Walking:   UpdateWalking();   break;
            case Phase.TakingOff: UpdateTakingOff(); break;
            case Phase.Circling:  UpdateCircling();  break;
            case Phase.Leaving:   UpdateLeaving();   break;
        }
    }

    // Called by MarkerSpawner when the marker is lost or found.
    public void SetMarkerTracked(bool tracked)
    {
        markerTracked = tracked;
        if (!tracked)
        {
            lostTimer = lostDelay;
            return;
        }

        if (phase == Phase.Gone)
        {
            gameObject.SetActive(true);
            ArriveFromSky();
        }
        else if (phase == Phase.Leaving && groundTimer <= 0f)
        {
            // Already in the air on the way out: turn round and come back.
            BeginFlyIn(transform.localPosition, PickLandingSpot());
        }
        // If it's just starting to take off to leave, it finishes leaving and
        // then comes straight back (see UpdateLeaving).
    }

    // ---------------- Arriving / landing ----------------

    void ArriveFromSky()
    {
        Vector3 landing = PickLandingSpot();
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 side = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        Vector3 from = landing + side * startDistance + Vector3.up * startHeight;

        transform.localPosition = from;
        FaceDirection(landing - from, snap: true);

        if (animator) animator.Play(flyState, 0, 0f); // skip idle -> takeOff, we're already airborne
        BeginFlyIn(from, landing);
    }

    void BeginFlyIn(Vector3 from, Vector3 to)
    {
        Vector3 mid = Vector3.Lerp(from, to, 0.5f);
        mid.y = from.y + swoopHeight;
        SetPath(from, mid, to);
        landAnimStarted = false;
        SetFlying(true);
        phase = Phase.FlyingIn;
    }

    void UpdateFlyingIn()
    {
        bool arrived = FollowPath(easeIn: false, easeOut: true);

        if (!landAnimStarted && pathT >= landAnimAt)
        {
            landAnimStarted = true;
            SetFlying(false); // fly -> landing
        }

        if (arrived)
        {
            transform.localPosition = pathC;
            heading = transform.localEulerAngles.y;
            transform.localRotation = Quaternion.Euler(0f, heading, 0f);
            if (!landAnimStarted) SetFlying(false);
            phaseTimer = landSettleTime;
            phase = Phase.Landing;
        }
    }

    void UpdateLanding()
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            pauseTimer = Random.Range(idleAfterLanding.x, idleAfterLanding.y);
            phaseTimer = Random.Range(flyAroundAfter.x, flyAroundAfter.y);
            currentSpeed = 0f;
            phase = Phase.Walking;
        }
    }

    // ---------------- Walking ----------------

    void UpdateWalking()
    {
        float targetSpeed = 0f;

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            heading += Mathf.Sin(Time.time * 2f) * 30f * Time.deltaTime;
            if (pauseTimer <= 0f) walkTimer = Random.Range(walkTime.x, walkTime.y);
        }
        else
        {
            noiseTime += Time.deltaTime * wanderFrequency;
            Vector3 pos = transform.localPosition;

            float wander = Mathf.PerlinNoise(noiseSeed, noiseTime) * 2f - 1f;
            float turn = wander * maxTurnRate;

            float edgeX = Mathf.InverseLerp(halfWidth - edgeMargin, halfWidth, Mathf.Abs(pos.x));
            float edgeZ = Mathf.InverseLerp(halfDepth - edgeMargin, halfDepth, Mathf.Abs(pos.z));
            float edgeAmount = Mathf.Max(edgeX, edgeZ);
            if (edgeAmount > 0f)
            {
                float toCentre = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(heading, toCentre);
                if (Mathf.Abs(diff) > 45f)
                    turn += Mathf.Sign(diff) * edgeTurnRate * edgeAmount;
            }

            heading += turn * Time.deltaTime;

            float speedNoise = Mathf.PerlinNoise(noiseSeed + 50f, noiseTime * 0.7f);
            float turnSlowdown = 1f - 0.5f * Mathf.Clamp01(Mathf.Abs(turn) / (maxTurnRate + edgeTurnRate));
            targetSpeed = walkSpeed * (1f - speedVariation * speedNoise) * turnSlowdown;

            walkTimer -= Time.deltaTime;
            if (walkTimer <= 0f)
            {
                if (Random.value < pauseChance)
                    pauseTimer = Random.Range(pauseTime.x, pauseTime.y);
                else
                    walkTimer = Random.Range(walkTime.x, walkTime.y);
            }
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, walkSpeed * 2f * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        Vector3 p = transform.localPosition;
        p += transform.localRotation * Vector3.forward * currentSpeed * Time.deltaTime;
        p.x = Mathf.Clamp(p.x, -halfWidth, halfWidth);
        p.z = Mathf.Clamp(p.z, -halfDepth, halfDepth);
        p.y = groundY;
        transform.localPosition = p;

        if (animator) animator.SetFloat(speedParameter, currentSpeed);

        // Time for a fly around?
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f) BeginTakeOff();
    }

    // ---------------- Taking off and circling ----------------

    void BeginTakeOff()
    {
        currentSpeed = 0f;
        SetFlying(true); // idle/walking -> takeOff -> fly
        groundTimer = takeOffDelay;

        // Join the circle at the point nearest to where the bird is standing.
        Vector3 from = transform.localPosition;
        Vector3 outDir = OutwardDirection(from);
        circleAngle = Mathf.Atan2(outDir.x, outDir.z);
        circleDir = Random.value < 0.5f ? 1f : -1f;

        Vector3 entry = CirclePoint(circleAngle);
        Vector3 control = from + Vector3.up * (entry.y - from.y) * 0.7f; // rise mostly upwards first
        SetPath(from, control, entry);
        phase = Phase.TakingOff;
    }

    void UpdateTakingOff()
    {
        if (groundTimer > 0f) { groundTimer -= Time.deltaTime; return; }

        if (FollowPath(easeIn: true, easeOut: false))
        {
            phaseTimer = Random.Range(circleTime.x, circleTime.y);
            phase = Phase.Circling;
        }
    }

    void UpdateCircling()
    {
        circleAngle += circleDir * flySpeed / circleRadius * Time.deltaTime;
        Vector3 pos = CirclePoint(circleAngle);
        FaceDirection(pos - transform.localPosition, snap: false);
        transform.localPosition = pos;

        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f) BeginFlyIn(pos, PickLandingSpot());
    }

    Vector3 CirclePoint(float angle) =>
        new Vector3(Mathf.Sin(angle) * circleRadius, groundY + cruiseHeight, Mathf.Cos(angle) * circleRadius);

    // ---------------- Leaving (marker lost) ----------------

    void BeginLeave()
    {
        bool onGround = phase == Phase.Landing || phase == Phase.Walking;
        currentSpeed = 0f;
        SetFlying(true);
        groundTimer = onGround ? takeOffDelay : 0f;

        Vector3 from = transform.localPosition;
        Vector3 to = from + OutwardDirection(from) * leaveDistance + Vector3.up * leaveHeight;
        Vector3 control = onGround
            ? from + Vector3.up * leaveHeight * 0.4f                         // jump up first
            : from + transform.localRotation * Vector3.forward * 0.3f;       // carry on forwards
        SetPath(from, control, to);
        phase = Phase.Leaving;
    }

    void UpdateLeaving()
    {
        if (groundTimer > 0f) { groundTimer -= Time.deltaTime; return; }

        if (FollowPath(easeIn: true, easeOut: false))
        {
            if (markerTracked)
            {
                ArriveFromSky(); // marker came back while we were leaving
            }
            else
            {
                phase = Phase.Gone;
                gameObject.SetActive(false); // MarkerSpawner turns us back on when the marker returns
            }
        }
    }

    // ---------------- Helpers ----------------

    void SetFlying(bool flying)
    {
        if (!animator) return;
        animator.SetBool(flyingParameter, flying);
        if (flying) animator.SetFloat(speedParameter, 0f);
    }

    Vector3 PickLandingSpot() => landAtRandomSpot
        ? new Vector3(Random.Range(-halfWidth, halfWidth) * 0.6f, groundY,
                      Random.Range(-halfDepth, halfDepth) * 0.6f)
        : new Vector3(0f, groundY, 0f);

    // Direction away from the middle of the marker (or a random one if at the middle).
    Vector3 OutwardDirection(Vector3 pos)
    {
        Vector3 flat = new Vector3(pos.x, 0f, pos.z);
        if (flat.sqrMagnitude > 0.0001f) return flat.normalized;
        float a = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
    }

    void SetPath(Vector3 a, Vector3 b, Vector3 c)
    {
        pathA = a; pathB = b; pathC = c;
        pathT = 0f;
        // Rough curve length. Paths are eased, so take twice as long as constant
        // speed would - that makes the fast end of the curve match flySpeed.
        float length = (Vector3.Distance(a, c) + Vector3.Distance(a, b) + Vector3.Distance(b, c)) * 0.5f;
        pathDuration = Mathf.Max(0.5f, 2f * length / flySpeed);
    }

    // Moves along the current path. Returns true when it reaches the end.
    bool FollowPath(bool easeIn, bool easeOut)
    {
        pathT = Mathf.Min(pathT + Time.deltaTime / pathDuration, 1f);
        float t = pathT;
        if (easeOut) t = 1f - (1f - t) * (1f - t);  // fast then slow (landing)
        else if (easeIn) t = t * t;                 // slow then fast (taking off)

        Vector3 pos = Bezier(pathA, pathB, pathC, t);
        Vector3 ahead = Bezier(pathA, pathB, pathC, Mathf.Min(t + 0.02f, 1f));
        transform.localPosition = pos;
        if ((ahead - pos).sqrMagnitude > 0.000001f) FaceDirection(ahead - pos, snap: false);

        return pathT >= 1f;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // Face along a direction, tilting nose up/down with the climb or dive.
    void FaceDirection(Vector3 dir, bool snap)
    {
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude < 0.000001f) return;
        float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
        Quaternion target = Quaternion.Euler(pitch, yaw, 0f);
        transform.localRotation = snap
            ? target
            : Quaternion.Slerp(transform.localRotation, target, 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));
    }

    void OnDrawGizmosSelected()
    {
        if (!transform.parent) return;
        Gizmos.matrix = transform.parent.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, 0.001f, halfDepth * 2f));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3((halfWidth - edgeMargin) * 2f, 0.001f,
                                                      (halfDepth - edgeMargin) * 2f));

        // Circling path
        Gizmos.color = Color.green;
        float y = Application.isPlaying ? groundY + cruiseHeight : transform.localPosition.y + cruiseHeight;
        for (int i = 0; i < 32; i++)
        {
            float a0 = i / 32f * Mathf.PI * 2f, a1 = (i + 1) / 32f * Mathf.PI * 2f;
            Gizmos.DrawLine(new Vector3(Mathf.Sin(a0) * circleRadius, y, Mathf.Cos(a0) * circleRadius),
                            new Vector3(Mathf.Sin(a1) * circleRadius, y, Mathf.Cos(a1) * circleRadius));
        }

        // Current flight path
        if (Application.isPlaying &&
            (phase == Phase.FlyingIn || phase == Phase.TakingOff || phase == Phase.Leaving))
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = pathA;
            for (int i = 1; i <= 20; i++)
            {
                Vector3 next = Bezier(pathA, pathB, pathC, i / 20f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
