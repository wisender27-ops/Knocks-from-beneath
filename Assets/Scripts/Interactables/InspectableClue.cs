using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Улика «Осмотра» (M-05) на предмете обстановки: E — «Осмотреть», предмет открывается в
    // ClueInspectWindow, скрытая деталь лежит на одной из его граней. Модель для превью берётся из
    // сериализованных меша и материалов: у статичных объектов в игре MeshFilter уже указывает на
    // общий меш статического батчинга.
    [DisallowMultipleComponent]
    public sealed class InspectableClue : MonoBehaviour
    {
        [Tooltip("Id улики в StoryFlags — не менять после выпуска, по нему считаются улики в сохранениях.")]
        [SerializeField] private string clueId;
        [SerializeField] private string displayName;
        [Tooltip("Доступна только после первой ночи (появилась/сдвинулась ночью).")]
        [SerializeField] private bool afterFirstNight;

        [Header("Превью")]
        [SerializeField] private Mesh previewMesh;
        [SerializeField] private Material[] previewMaterials;
        [SerializeField] private Vector3 previewScale = Vector3.one;
        [Tooltip("Поворот меша, как он стоит в доме (меши из Blender лежат на боку).")]
        [SerializeField] private Quaternion previewRotation = Quaternion.identity;

        [Header("Скрытая деталь (координаты меша)")]
        [SerializeField] private Vector3 detailPosition;
        [SerializeField] private Vector3 detailNormal = Vector3.down;
        [SerializeField] private Vector3 detailUp = Vector3.forward;
        [Tooltip("Размер детали в метрах предмета: ширина и высота.")]
        [SerializeField] private Vector2 detailSize = new Vector2(0.12f, 0.05f);
        [Tooltip("Бумажная наклейка под текстом. Без неё — царапины прямо по предмету.")]
        [SerializeField] private bool paperLabel = true;
        [TextArea] [SerializeField] private string detailText;
        [Tooltip("Сдвиг бумажки вдоль её «верха», м: край выходит за грань и торчит из-под предмета — зацепка для глаза.")]
        [SerializeField] private float detailPeek;
        [Tooltip("Насколько предмет утоплен в поверхность, м: бумажка из-под него ложится на саму поверхность.")]
        [SerializeField] private float surfaceSink;
        [Tooltip("Предмет, под которым лежит деталь (если улика — не сам этот объект). Деталь лежит на месте, даже пока его нет в доме.")]
        [SerializeField] private GameObject anchor;

        [Header("Зацепка на видной грани (необязательно)")]
        [Tooltip("Царапины и т.п. на грани, которую видно с места — намёк, что предмет стоит осмотреть.")]
        [SerializeField] private string tellText;
        [SerializeField] private Vector3 tellPosition;
        [SerializeField] private Vector3 tellNormal = Vector3.up;
        [SerializeField] private Vector3 tellUp = Vector3.forward;
        [SerializeField] private Vector2 tellSize = new Vector2(0.08f, 0.03f);

        [Header("Текст")]
        [Tooltip("Что герой разобрал — появляется в окне, когда деталь найдена.")]
        [TextArea] [SerializeField] private string foundCaption;
        [Tooltip("Мысли героя после закрытия окна, только в первый раз.")]
        [SerializeField] private string[] thoughtLines;

        private static IntroSequence intro;
        private static readonly List<InspectableClue> anchoredClues = new List<InspectableClue>();
        private Transform worldDetails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            intro = null;
            anchoredClues.Clear();
        }

        // Улика на чужом предмете, в который смотрит игрок: у неё нет своего коллайдера, E по предмету.
        public static InspectableClue OnAnchor(Transform target)
        {
            for (int i = 0; i < anchoredClues.Count; i++)
            {
                InspectableClue clue = anchoredClues[i];
                if (clue != null && target.IsChildOf(clue.anchor.transform)) return clue;
            }
            return null;
        }

        // Регистрация в Awake/OnDestroy, а не OnEnable: после показа детали компонент выключается.
        private void Awake()
        {
            if (anchor != null) anchoredClues.Add(this);
        }

        public string ClueId => clueId;
        public string DisplayName => displayName;
        public Mesh PreviewMesh => previewMesh;
        public Material[] PreviewMaterials => previewMaterials;
        public Vector3 PreviewScale => previewScale;
        public Quaternion PreviewRotation => previewRotation;
        public Vector3 DetailPosition => detailPosition;
        public Vector3 DetailNormal => detailNormal;
        public Vector3 DetailUp => detailUp;
        public Vector2 DetailSize => detailSize;
        public bool PaperLabel => paperLabel;
        public string DetailText => detailText;
        public float DetailPeek => detailPeek;
        public float SurfaceSink => surfaceSink;
        public bool HasTell => !string.IsNullOrEmpty(tellText);
        public string TellText => tellText;
        public Vector3 TellPosition => tellPosition;
        public Vector3 TellNormal => tellNormal;
        public Vector3 TellUp => tellUp;
        public Vector2 TellSize => tellSize;
        public bool DetailsShown => worldDetails != null;
        public string FoundCaption => foundCaption;
        public string[] ThoughtLines => thoughtLines;
        public bool IsFound => StoryFlags.HasClue(clueId);

        public bool CanInteract => previewMesh != null && !MinigameWindow.IsAnyOpen && !ClueInspectWindow.JustClosed && Available;

        private bool Available
        {
            get
            {
                if (!afterFirstNight) return true;
                if (intro == null) intro = FindFirstObjectByType<IntroSequence>();
                return intro != null && intro.FirstNightPassed;
            }
        }

        // Деталь и зацепка появляются на самом предмете, когда улика становится доступной. Дальше Update не нужен.
        private void Update()
        {
            if (worldDetails == null && previewMesh != null && Available) ShowWorldDetails();
            if (worldDetails != null) enabled = false;
        }

        private void ShowWorldDetails()
        {
            // В корне сцены, а не дочерним: у предметов отрицательный масштаб, текст отразился бы зеркально.
            Transform pose = anchor != null ? anchor.transform : transform;
            worldDetails = new GameObject(name + " Clue").transform;
            worldDetails.SetPositionAndRotation(pose.position, Quaternion.identity);
            ClueDetailBuilder.BuildDetail(worldDetails, this, pose.rotation, pose.lossyScale, true);
            ClueDetailBuilder.BuildTell(worldDetails, this, pose.rotation, pose.lossyScale, true);
        }

        private void OnDestroy()
        {
            anchoredClues.Remove(this);
            if (worldDetails != null) Destroy(worldDetails.gameObject);
        }

        public void Interact()
        {
            if (CanInteract) ClueInspectWindow.Show(this);
        }

        // Editor-настройка: связать с мешем объекта и положить деталь на грань его габарита.
        public void Configure(string id, string title, bool fromSecondDay, Vector3 faceNormal, Vector3 faceUp,
            Vector2 size, bool label, string text, string caption, string[] thoughts,
            float peek = 0f, string tell = null, Vector3 tellFace = default, Vector3 tellFaceUp = default, Vector2 tellArea = default)
        {
            clueId = id;
            displayName = title;
            afterFirstNight = fromSecondDay;
            detailNormal = faceNormal.normalized;
            detailUp = faceUp.normalized;
            detailSize = size;
            paperLabel = label;
            detailText = text;
            foundCaption = caption;
            thoughtLines = thoughts;
            detailPeek = peek;
            tellText = tell;
            if (tellFace != Vector3.zero) tellNormal = tellFace.normalized;
            if (tellFaceUp != Vector3.zero) tellUp = tellFaceUp.normalized;
            if (tellArea != Vector2.zero) tellSize = tellArea;
            CaptureMesh(anchor != null ? anchor : gameObject);
        }

        // Улика на чужом предмете (вызывать до Configure): поза и меш превью берутся у него,
        // у самой улики остаётся только коллайдер у торчащей детали.
        public void ConfigureAnchor(GameObject target, float sink)
        {
            anchor = target;
            surfaceSink = sink;
        }

        private void CaptureMesh(GameObject source)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = source.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null) return;
            previewMesh = filter.sharedMesh;
            previewMaterials = meshRenderer != null ? meshRenderer.sharedMaterials : new Material[0];
            previewScale = source.transform.lossyScale;
            // Без поворота вокруг вертикали: в превью важно, где верх, а не куда предмет смотрит в комнате.
            Vector3 euler = source.transform.rotation.eulerAngles;
            previewRotation = Quaternion.Euler(0f, -euler.y, 0f) * source.transform.rotation;
            Bounds bounds = previewMesh.bounds;
            Vector3 n = detailNormal;
            detailPosition = bounds.center + Vector3.Scale(bounds.extents, n);
            tellPosition = bounds.center + Vector3.Scale(bounds.extents, tellNormal);
        }
    }
}
