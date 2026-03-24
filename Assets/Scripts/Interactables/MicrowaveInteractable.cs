using UnityEngine;
using System.Collections;

public class MicrowaveInteractable : MonoBehaviour
{
    [Header("Ссылки")]
    public Door microwaveDoor;
    public Light microwaveLight;
    public Transform pieInsidePoint;

    [Header("Нагрев")]
    public float heatDuration = 15f;
    public AudioSource microwaveAudioSource;
    public AudioClip heatStartSfx;
    public AudioClip heatLoopSfx;
    public AudioClip heatDoneSfx;

    private bool _isHeating = false;
    private bool _isReady = false;
    private PieQuestItem _pieInMicrowave;

    void Start()
    {
        if (microwaveLight != null)
            microwaveLight.enabled = false;
    }

    public void Interact()
    {
        var intro = FindObjectOfType<IntroSequence>();
        if (intro == null) return;

        var playerInteraction = FindObjectOfType<PlayerInteraction>();
        if (playerInteraction == null) return;

        // Если пирог уже готов: достать его можно только при открытой дверце и пустых руках.
        if (_isReady)
        {
            if (microwaveDoor == null || !microwaveDoor.isOpen) return;
            if (playerInteraction.GetHeldObject() != null) return;
            TryTakePieFromMicrowave(playerInteraction);
            return;
        }

        if (_isHeating) return;
        if (!intro.CanPlacePieInMicrowave()) return;
        if (microwaveDoor == null || !microwaveDoor.isOpen) return;
        if (pieInsidePoint == null) return;

        GameObject held = playerInteraction.ReleaseHeldObject();
        if (held == null) return;

        PieQuestItem pie = held.GetComponent<PieQuestItem>();
        if (pie == null) return;

        PutPieInsideMicrowave(held, pie);
        microwaveDoor.CloseDoor(); // принудительно закрываем дверцу после установки
        intro.OnPiePlacedInMicrowave();

        StartCoroutine(HeatPieRoutine());
    }

    void PutPieInsideMicrowave(GameObject pieObj, PieQuestItem pie)
    {
        _pieInMicrowave = pie;
        _isReady = false;

        pieObj.transform.SetParent(pieInsidePoint);
        pieObj.transform.position = pieInsidePoint.position;
        pieObj.transform.rotation = pieInsidePoint.rotation;

        Rigidbody rb = pieObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider[] colliders = pieObj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        pieObj.tag = "Untagged";
        pie.isHeated = false;
    }

    IEnumerator HeatPieRoutine()
    {
        _isHeating = true;
        if (microwaveDoor != null) microwaveDoor.SetInteractionLocked(true);
        if (microwaveLight != null) microwaveLight.enabled = true;

        if (microwaveAudioSource != null)
        {
            if (heatStartSfx != null) microwaveAudioSource.PlayOneShot(heatStartSfx);
            if (heatLoopSfx != null)
            {
                microwaveAudioSource.clip = heatLoopSfx;
                microwaveAudioSource.loop = true;
                microwaveAudioSource.Play();
            }
        }

        yield return new WaitForSeconds(heatDuration);

        if (microwaveAudioSource != null)
        {
            microwaveAudioSource.Stop();
            microwaveAudioSource.loop = false;
            if (heatDoneSfx != null) microwaveAudioSource.PlayOneShot(heatDoneSfx);
        }

        if (microwaveLight != null) microwaveLight.enabled = false;

        MakePieReadyToTake();
        _isHeating = false;
        _isReady = true;
        if (microwaveDoor != null) microwaveDoor.SetInteractionLocked(false);
    }

    void MakePieReadyToTake()
    {
        if (_pieInMicrowave == null) return;

        _pieInMicrowave.isHeated = true;
        // Пирог остаётся внутри микроволновки неподвижным и неинтерактивным,
        // пока игрок явно не откроет дверцу и не нажмёт E для извлечения.
    }

    void TryTakePieFromMicrowave(PlayerInteraction playerInteraction)
    {
        if (_pieInMicrowave == null) return;

        GameObject pieObj = _pieInMicrowave.gameObject;
        pieObj.transform.SetParent(null);

        Rigidbody rb = pieObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider[] colliders = pieObj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        pieObj.tag = "Pickable";

        if (!playerInteraction.TryGrabObjectFromScript(pieObj))
            return;

        _isReady = false;
        _pieInMicrowave = null;
    }

    public bool IsPieReadyToTake()
    {
        return _isReady && !_isHeating && _pieInMicrowave != null;
    }
}
