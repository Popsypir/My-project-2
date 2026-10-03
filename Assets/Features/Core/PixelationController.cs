using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Применяет GameSettings.Pixelation к материалу постэффекта пикселизации
    /// (тот, что назначен в Full Screen Pass Renderer Feature). 0 - эффект
    /// не заметен (размер "пикселя" = 1 экранный пиксель), 1 - максимум.
    /// Положи этот компонент один раз в каждую сцену (или на постоянный
    /// объект, как DayManager) и назначь материал.
    /// </summary>
    public class PixelationController : MonoBehaviour
    {
        [Tooltip("Материал, созданный из шейдера Hidden/Pixelate - тот же, что назначен в Renderer Feature")]
        [SerializeField] private Material _pixelateMaterial;
        [Tooltip("Размер \"пикселя\" (в экранных пикселях) при настройке = 1 (максимум)")]
        [SerializeField] private float _maxPixelSize = 16f;

        private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");

        private void OnEnable()
        {
            Apply();
            GameSettings.OnChanged += Apply;
        }

        private void OnDisable()
        {
            GameSettings.OnChanged -= Apply;
        }

        private void Apply()
        {
            if (_pixelateMaterial == null) return;

            float pixelSize = Mathf.Lerp(1f, _maxPixelSize, GameSettings.Pixelation);
            _pixelateMaterial.SetFloat(PixelSizeId, pixelSize);
        }
    }
}
