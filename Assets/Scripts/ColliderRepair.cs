using UnityEngine;

namespace KnocksFromBeneath
{
    /// <summary>
    /// Чинит геометрию коллайдеров импортированных пропов.
    ///
    /// ЗАЧЕМ: у моделей из FBX бывает сломан mesh.bounds — он почти пустой
    /// (у мусорного мешка Garbage_bags.001 mesh.bounds = 0.01 x 0.02 x 0.02 при
    /// настоящем размере вершин 0.013 x 0.016 x 0.042). MeshCollider строится
    /// именно по mesh.bounds, поэтому получается КОЛЛАЙДЕР НУЛЕВОГО РАЗМЕРА:
    /// физика его не видит, и предмет проваливается сквозь пол, хотя визуально
    /// выглядит нормально. Поднять точку спавна выше НЕ помогает — мешок всё
    /// равно не имеет физики, он просто чуть позже падает.
    ///
    /// Что делаем: заменяем битый коллайдер на BoxCollider по НАСТОЯЩЕМУ
    /// AABB вершин (mesh.vertices), а не по вручному mesh.bounds.
    /// </summary>
    public static class ColliderRepair
    {
        /// <summary>
        /// Возвращает настоящий AABB меша в локальных координатах, по самим вершинам.
        /// У этих FBX-мешей mesh.bounds врёт, поэтому доверять ему нельзя.
        /// </summary>
        public static bool TryGetTrueLocalBounds(Mesh mesh, out Vector3 min, out Vector3 max)
        {
            min = Vector3.zero;
            max = Vector3.zero;
            if (mesh == null || mesh.vertexCount == 0) return false;

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return false;

            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }
            return max != min;
        }

        /// <summary>
        /// Ставит на объект корректный BoxCollider по настоящему AABB вершин.
        /// Существующий сломанный MeshCollider удаляется.
        /// </summary>
        /// <param name="target">Объект с MeshFilter (обычно корень префаба).</param>
        /// <param name="bottomSkew">Насколько опустить нижнюю грань, в долях высоты.
        /// Небольшой запас (0.1) гарантирует, что предмет касается пола, даже если
        /// пол неровный или точка спавна считалась по другой поверхности.</param>
        /// <returns>true, если коллайнер удалось починить.</returns>
        public static bool FitBoxColliderToMesh(GameObject target, float bottomSkew = 0.1f)
        {
            if (target == null) return false;

            var filter = target.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;
            if (!TryGetTrueLocalBounds(filter.sharedMesh, out Vector3 min, out Vector3 max)) return false;

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            // Запас снизу: опускаем нижнюю грань, чтобы предмет гарантированно
            // перекрыл пол при посадке и не завис на нём краем.
            float skew = size.y * Mathf.Clamp01(bottomSkew);
            center.y -= skew * 0.5f;
            size.y += skew;

            // Удаляем сломанный MeshCollider (он всё равно нулевой).
            //
            // В ПЛЕСКЕ используем Destroy, а не DestroyImmediate: редактор ругается
            // на DestroyImmediate во время игры. В редакторе наоборот — нужен
            // DestroyImmediate, иначе компонент не удалится сразу. На ассете
            // (префаб) дополнительно нужен флаг allowDestroyingAssets.
            MeshCollider broken = target.GetComponent<MeshCollider>();
            if (broken != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(broken);
                }
                else
                {
                    Object.DestroyImmediate(broken);
                    if (target.GetComponent<MeshCollider>() != null)
                    {
                        Object.DestroyImmediate(broken, true);
                        if (target.GetComponent<MeshCollider>() != null)
                            Debug.LogWarning(
                                $"[ColliderRepair] Не удалось удалить сломанный MeshCollider на '{target.name}'.", target);
                    }
                }
            }

            BoxCollider box = target.GetComponent<BoxCollider>();
            if (box == null) box = target.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.isTrigger = false;

            return true;
        }
    }
}
