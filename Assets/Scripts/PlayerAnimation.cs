using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform root;      // el Player (root)
    [SerializeField] private float damp = 0.1f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    Vector3 lastPos;

    void Start() => lastPos = root.position;

    void Update()
    {
        Vector3 delta = root.position - lastPos;
        delta.y = 0f; // ignorar caídas y saltos
        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = root.position;

        animator.SetFloat(SpeedHash, speed, damp, Time.deltaTime);
    }
}