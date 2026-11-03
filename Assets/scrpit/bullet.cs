using UnityEngine;

public class bullet : MonoBehaviour
{
    private Rigidbody2D rb;
    public float initialVelocity;
    public bool lookingRight;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if(lookingRight)
        {
            rb.linearVelocity = Vector2.right * initialVelocity;
        }
        else
        {
            rb.linearVelocity = Vector2.left * initialVelocity;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
