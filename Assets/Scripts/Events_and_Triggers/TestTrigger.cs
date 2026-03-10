using UnityEngine;

public class TestTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("ЕБАТЬ, СРАБОТАЛО! Я вошел в: " + other.name);
    }
}