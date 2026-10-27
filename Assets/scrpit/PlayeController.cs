using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Jump & Physics Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private bool enableDoubleJump = true; // Nova opção ativável no Inspector

    [Header("Game Feel (Advanced)")]
    [SerializeField] private float coyoteTime = 0.15f; // Tempo que pode pular apos cair
    [SerializeField] private float jumpBufferTime = 0.15f; // Tempo que o pulo fica salvo
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool canDoubleJump;

    [Header("Dash Settings")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1f;

    private bool canDash = true;
    private bool isDashing;
    private float dashDirection = 1f;

    [Header("Ground Check (Robust)")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.2f); // Transformamos em Caixa (Box)
    [SerializeField] private LayerMask groundLayer;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private TextMeshProUGUI coinText;

    [Header("Animation Settings")]
    [SerializeField] private string walkingParameter = "isMoving";
    [SerializeField] private string onGroundParameter = "onGround";

    private int coinCounter = 0;
    private PlayerActionMap inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private bool isGrounded;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        inputActions = new PlayerActionMap();
        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();

        // Registamos apenas o acionamento do botão para o Buffer
        jumpAction.performed += context => jumpBufferCounter = jumpBufferTime;
        jumpAction.canceled += context => jumpBufferCounter = 0f;
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    private void Update()
    {
        if (isDashing) return;

        moveInput = moveAction.ReadValue<Vector2>();

        CheckGrounded();
        HandleJumpLogic();
        Flip();
        UpdateAnimations();

        // Dash Input
        if (Keyboard.current != null)
        {
            if ((Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame) && canDash)
            {
                StartCoroutine(PerformDash());
            }
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
            return;
        }

        // Movimentação Horizontal
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }

    private void CheckGrounded()
    {
        // Usamos OverlapBox para uma deteção mais precisa que o OverlapCircle
        Collider2D collider = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0f, groundLayer);
        isGrounded = collider != null;

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime; // Reseta o tempo de tolerância de queda
            canDoubleJump = enableDoubleJump; // Recarrega o pulo duplo
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime; // Vai diminuindo se não estiver no chão
        }
    }

    private void HandleJumpLogic()
    {
        // Diminui o tempo guardado na "memória" do botão de pulo
        jumpBufferCounter -= Time.deltaTime;

        // Se o jogador tentou pular (buffer ativo)
        if (jumpBufferCounter > 0f)
        {
            // Pulo Normal (usando Coyote Time, mesmo se acabou de escorregar da borda)
            if (coyoteTimeCounter > 0f)
            {
                ExecuteJump();
                jumpBufferCounter = 0f;
            }
            // Pulo Duplo
            else if (canDoubleJump)
            {
                ExecuteJump();
                canDoubleJump = false;
                jumpBufferCounter = 0f;
            }
        }

        // FÍSICA ESTILO MARIO: Cai mais rápido, e pula mais baixo se soltar o botão
        if (rb.linearVelocity.y < 0) // Caindo
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpAction.IsPressed()) // Subindo, mas soltou o botão
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }

    private void ExecuteJump()
    {
        // Zera a velocidade vertical antes de aplicar a força para garantir consistência no pulo duplo
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.linearVelocity += Vector2.up * jumpForce;
        coyoteTimeCounter = 0f; // Zera para não pular de novo no ar
    }

    private void Flip()
    {
        if (moveInput.x != 0)
        {
            float direction = Mathf.Sign(moveInput.x);
            Vector3 currentScale = transform.localScale;
            currentScale.x = direction * Mathf.Abs(currentScale.x);
            transform.localScale = currentScale;
            dashDirection = direction; // Atualiza a direção do dash
        }
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

        // Se estiver parado, dá dash para onde está a olhar
        if (moveInput.x == 0) dashDirection = Mathf.Sign(transform.localScale.x);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // Desenha a nova caixa de colisão no Editor para você ajustar perfeitamente
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
        }
    }

    public void ChangeTextCoin()
    {
        coinCounter += 1;
        if (coinText != null) coinText.text = coinCounter.ToString();
    }

    public void Die()
    {
        Destroy(gameObject);
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }
}