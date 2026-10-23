using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))] // Автоматически добавляет AudioSource на объект
public class CatJumpScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cat1;
    [SerializeField] private GameObject cat2;

    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.3f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip jumpSound;

    [SerializeField] private AudioClip[] MonSounds;
    // Звук прыжка/падения

    private AudioSource audioSource;
    private bool isAnimating = false; // Защита от спама анимации

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void CatJump(int index)
    {
        if (isAnimating) return; // Если анимация уже идет, игнорируем вызов

        GameObject targetCat = null;

        switch (index)
        {
            case 0:
                targetCat = cat1;
                break;
            case 1:
                targetCat = cat2;
                break;
            default:
                Debug.LogWarning("Неверный индекс кота!");
                return;
        }

        if (targetCat != null)
        {
            if (index == 0)
            {
                // 1. Проигрываем звук в самом начале
                if (jumpSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(jumpSound);
                }

                // 2. Запускаем анимацию
                StartCoroutine(AnimateComicalJump(targetCat));
            }
            else
            {
                if (audioSource.isPlaying) return;
                if (MonSounds == null || MonSounds.Length == 0) return;

                int randomIndex = Random.Range(0, MonSounds.Length);
                audioSource.clip = MonSounds[randomIndex];
                audioSource.volume = soundVolume;
                audioSource.Play();
            }
            
        }
        else
        {
            Debug.LogError($"Кот с индексом {index} не назначен в инспекторе!");
        }
    }

    private IEnumerator AnimateComicalJump(GameObject cat)
    {
        isAnimating = true;

        // Сохраняем начальные значения
        Vector3 startPos = cat.transform.position;
        Quaternion startRot = cat.transform.rotation;
        Vector3 startScale = cat.transform.localScale;

        // Параметры анимации (можете настроить под себя)
        float jumpHeight = 1.2f;
        float jumpForward = 0.5f;
        float fallBackDistance = 1.0f;

        float jumpDuration = 0.2f;  // Быстрый взлет
        float fallDuration = 0.3f;  // Падение на спину
        float pauseDuration = 0.4f; // Пауза в "обморочном" состоянии
        float resetDuration = 0.2f; // Быстрый возврат в норму

        // --- ФАЗА 1: Комичный прыжок вверх и вперед ---
        Vector3 jumpTarget = startPos + new Vector3(0, jumpHeight, jumpForward);
        Vector3 stretchScale = Vector3.Scale(startScale, new Vector3(0.8f, 1.3f, 0.8f));// Растягиваем по вертикали

        float timer = 0;
        while (timer < jumpDuration)
        {
            timer += Time.deltaTime;
            float t = timer / jumpDuration;

            cat.transform.position = Vector3.Lerp(startPos, jumpTarget, Mathf.SmoothStep(0, 1, t));
            cat.transform.localScale = Vector3.Lerp(startScale, stretchScale, t);
            yield return null;
        }
        cat.transform.position = jumpTarget;
        cat.transform.localScale = stretchScale;

        // --- ФАЗА 2: Падение назад (как подкошенный) ---
        Vector3 fallTarget = startPos + new Vector3(0, 0, -fallBackDistance);
        Quaternion fallRot = startRot * Quaternion.Euler(-90, 0, 0); // Поворот на 90 градусов назад
        Vector3 squashScale = Vector3.Scale(startScale, new Vector3(1.3f, 0.4f, 1.3f)); // Сплющиваем при ударе

        timer = 0;
        while (timer < fallDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fallDuration;

            // Ускорение при падении (Ease In)
            float fallT = t * t;

            cat.transform.position = Vector3.Lerp(jumpTarget, fallTarget, fallT);
            cat.transform.rotation = Quaternion.Lerp(startRot, fallRot, t);
            cat.transform.localScale = Vector3.Lerp(stretchScale, squashScale, t);
            yield return null;
        }
        cat.transform.position = fallTarget;
        cat.transform.rotation = fallRot;
        cat.transform.localScale = squashScale;

        // --- ПАУЗА: Лежит "оглушенный" ---
        yield return new WaitForSeconds(pauseDuration);

        // --- ФАЗА 3: Резкий возврат в исходное положение (как ни в чем не бывало) ---
        timer = 0;
        while (timer < resetDuration)
        {
            timer += Time.deltaTime;
            float t = timer / resetDuration;

            cat.transform.position = Vector3.Lerp(fallTarget, startPos, t);
            cat.transform.rotation = Quaternion.Lerp(fallRot, startRot, t);
            cat.transform.localScale = Vector3.Lerp(squashScale, startScale, t);
            yield return null;
        }

        // Гарантируем точные начальные значения в конце
        cat.transform.position = startPos;
        cat.transform.rotation = startRot;
        cat.transform.localScale = startScale;

        isAnimating = false;
    }
}
