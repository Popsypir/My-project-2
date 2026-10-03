using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePhone.Apps
{
    /// <summary>
    /// Красная нитка между двумя булавками - PinAnchor заметки или свободная
    /// булавка прямо на доске (см. FreePinMarker). Обычная растянутая и
    /// повёрнутая Image - каждый кадр сама подстраивается под текущее
    /// положение точек, поэтому не отваливается и не "рвётся", когда заметки
    /// двигают - просто тянется следом, как настоящая нитка. Если одна из
    /// заметок пропала (удалили) - удаляет сама себя. Клик по самой нитке
    /// в режиме Pin - режет её (см. NotesBoardApp.HandleStringClicked).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RedStringUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private float _thickness = 4f;

        private NotesBoardApp _board;
        private RectTransform _rect;
        private RectTransform _pointA;
        private RectTransform _pointB;
        private StickyNoteUI _noteA;
        private StickyNoteUI _noteB;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            // anchoredPosition отсчитывается от точки анкоринга, а не от pivot'а
            // родителя (Strings Container) - если они не совпадают, нитка рисуется
            // со сдвигом от точки, к которой привязана (родитель заякорен по
            // центру доски, а не в углу). Ставим анкор нитки ровно в pivot
            // родителя, чтобы localA/localB из LateUpdate совпадали с anchoredPosition.
            var parentRect = _rect.parent as RectTransform;
            _rect.anchorMin = _rect.anchorMax = parentRect != null ? parentRect.pivot : new Vector2(0.5f, 0.5f);

            // Pivot слева-по центру высоты - растягиваем вправо от точки A к точке B
            _rect.pivot = new Vector2(0f, 0.5f);
        }

        public void Connect(NotesBoardApp board, RectTransform pointA, RectTransform pointB, StickyNoteUI noteA, StickyNoteUI noteB)
        {
            _board = board;
            _pointA = pointA;
            _pointB = pointB;
            _noteA = noteA;
            _noteB = noteB;
        }

        public bool ConnectsTo(StickyNoteUI note) => _noteA == note || _noteB == note;

        // Нужны для сохранения доски (см. NotesBoardApp.SaveBoard)
        public RectTransform PointA => _pointA;
        public RectTransform PointB => _pointB;
        public StickyNoteUI NoteA => _noteA;
        public StickyNoteUI NoteB => _noteB;

        public void OnPointerClick(PointerEventData eventData)
        {
            _board?.HandleStringClicked(this);
        }

        // Свободные булавки (см. FreePinMarker) не нужны никому, кроме своей
        // нитки - когда нитка исчезает, они исчезают вместе с ней. PinAnchor
        // заметки и "хвост" за курсором маркера не имеют - их не трогаем.
        private void OnDestroy()
        {
            DestroyIfFreePin(_pointA);
            DestroyIfFreePin(_pointB);
        }

        private static void DestroyIfFreePin(RectTransform point)
        {
            if (point != null && point.GetComponent<FreePinMarker>() != null)
                Destroy(point.gameObject);
        }

        private void LateUpdate()
        {
            if (_pointA == null || _pointB == null || _rect.parent == null)
            {
                Destroy(gameObject);
                return;
            }

            var parent = (RectTransform)_rect.parent;

            Vector2 localA = parent.InverseTransformPoint(_pointA.position);
            Vector2 localB = parent.InverseTransformPoint(_pointB.position);

            Vector2 diff = localB - localA;
            float distance = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            _rect.anchoredPosition = localA;
            _rect.sizeDelta = new Vector2(distance, _thickness);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
