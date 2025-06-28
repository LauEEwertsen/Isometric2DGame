using UnityEngine;

public class Spin : MonoBehaviour
{
    [SerializeField] float speed = 1f;

    private void FixedUpdate()
    {
        transform.Rotate(0,0, speed);
    }

}
