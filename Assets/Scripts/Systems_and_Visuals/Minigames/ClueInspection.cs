using UnityEngine;

namespace KnocksFromBeneath
{
    public enum ClueView { Hidden, Glimpse, Reading, Found }

    // Логика «Осмотра» (M5): скрытая деталь (надпись, царапины) лежит на одной из граней предмета.
    // Её видно, когда грань повёрнута к камере; прочитать — только приблизив. Удержать взгляд на
    // прочитанной детали HoldSeconds — улика найдена. Отвернул — прогресс тает, но не сразу.
    public sealed class ClueInspection
    {
        public const float FacingDot = 0.74f;    // ~42° от прямого взгляда — грань уже видна
        public const float ReadZoom = 1.2f;      // столько приближения нужно, чтобы разобрать
        public const float HoldSeconds = 0.7f;
        public const float DecayPerSecond = 0.5f;

        public float Progress { get; private set; }
        public bool Found { get; private set; }

        public ClueInspection(bool alreadyFound = false)
        {
            Found = alreadyFound;
            Progress = alreadyFound ? 1f : 0f;
        }

        // detailNormal — нормаль грани с деталью, toViewer — направление от детали к камере (оба в мире).
        public static float Facing(Vector3 detailNormal, Vector3 toViewer) =>
            Vector3.Dot(detailNormal.normalized, toViewer.normalized);

        public ClueView Classify(float facing, float zoom)
        {
            if (Found) return ClueView.Found;
            if (facing < FacingDot) return ClueView.Hidden;
            return zoom >= ReadZoom ? ClueView.Reading : ClueView.Glimpse;
        }

        // Возвращает true ровно в кадре, когда улика найдена.
        public bool Step(float facing, float zoom, float deltaTime)
        {
            if (Found) return false;
            if (Classify(facing, zoom) == ClueView.Reading)
                Progress = Mathf.Min(1f, Progress + deltaTime / HoldSeconds);
            else
                Progress = Mathf.Max(0f, Progress - deltaTime * DecayPerSecond);
            if (Progress < 1f) return false;
            Found = true;
            return true;
        }
    }

    // Кручение предмета в «Осмотре»: как тянуть его рукой. Влево-вправо — поворот вокруг своей
    // вертикали, вверх-вниз — наклон к себе/от себя вокруг горизонтали экрана, не дальше MaxPitch.
    // Крена нет: верх предмета всегда остаётся в вертикальной плоскости экрана. Поворот догоняет
    // ввод плавно. Камера превью смотрит вдоль +Z, вправо по экрану — +X, вверх — +Y.
    public sealed class InspectOrbit
    {
        public const float MaxPitch = 80f;
        public const float Sharpness = 16f;   // чем больше, тем быстрее догоняет ввод

        public float TargetYaw { get; private set; }
        public float TargetPitch { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        // dragX/dragY — куда тянут (вправо/вверх положительно), в градусах.
        public void Drag(float dragX, float dragY)
        {
            // Поворот вокруг +Y на положительный угол уводит переднюю грань влево, поэтому знак обратный.
            TargetYaw -= dragX;
            TargetPitch = Mathf.Clamp(TargetPitch + dragY, -MaxPitch, MaxPitch);
            if (TargetYaw > 360f || TargetYaw < -360f)
            {
                float wrap = Mathf.Sign(TargetYaw) * 360f;
                TargetYaw -= wrap;
                Yaw -= wrap;
            }
        }

        public void Step(float deltaTime)
        {
            float k = 1f - Mathf.Exp(-Sharpness * Mathf.Max(0f, deltaTime));
            Yaw = Mathf.Lerp(Yaw, TargetYaw, k);
            Pitch = Mathf.Lerp(Pitch, TargetPitch, k);
        }

        // Плавно вернуть в исходный вид коротким путём, без лишних оборотов.
        public void Reset()
        {
            Yaw = Mathf.DeltaAngle(0f, Yaw);
            TargetYaw = 0f;
            TargetPitch = 0f;
        }

        public void Snap()
        {
            Yaw = TargetYaw;
            Pitch = TargetPitch;
        }

        public Quaternion Rotation => Quaternion.AngleAxis(Pitch, Vector3.right) * Quaternion.AngleAxis(Yaw, Vector3.up);
    }
}
