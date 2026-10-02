using UnityEngine;

namespace KnocksFromBeneath
{
    /// <summary>
    /// ПОЛУПРОЗРАЧНЫЙ МАРКЕР МЕСТА ДЛЯ ПРЕДМЕТА МЕБЕЛИ.
    ///
    /// Что это: обычный дочерний объект сцены (как сейчас Slot_1), но с видимым
    /// полупрозрачным кубом. Место, куда предмет встанет, видно прямо в сцене и
    /// в геймплее — и его можно двигать мышкой (G) и вращать (R) прямо в
    /// Hierarchy/Scene view, без единого скрипта и без отдельного окна.
    ///
    /// КАК НАСТРАИВАТЬ (вручную, как ты и просил):
    ///   1. Найти в Hierarchy: DecorZone_* → FurnitureSlot_*.
    ///   2. Двигать G, вращать R, или вбить координаты в Inspector.
    ///   3. Размер куба = размер предмета (см. Size Mode), так что по габаритам
    ///      видно, влезет ли вещь в это место.
    ///   4. Кнопка "Слот на поверхность под маркером" (в компоненте) — если
    ///      захочешь опустить маркер на пол/стол одной кнопкой.
    ///
    /// Важное отличие от старого Slot_1: старый слот был пустым Transform, и
    /// предмет в него ставился по ПИВОТУ, из-за чего FBX-модели (пивот в центре
    /// меша, а не на дне) уезжали в пол или висели в воздухе. Теперь маркер
    /// держит предмет за нижнюю грань меша — вещь всегда стоит на поверхности.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Knocks From Beneath/Furniture Slot")]
    public class FurnitureSlot : MonoBehaviour
    {
        public enum SizeMode
        {
            /// <summary>Размер задаётся вручную в инспекторе (или подгоняется под размер предмета).</summary>
            Manual,

            /// <summary>Куб автоматически равен хитбоксу предмета, который в это место встанет.</summary>
            MatchItem
        }

        [Header("Что сюда встанет")]
        [Tooltip("Предмет, для которого это место. Маркер подстраивает размер куба под него и рисует рамку с его именем. Можно оставить пустым.")]
        [SerializeField] private GameObject itemForSlot;

        [Header("Вид")]
        [Tooltip("Как задавать размер маркера: вручную или автоматически по габаритам предмета.")]
        [SerializeField] private SizeMode sizeMode = SizeMode.MatchItem;

        [Tooltip("Размер маркера при Size Mode = Manual (X = ширина, Y = высота, Z = глубина), метры.")]
        [SerializeField] private Vector3 manualSize = new Vector3(0.6f, 0.6f, 0.6f);

        [Tooltip("Цвет полупрозрачного куба. У каждого места можно свой — так проще не перепутать.")]
        [SerializeField] private Color markerColor = new Color(0.35f, 0.85f, 1f, 0.22f);

        [Tooltip("Показывать маркер в геймплее. Выключи, если не хочешь видеть кубы в игре, но в сцене они останутся.")]
        [SerializeField] private bool visibleInGame = false;

        [Header("Поведение")]
        [Tooltip("Держать предмет за нижнюю грань его меша вместо пивота. Без этого FBX-модели с пивотом в центре уезжают в пол или висят в воздухе. Почти всегда нужно включено.")]
        [SerializeField] private bool sitOnBottomOfMesh = true;

        [Tooltip("Дополнительный зазор снизу, чтобы вещь не утопала в полу и не показывался z-fighting. Метры.")]
        [SerializeField] private float surfaceGap = 0.002f;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Material _material;
        private Bounds _itemBounds;
        private bool _hasItemBounds;

        // ---------------- Настройка ----------------

        /// <summary>Предмет, которому принадлежит это место. Может быть назначен из инспектора.</summary>
        public GameObject ItemForSlot
        {
            get => itemForSlot;
            set
            {
                itemForSlot = value;
                _hasItemBounds = false;
                ApplyVisuals();
            }
        }

        public PlacementZone OwningZone
        {
            get
            {
                // Зона — это ближайший предок с PlacementZone. Именно в неё
                // добавляется этот Transform, поэтому связь автоматическая.
                for (Transform t = transform.parent; t != null; t = t.parent)
                {
                    var zone = t.GetComponent<PlacementZone>();
                    if (zone != null) return zone;
                }
                return null;
            }
        }

        /// <summary>Размер предмета в мировых единицах (с учётом его масштаба).</summary>
        public Bounds ItemBounds
        {
            get
            {
                if (!_hasItemBounds || itemForSlot == null)
                {
                    Renderer r = itemForSlot != null ? itemForSlot.GetComponentInChildren<Renderer>() : null;
                    _itemBounds = r != null ? r.bounds : new Bounds();
                    _hasItemBounds = true;
                }
                return _itemBounds;
            }
        }

        public Vector3 MarkerSize
        {
            get
            {
                if (sizeMode == SizeMode.MatchItem && itemForSlot != null)
                {
                    Bounds b = ItemBounds;
                    // Слегка расширяем, чтобы предмет визуально «сидел» в кубе,
                    // а не томился внутри него с запасом.
                    return new Vector3(
                        Mathf.Max(0.05f, b.size.x + 0.06f),
                        Mathf.Max(0.05f, b.size.y + 0.06f),
                        Mathf.Max(0.05f, b.size.z + 0.06f));
                }
                return manualSize;
            }
        }

