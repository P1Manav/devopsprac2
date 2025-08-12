// BotController.cs
using UnityEngine;

public class BotController : MonoBehaviour
{
    public Transform firePoint;
    public GameObject bulletPrefab; // prefab from Project window
    public string agentId = "bot_1";
    public string ownerTag = "Bot";
    public float moveSpeed = 12f;
    public float rotationMultiplier = 1f;
    public float fov = 25f;
    public float bulletSpeed = 50f;
    public float fireRate = 0.2f; // seconds between shots
    public float shootDistance = 120f; // only shoot if closer than this

    private float yawDelta, pitchDelta, rollDelta;
    private bool shootRequested;
    private float nextFireTime = 0f;
    private Collider myCollider;

    void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    // Called by PositionSender on main thread
    public void SetPrediction(float yaw, float pitch, float roll, bool shoot)
    {
        yawDelta = yaw;
        pitchDelta = pitch;
        rollDelta = roll;
        shootRequested = shoot;
    }

    void Update()
    {
        // forward movement
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);

        // apply small rotation deltas (server returns degrees-per-step)
        Vector3 rotChange = new Vector3(pitchDelta, yawDelta, rollDelta) * rotationMultiplier * Time.deltaTime * 60f;
        transform.Rotate(rotChange, Space.Self);

        if (shootRequested && Time.time >= nextFireTime && firePoint != null && bulletPrefab != null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Vector3 toPlayer = (player.transform.position - transform.position);
                float dist = toPlayer.magnitude;
                Vector3 dir = toPlayer.normalized;
                float angle = Vector3.Angle(transform.forward, dir);
                if (angle <= fov && dist <= shootDistance)
                {
                    Fire();
                    nextFireTime = Time.time + fireRate;
                }
            }
            shootRequested = false;
        }
    }

    public void Fire()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.ownerTag = ownerTag;
            b.shooterId = agentId;
            b.serverIP = "127.0.0.1";
            b.serverPort = 5000;
        }

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = firePoint.forward * bulletSpeed;

        Collider bulletCol = bullet.GetComponent<Collider>();
        if (bulletCol != null && myCollider != null) Physics.IgnoreCollision(bulletCol, myCollider);

        Destroy(bullet, 8f);
    }
}
