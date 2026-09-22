using UnityEngine;

namespace KnocksFromBeneath
{

// Вешается на тот же GameObject, что и PlacementZone комнаты. Сама выключает
// назначенные предметы в момент старта сцены (не нужно вручную снимать галочку
// активности у каждого объекта в инспекторе) и включает их обратно, когда в зону
// поставят коробку — видимый прогресс "обживания" дома. Подписывается на
// PlacementZone.onBoxPlaced сама, чтобы в инспекторе нужно было расставить только
// сами предметы, а не событие.
[RequireComponent(typeof(PlacementZone))]
public class RoomRevealZone : MonoBehaviour
{
    [Tooltip("Предметы комнаты — скрываются при старте сцены и появятся при установке коробки в эту зону.")]
    [SerializeField] private GameObject[] itemsToReveal;

    private bool _revealed;

    void Awake()
    {
        GetComponent<PlacementZone>().onBoxPlaced.AddListener(RevealItems);
        HideItems();
    }

    void HideItems()
    {
        if (itemsToReveal == null) return;
        for (int i = 0; i < itemsToReveal.Length; i++)
        {
            if (itemsToReveal[i] != null)
                itemsToReveal[i].SetActive(false);
        }
    }

    public void RevealItems()
    {
        if (_revealed) return;
        _revealed = true;

        if (itemsToReveal == null) return;
        for (int i = 0; i < itemsToReveal.Length; i++)
        {
            if (itemsToReveal[i] != null)
                itemsToReveal[i].SetActive(true);
        }
    }
}
}
