// PlayerController.cs
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float baseSpeed = 10f;
    public float speedAdjustStep = 2f;
    public float boostMultiplier = 2f;
    public float strafeSpeed = 5f;
    public float mouseSensitivity = 2f;

    public GameObject bulletPrefab; // prefab from Project window
    public Transform firePoint;
    public float bulletSpeed = 50f;

    private float currentSpeed;
    private Rigidbody rb;
    private float yaw;
    private float pitch;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        currentSpeed = baseSpeed;
    }

    void Update()
    {
        HandleMouseLook();
        HandleSpeedAdjustment();
        HandleShooting();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMouseLook()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleSpeedAdjustment()
    {
        if (Input.GetKeyDown(KeyCode.W)) currentSpeed += speedAdjustStep;
        if (Input.GetKeyDown(KeyCode.S))
        {
            currentSpeed = 0f;
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }
    }

    void HandleMovement()
    {
        float strafe = 0f;
        if (Input.GetKey(KeyCode.A)) strafe = -strafeSpeed;
        if (Input.GetKey(KeyCode.D)) strafe = strafeSpeed;
        float speed = currentSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= boostMultiplier;

        Vector3 forwardMovement = transform.forward * speed;
        Vector3 strafeMovement = transform.right * strafe;
        if (rb != null) rb.linearVelocity = forwardMovement + strafeMovement;
    }

    void HandleShooting()
    {
        if (Input.GetMouseButton(0))
        {
            Fire();
        }
    }

    public void Fire()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.ownerTag = gameObject.tag;
            b.shooterId = "player_1";
            b.serverIP = "127.0.0.1";
            b.serverPort = 5000;
        }

        Rigidbody rbBullet = bullet.GetComponent<Rigidbody>();
        if (rbBullet != null) rbBullet.linearVelocity = firePoint.forward * bulletSpeed;

        Collider bulletCol = bullet.GetComponent<Collider>();
        Collider playerCol = GetComponent<Collider>();
        if (bulletCol != null && playerCol != null) Physics.IgnoreCollision(bulletCol, playerCol);

        Destroy(bullet, 8f);
    }
}
