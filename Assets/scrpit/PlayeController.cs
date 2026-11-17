using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformController : MonoBehaviour
{
    private const float GroundLockAfterJump = 0.1f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Jump & Physics")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private bool enableDoubleJump = true;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.2f);
    [SerializeField] private LayerMask groundLayer;

    [Header("References")]
    [SerializeField] private TextMeshProUGUI coinText;

    [Header("Animation")]
    [SerializeField] private string walkingParameter = "isMoving";
    [SerializeField] private string onGroundParameter = "onGround";

    private Rigidbody2D rb;
    private Animator animator;
    private PlayerActionMap inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;

    private readonly Collider2D[] groundHits = new Collider2D[8];
    private ContactFilter2D groundFilter;

    private Vector2 moveInput;
    private bool isGrounded;
    private bool canDoubleJump;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float groundLockTimer;

    private bool canDash = true;
    private bool isDashing;
    private float dashDirection = 1f;

    private int coinCounter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();

        inputActions = new PlayerActionMap();
        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;

        groundFilter = new ContactFilter2D { useTriggers = false };
        groundFilter.SetLayerMask(groundLayer);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        jumpAction.started += OnJumpStarted;
    }

    private void OnDisable()
    {
        jumpAction.started -= OnJumpStarted;
        moveAction.Disable();
        jumpAction.Disable();
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        jumpBufferCounter = jumpBufferTime;
    }

    private void Update()
    {
        if (isDashing) return;

        moveInput = moveAction.ReadValue<Vector2>();

        Flip();
        UpdateAnimations();

        if (canDash && Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame))
        {
            StartCoroutine(PerformDash());
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
            return;
        }

        CheckGrounded();
        HandleJump();
        ApplyGravityModifiers();

        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }

    private void CheckGrounded()
    {
        groundLockTimer -= Time.fixedDeltaTime;

        int hitCount = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0f, groundFilter, groundHits);
        bool touchingGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            if (groundHits[i].attachedRigidbody != rb)
            {
                touchingGround = true;
                break;
            }
        }

        isGrounded = touchingGround && groundLockTimer <= 0f && rb.linearVelocity.y <= 0.1f;

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            canDoubleJump = enableDoubleJump;
        }
        else
        {
            coyoteTimeCounter -= Time.fixedDeltaTime;
        }
    }

    private void HandleJump()
    {
        jumpBufferCounter -= Time.fixedDeltaTime;

        if (jumpBufferCounter <= 0f) return;

        if (coyoteTimeCounter > 0f)
        {
            ExecuteJump();
        }
        else if (canDoubleJump)
        {
            canDoubleJump = false;
            ExecuteJump();
        }
    }

    private void ExecuteJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        groundLockTimer = GroundLockAfterJump;
        isGrounded = false;
    }

    private void ApplyGravityModifiers()
    {
        float velocityY = rb.linearVelocity.y;
        float gravity = Physics2D.gravity.y * rb.gravityScale * Time.fixedDeltaTime;

        if (velocityY < 0f)
            rb.linearVelocity += Vector2.up * gravity * (fallMultiplier - 1f);
        else if (velocityY > 0f && !jumpAction.IsPressed())
            rb.linearVelocity += Vector2.up * gravity * (lowJumpMultiplier - 1f);
    }

    private void Flip()
    {
        if (moveInput.x == 0f) return;

        dashDirection = Mathf.Sign(moveInput.x);
        Vector3 scale = transform.localScale;
        scale.x = dashDirection * Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        animator.SetBool(walkingParameter, Mathf.Abs(moveInput.x) > 0.01f);
        animator.SetBool(onGroundParameter, isGrounded);
    }

    private IEnumerator PerformDash()
    {
        canDash = false;
        isDashing = true;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
    }

    public void ChangeTextCoin()
    {
        coinCounter++;
        if (coinText != null) coinText.text = coinCounter.ToString();
    }

    public void Die()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}