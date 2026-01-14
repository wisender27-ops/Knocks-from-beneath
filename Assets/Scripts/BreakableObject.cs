using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    public void Break()
    {
        // Здесь можно добавить звук разламывания или искры перед удалением
        Debug.Log(gameObject.name + " сломан!");
        Destroy(gameObject);
    }
}