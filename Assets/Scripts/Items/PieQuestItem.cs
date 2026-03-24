using UnityEngine;

public class PieQuestItem : MonoBehaviour
{
    [Header("Состояние пирога")]
    public bool isHeated = false;

    [Header("Поедание")]
    public AudioClip eatSfx;
    public float eatDuration = 5f;
}
