using UnityEngine;

namespace KnocksFromBeneath
{
// A-01: щелчок защёлки при открытии и стук при закрытии для любой Door (скрип движения остаётся в Door).
// Навешивается GameAudio на все двери сцены. Положение «закрыто» — поза двери при старте.
[DisallowMultipleComponent]
public sealed class DoorAudio : MonoBehaviour
{
    enum Kind { Room, Closet, Fridge, Microwave, Gate, Front }

    const float OpenThreshold = 4f, ClosedThreshold = 1.2f, SlideScale = 60f;

    Door door;
    AudioSource source;
    AudioClip woodDoorOpen;
    Kind kind;
    Quaternion closedRotation;
    Vector3 closedPosition;
    float openness, peakSpeed, lastOpenness;
    bool open, armed;
    int lastOpen = -1;

    public static void Attach(Door door)
    {
        if (door != null && door.GetComponent<DoorAudio>() == null) door.gameObject.AddComponent<DoorAudio>();
    }

    void Start()
    {
        door = GetComponent<Door>();
        closedRotation = transform.localRotation;
        closedPosition = transform.localPosition;
        string n = (name + " " + (transform.parent != null ? transform.parent.name : "")).ToLowerInvariant();
        kind = n.Contains("fridge") || n.Contains("refrigerator") ? Kind.Fridge
            : n.Contains("microwave") ? Kind.Microwave
            : n.Contains("gate") ? Kind.Gate
            : n.Contains("closet") || n.Contains("cabinet") || n.Contains("wardrobe") ? Kind.Closet
            : name == "Door" ? Kind.Front : Kind.Room;
        source = AudioRouting.Create(transform, "Door Latch Audio", AudioBus.World, 1f, kind == Kind.Front || kind == Kind.Gate ? 18f : 12f);
        if (kind == Kind.Room || kind == Kind.Front)
            woodDoorOpen = Resources.Load<AudioClip>("Audio/WoodDoorOpenJTap");
        source.minDistance = 1.2f;
        // Дверь, открытая при старте, считается открытой, а не «закрытой в этой позе».
        open = door != null && door.isOpen;
        armed = true;
    }

    void Update()
    {
        if (door == null || source == null || door.IsDetached) return;
        float angle = Quaternion.Angle(transform.localRotation, closedRotation);
        float slide = Vector3.Distance(transform.localPosition, closedPosition) * SlideScale;
        openness = Mathf.Max(angle, slide);
        float speed = Mathf.Abs(openness - lastOpenness) / Mathf.Max(Time.deltaTime, .0001f);
        lastOpenness = openness;
        if (door.IsRattling || Time.timeScale <= 0f) return;
        if (!open)
        {
            if (openness > OpenThreshold) { open = true; peakSpeed = 0f; if (armed && !door.IsCreakSuppressed) PlayOpen(); }
            return;
        }
        peakSpeed = Mathf.Max(peakSpeed * .9f, speed);
        if (openness < ClosedThreshold)
        {
            open = false;
            if (!door.IsCreakSuppressed) PlayClose(peakSpeed);
        }
    }

    void PlayOpen()
    {
        var lib = GameSounds.Library;
        if (lib == null) return;
        switch (kind)
        {
            case Kind.Fridge: Play(GameSounds.Pick(lib.doorOpen), .35f, 1.35f); break;
            case Kind.Microwave: Play(lib.doorLatch, .45f, 1.5f); break;
            case Kind.Gate: Play(lib.doorLatch, .7f, .8f); break;
            // Запись открытия двери с ручкой (автор: металлический щелчок звучал плохо).
            case Kind.Closet: Play(GameSounds.Pick(lib.doorOpen, ref lastOpen), .4f, 1.15f); break;
            default: Play(woodDoorOpen, .35f, 1f); break;
        }
    }

    void PlayClose(float speed)
    {
        var lib = GameSounds.Library;
        if (lib == null) return;
        // Медленно прикрытая дверь щёлкает тихо, захлопнутая — бьёт.
        float force = Mathf.InverseLerp(15f, 160f, speed);
        switch (kind)
        {
            case Kind.Fridge: Play(GameSounds.Pick(lib.doorClose), Mathf.Lerp(.2f, .4f, force), 1.45f); break;
            case Kind.Microwave: Play(lib.doorLatch, .5f, 1.35f); break;
            case Kind.Gate: Play(lib.doorLatch, Mathf.Lerp(.5f, .9f, force), .7f); break;
            case Kind.Closet: Play(GameSounds.Pick(lib.doorClose), Mathf.Lerp(.15f, .45f, force), 1.2f); break;
            default:
                Play(GameSounds.Pick(lib.doorClose), Mathf.Lerp(.25f, .85f, force), Random.Range(.95f, 1.03f));
                if (force < .35f) Play(lib.doorLatch, .4f, 1f);
                break;
        }
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }
}
}
