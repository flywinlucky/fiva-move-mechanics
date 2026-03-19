using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace UCGUP
{
    public class SetHeight : MonoBehaviour
    {
        RectTransform _rectTransform;
        bool _sizeTall;
        Vector3 _pos;

        void Start()
        {
            _rectTransform = transform.parent.GetComponent<RectTransform>();
            _pos = transform.localPosition;
            _sizeTall = false;
        }

        public void SetSize()
        {
            if (!_sizeTall)
            {
                // rectTransform.sizeDelta = new Vector2(450, 450);
                _sizeTall = true;
                _rectTransform.GetComponent<VerticalLayoutGroup>().enabled = true;
                _rectTransform.GetComponent<Image>().enabled = true;
                _rectTransform.GetChild(1).gameObject.SetActive(true);
                _rectTransform.GetChild(2).gameObject.SetActive(true);
            }

            else if (_sizeTall)
            {
                //rectTransform.sizeDelta = new Vector2(450, 150);
                _sizeTall = false;
                _rectTransform.GetComponent<VerticalLayoutGroup>().enabled = false;
                _rectTransform.GetComponent<Image>().enabled = false;
                _rectTransform.GetChild(1).gameObject.SetActive(false);
                _rectTransform.GetChild(2).gameObject.SetActive(false);
                transform.localPosition = _pos;
            }

        }
    }
}