        // ---------------- Применение визуала ----------------

        void OnEnable() => ApplyVisuals();
        void OnValidate() => ApplyVisuals();
        void LateUpdate()
        {
            if (!Application.isPlaying) ApplyVisuals();
        }

        /// <summary>
        /// Создаёт/обновляет полупрозрачный куб. Идемпотентно: можно звать каждый
        /// кадр в редакторе (ExecuteAlways), лишних объектов не плодит.
        /// </summary>
        public void ApplyVisuals()
        {
            // Маркер — обычный объект сцены, а не готовый префаб, поэтому меш и
            // рендерер создаём сами при первом вызове (в инспекторе их нет).
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
            if (_filter == null || _renderer == null) return;

            if (_filter.sharedMesh == null)
            {
                // Встроенный куб: не нужен отдельный ассет, при этом меш не
                // копируется в сцену и не засоряет проект.
                _filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (_filter.sharedMesh == null) _filter.sharedMesh = BuildCubeMesh();
            }

            Vector3 size = MarkerSize;
            transform.localScale = new Vector3(
                size.x, size.y, size.z);

            if (_material == null)
            {
                _material = new Material(FindMarkerShader());
                _renderer.sharedMaterial = _material;
            }
            _material.color = markerColor;

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;
            // Маркер не должен ни блокировать, ни принимать лучи — это чисто визуал.
            _renderer.enabled = Application.isPlaying ? visibleInGame : true;
        }

        static Shader FindMarkerShader()
        {
            // Unlit/Color рисует куб ровным цветом без освещения и теней — в
            // редакторе и в игре выглядит одинаково, и ничто не зависит от
            // настроек освещения сцены.
            Shader s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static Mesh BuildCubeMesh()
        {
            // Запасной вариант, если встроенный куб недоступен.
            Mesh m = new Mesh { name = "FurnitureSlotCube" };
            Vector3 h = Vector3.one * 0.5f;
            m.vertices = new[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z),
            };
            m.triangles = new[]
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4,
                3,7,6, 3,6,2, 0,4,7, 0,7,3, 1,2,6, 1,6,5,
            };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------------- Куда и как встанет предмет ----------------

        /// <summary>
        /// Позиция, в которую зона поставит предмет.
        ///
        /// По умолчанию (sitOnBottomOfMesh) это положение, при котором нижняя
        /// грань МЕША предмета оказывается ровно на маркере, а не сам пивот: у
        /// FBX-моделей пивот часто в центре меша, и установка по пивоту уводила
        /// лампу в пол или подвешивала в воздухе. Теперь предмет встаёт на ту
        /// поверхность, возле которой стоит маркер, при любом повороте.
        /// </summary>
        public Vector3 GetItemPlacementPosition(Transform item, Quaternion placementRotation)
        {
            if (item == null || !sitOnBottomOfMesh) return transform.position;
            Bounds rotatedBounds = new Bounds();
            bool hasBounds = false;
            // Imported meshes can have a rotated pivot. Find the bottom of the
            // target orientation, rather than treating mesh.bounds.min as a pivot.
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 localPoint = item.InverseTransformPoint(filter.transform.TransformPoint(point));
                    point = placementRotation * Vector3.Scale(localPoint, item.lossyScale);
                    if (!hasBounds) { rotatedBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else rotatedBounds.Encapsulate(point);
                }
            }
            if (!hasBounds) return transform.position;
            Vector3 bottomCenter = new Vector3(rotatedBounds.center.x, rotatedBounds.min.y, rotatedBounds.center.z);
            return transform.position - bottomCenter + Vector3.up * surfaceGap;
        }

        /// <summary>
        /// Смещение от пивота предмета до нижней грани его меша, в локальных
        /// координатах предмета. Считаем через TransformPoint, поэтому результат
        /// не зависит от текущего поворота предмета в сцене.
        /// </summary>
        Vector3 GetLocalBottomOffset(Transform item)
        {
            Renderer r = item.GetComponentInChildren<Renderer>();
            if (r == null) return Vector3.zero;

            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return Vector3.zero;

            // Нижняя точка меша в мировых координатах, затем в локальные
            // координаты самого предмета (меш может быть вложен в дочерний узел).
            Vector3 worldBottom = mf.transform.TransformPoint(mf.sharedMesh.bounds.min);
            return item.transform.InverseTransformPoint(worldBottom);
        }

        /// <summary>
        /// Опускает маркер на ближайшую поверхность прямо под ним. Удобно, если
        /// поставил куб в воздухе — одной кнопкой он ляжет на пол/стол/полку.
        /// </summary>
        [ContextMenu("Слот на поверхность под маркером")]
        public void SnapToSurfaceBelow()
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers))
            {
                Debug.Log($"[FurnitureSlot] '{name}': под маркером нет поверхности.", this);
                return;
            }
            transform.position = new Vector3(
                transform.position.x, hit.point.y + surfaceGap, transform.position.z);
        }

        void OnDrawGizmosSelected()
        {
            // Рамка в габаритах предмета — видно, влезает ли вещь в это место.
            if (itemForSlot == null) return;
            Bounds b = ItemBounds;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
