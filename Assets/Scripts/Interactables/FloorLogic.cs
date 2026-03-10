using UnityEngine;

public class FloorLogic : MonoBehaviour
{
    private Animator anim;
    private Collider myCollider;
    
    [Header("Состояние пола")]
    public bool isBroken = false; 

    void Start()
    {
        // Пытаемся найти компоненты на этом же объекте
        anim = GetComponent<Animator>();
        myCollider = GetComponent<Collider>();
    }

    // Метод, который вызывается из PlayerInteraction, если у нас есть лом
    public void Break()
    {
        if (isBroken) return; 

        if (anim != null) anim.SetTrigger("Break"); 
        
        // Отключаем коллайдер, чтобы игрок мог провалиться
        if (myCollider != null) myCollider.enabled = false; 
        
        isBroken = true;
        Debug.Log("Пол успешно сломан ломом!");
    }

    // Метод, который вызывается, если у нас есть молоток
    public void Fix()
    {
        if (!isBroken) return; 

        if (anim != null) anim.SetTrigger("Fix");
        
        // Включаем коллайдер обратно, чтобы по полу можно было ходить
        if (myCollider != null) myCollider.enabled = true; 
        
        isBroken = false;
        Debug.Log("Пол заколочен досками!");
    }
}