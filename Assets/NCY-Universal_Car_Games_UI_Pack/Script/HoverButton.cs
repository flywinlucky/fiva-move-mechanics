using UnityEngine;
using UnityEngine.EventSystems;

namespace UCGUP
{
    public class HoverButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        Vector3 originalScale;

        [SerializeField]
        public Vector3 hoverScale = new Vector3(1.03f, 1.03f, 1.03f);

        [SerializeField]
        AudioSource _hoverSound;

        [SerializeField]
        AudioSource _click_Sound;

        void Awake()
        {
            originalScale = transform.localScale;
        }

        void OnDisable()
        {
            transform.localScale = originalScale;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        bool TryGetCursorIcon(out Texture2D cursorIcon)
        {
            cursorIcon = null;
            if (GameManager._instance == null)
                return false;

            cursorIcon = GameManager._instance._cursorIcon;
            return cursorIcon != null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = hoverScale;

            if (_hoverSound != null)
                _hoverSound.Play();

            if (TryGetCursorIcon(out Texture2D cursorIcon))
                Cursor.SetCursor(cursorIcon, Vector2.zero, CursorMode.Auto);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = originalScale;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_click_Sound != null)
                _click_Sound.Play();
        }
    }
}
