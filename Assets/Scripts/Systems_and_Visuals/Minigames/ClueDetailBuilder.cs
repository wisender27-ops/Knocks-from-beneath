using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Строит скрытую деталь улики (бумажка с текстом или царапины) и её «зацепку» на видной грани —
    // одним кодом для предмета в доме и для копии в окне «Осмотра», чтобы они совпадали.
    // Координаты детали — в пространстве меша; кадр задаёт, как меш стоит (поворот и масштаб).
    public static class ClueDetailBuilder
    {
        private const float SurfaceGap = 0.002f;
        private const float TuckDepth = 0.001f;
        private static readonly Color ScratchColor = new Color(0.82f, 0.78f, 0.7f);
        // Свежие царапины на тёмном пластике светлее самого предмета — их видно с шага.
        private static readonly Color WorldScratchColor = new Color(0.72f, 0.69f, 0.62f);

        private static Material previewPaper, worldPaper;

        public struct Part
        {
            public Transform Transform;
            public TextMeshPro Text;
        }

        // world: деталь в доме (освещённая бумага, бумажка на нижней грани подсунута под предмет —
        // наружу торчит только край); иначе — в превью, вся на виду.
        public static Part BuildDetail(Transform parent, InspectableClue clue, Quaternion rotation, Vector3 scale, bool world)
        {
            Pose(clue.DetailPosition, clue.DetailNormal, clue.DetailUp, rotation, scale, out Vector3 position, out Vector3 normal, out Vector3 up);
            bool under = clue.PaperLabel && normal.y < -0.5f;
            bool tuck = world && under;
            // Бумажка из-под предмета торчит только в доме; в превью она целиком на дне, в кадре.
            float peek = world || !under ? clue.DetailPeek : 0f;
            position += up * peek + normal * (tuck ? -(TuckDepth + clue.SurfaceSink) : SurfaceGap);
            return Build(parent, "ClueDetail", position, normal, up, clue.DetailSize, clue.DetailText, clue.PaperLabel, world);
        }

        public static Part BuildTell(Transform parent, InspectableClue clue, Quaternion rotation, Vector3 scale, bool world)
        {
            if (!clue.HasTell) return default;
            Pose(clue.TellPosition, clue.TellNormal, clue.TellUp, rotation, scale, out Vector3 position, out Vector3 normal, out Vector3 up);
            return Build(parent, "ClueTell", position + normal * SurfaceGap, normal, up, clue.TellSize, clue.TellText, false, world);
        }

        // Точка и направления меша → в кадр родителя. Нормаль преобразуется обратным масштабом.
        public static void Pose(Vector3 meshPoint, Vector3 meshNormal, Vector3 meshUp, Quaternion rotation, Vector3 scale,
            out Vector3 position, out Vector3 normal, out Vector3 up)
        {
            position = rotation * Vector3.Scale(meshPoint, scale);
            normal = (rotation * Vector3.Scale(meshNormal, new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z))).normalized;
            up = (rotation * Vector3.Scale(meshUp, scale)).normalized;
        }

        private static Part Build(Transform parent, string name, Vector3 position, Vector3 normal, Vector3 up, Vector2 size,
            string text, bool paper, bool world)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            // +Z смотрит внутрь предмета: лицевая сторона Quad и TMP видна снаружи.
            root.localRotation = Quaternion.LookRotation(-normal, up);

            if (paper)
            {
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(sheet.GetComponent<Collider>());
                sheet.name = "Paper";
                sheet.transform.SetParent(root, false);
                sheet.transform.localScale = new Vector3(size.x, size.y, 1f);
                var meshRenderer = sheet.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = PaperMaterial(world);
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Изнанка: Quad односторонний, а TMP рисуется с обеих сторон — без неё уголок,
                // торчащий из-за предмета, был бы невидим, а текст просвечивал бы зеркально.
                var back = Object.Instantiate(sheet, root);
                back.name = "PaperBack";
                back.transform.localPosition = new Vector3(0f, 0f, 0.0005f);
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(root, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
            var label = textObject.AddComponent<TextMeshPro>();
            label.font = UiStyle.Font(null);
            label.rectTransform.sizeDelta = size * 0.9f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.01f;
            label.fontSizeMax = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = paper ? UiStyle.PaperInk : world ? WorldScratchColor : ScratchColor;
            label.text = text;
            return new Part { Transform = root, Text = label };
        }

        private static Material PaperMaterial(bool world)
        {
            if (world)
            {
                if (worldPaper == null)
                {
                    worldPaper = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ClueLabelWorld" };
                    worldPaper.SetColor("_BaseColor", UiStyle.Paper);
                    worldPaper.SetFloat("_Smoothness", 0.1f);
                }
                return worldPaper;
            }
            if (previewPaper == null)
            {
                previewPaper = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ClueLabel" };
                previewPaper.SetColor("_BaseColor", UiStyle.Paper);
            }
            return previewPaper;
        }
    }
}
