using UnityEngine;

public class Rotator : MonoBehaviour
{
    [Header("Настройки вращения")]
    public float maxSpeed = 600f;
    public float acceleration = 80f;
    public float deceleration = 40f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("Звуковой эффект")]
    public AudioClip fanClip;   
    [Range(0f, 1f)]
    public float maxVolume = 0.7f;
    public float minPitch = 0.8f; 
    public float maxPitch = 1.1f;

    [Header("Настройки Crossfade")]
    public float crossfadeTime = 0.6f; 

    private AudioSource sourceA;
    private AudioSource sourceB;
    private float currentSpeed = 0f;
    private bool isSpinning = false;
    private float powerFactor = 0f;
    private float powerRampDuration = 3f;

    private float effectiveLength; // Обрезанная длина файла
    private float timer;
    private bool activeSourceA = true;

    void Start()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 2) { sourceA = sources[0]; sourceB = sources[1]; }
        else { sourceA = gameObject.AddComponent<AudioSource>(); sourceB = gameObject.AddComponent<AudioSource>(); }

        if (fanClip != null)
        {
            // КРИТИЧЕСКИЙ МОМЕНТ: Мы отрезаем последние 0.2 секунды файла, 
            // где обычно и прячется щелчок от нейросети.
            effectiveLength = fanClip.length - 0.2f; 
            
            SetupSource(sourceA);
            SetupSource(sourceB);
            sourceA.Play();
        }
        powerFactor = isSpinning ? 1f : 0f;
    }

    void SetupSource(AudioSource source)
    {
        source.clip = fanClip;
        source.loop = false; 
        source.spatialBlend = 1f; 
        source.volume = 0;
    }

    void Update()
    {
        // Логика вращения
        float targetPower = isSpinning ? 1f : 0f;
        powerFactor = Mathf.MoveTowards(powerFactor, targetPower, Time.deltaTime / powerRampDuration);
        float dynamicMaxSpeed = maxSpeed * powerFactor;
        currentSpeed = Mathf.MoveTowards(currentSpeed, dynamicMaxSpeed, (isSpinning ? acceleration : deceleration) * Time.deltaTime);
        transform.Rotate(rotationAxis * currentSpeed * Time.deltaTime);

        if (fanClip != null)
        {
            timer += Time.deltaTime;

            // Переключаем источники, используя "обрезанную" длину
            if (timer >= effectiveLength - crossfadeTime)
            {
                timer = 0;
                activeSourceA = !activeSourceA;
                if (activeSourceA) { sourceA.time = 0; sourceA.Play(); }
                else { sourceB.time = 0; sourceB.Play(); }
            }

            float speedRatio = currentSpeed / maxSpeed;
            float targetFullVolume = speedRatio * maxVolume;
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);

            // Плавный коэффициент перехода
            float t = Mathf.Clamp01(timer / crossfadeTime);
            
            if (activeSourceA)
            {
                // Source A плавно заменяет Source B
                sourceA.volume = Mathf.Sqrt(t) * targetFullVolume;
                sourceB.volume = Mathf.Sqrt(1.0f - t) * targetFullVolume;
                
                // Если переход закончен, полностью останавливаем B
                if (t >= 0.99f) { sourceB.Stop(); sourceB.volume = 0; }
            }
            else
            {
                // Source B плавно заменяет Source A
                sourceB.volume = Mathf.Sqrt(t) * targetFullVolume;
                sourceA.volume = Mathf.Sqrt(1.0f - t) * targetFullVolume;
                
                if (t >= 0.99f) { sourceA.Stop(); sourceA.volume = 0; }
            }

            sourceA.pitch = targetPitch;
            sourceB.pitch = targetPitch;
        }
    }

    public void ToggleRotation(bool state) { isSpinning = state; }
}