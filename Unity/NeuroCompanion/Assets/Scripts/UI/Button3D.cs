// Button3D.cs
// NeuroCompanion — 3D Tactile Push-Button Component for 2D/3D UI
// Provides authentic tactile 3D physical press depth, bevel shading, and spring-back animation.

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeuroCompanion.Backend
{
    [RequireComponent(typeof(Button))]
    public class Button3D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("3D Tactile Physics")]
        [Tooltip("Child transform that gets pressed down. If null, uses this transform or creates offset.")]
        [SerializeField] private RectTransform _targetTransform;

        [Tooltip("Physical 3D sink depth in pixels when pressed.")]
        [SerializeField] private float _pressDepth = 4.5f;

        [Tooltip("Smooth return spring speed.")]
        [SerializeField] private float _springSpeed = 22f;

        [Header("Lighting & Visual Highlights")]
        [SerializeField] private Image _buttonImage;
        [SerializeField] private Color _hoverTint = new Color(1.08f, 1.08f, 1.08f, 1f);
        [SerializeField] private Color _pressedTint = new Color(0.88f, 0.88f, 0.88f, 1f);

        private Vector2 _originalAnchoredPosition;
        private Color _originalColor = Color.white;
        private Coroutine _moveCoroutine;
        private bool _isPressed = false;

        private void Awake()
        {
            if (_targetTransform == null)
            {
                // Find a child named 'Content' or 'T', or use self
                var content = transform.Find("Content");
                if (content != null) _targetTransform = content.GetComponent<RectTransform>();
                else
                {
                    var txt = transform.Find("Txt") ?? transform.Find("Text");
                    if (txt != null) _targetTransform = txt.GetComponent<RectTransform>();
                    else _targetTransform = GetComponent<RectTransform>();
                }
            }

            if (_targetTransform != null)
                _originalAnchoredPosition = _targetTransform.anchoredPosition;

            if (_buttonImage == null)
                _buttonImage = GetComponent<Image>();

            if (_buttonImage != null)
                _originalColor = _buttonImage.color;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            var btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            _isPressed = true;
            MoveTo(_originalAnchoredPosition + new Vector2(0f, -_pressDepth));

            if (_buttonImage != null)
                _buttonImage.color = _originalColor * _pressedTint;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            MoveTo(_originalAnchoredPosition);

            if (_buttonImage != null)
                _buttonImage.color = _originalColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            if (!_isPressed && _buttonImage != null)
                _buttonImage.color = _originalColor * _hoverTint;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isPressed)
            {
                MoveTo(_originalAnchoredPosition);
                if (_buttonImage != null)
                    _buttonImage.color = _originalColor;
            }
        }

        private void MoveTo(Vector2 targetPos)
        {
            if (_targetTransform == null) return;
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            _moveCoroutine = StartCoroutine(AnimateMove(targetPos));
        }

        private IEnumerator AnimateMove(Vector2 targetPos)
        {
            while (Vector2.Distance(_targetTransform.anchoredPosition, targetPos) > 0.1f)
            {
                _targetTransform.anchoredPosition = Vector2.Lerp(
                    _targetTransform.anchoredPosition,
                    targetPos,
                    Time.unscaledDeltaTime * _springSpeed
                );
                yield return null;
            }
            _targetTransform.anchoredPosition = targetPos;
        }

        private void OnDisable()
        {
            if (_targetTransform != null)
                _targetTransform.anchoredPosition = _originalAnchoredPosition;
            if (_buttonImage != null)
                _buttonImage.color = _originalColor;
            _isPressed = false;
        }
    }
}
