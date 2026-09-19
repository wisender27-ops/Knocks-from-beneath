using UnityEngine;

namespace KnocksFromBeneath
{

// Вешается на тот же GameObject, что и PlacementZone комнаты. Когда в зону ставят
// коробку, показывает заранее скрытые мелкие предметы этой комнаты (посуда и т.п.) —
// видимый прогресс "обживания" дома. Подписывается на PlacementZone.onBoxPlaced сама,
// чтобы в инспекторе нужно было расставить только сами предметы, а не событие.
[RequireComponent(typeof(PlacementZone))]
public class RoomRevealZone : MonoBehaviour
{
    [Tooltip("Предметы комнаты, изначально выключенные в сцене — появятся при установке коробки в эту зону.")]
    [SerializeField] private GameObject[] itemsToReveal;

    private bool _revealed;

    void Awake()
    {
        GetComponent<PlacementZone>().onBoxPlaced.AddListener(RevealItems);
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
