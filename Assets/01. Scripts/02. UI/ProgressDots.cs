using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PHD.UI
{
    /// <summary>
    /// 이번 라운드의 길이만큼 점을 찍고, 입력한 만큼 채운다.
    /// 점은 풀링해서 재사용한다(라운드마다 Instantiate/Destroy 하지 않는다).
    /// </summary>
    public class ProgressDots : MonoBehaviour
    {
        [SerializeField] float dotSize = 6f;
        [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] Color filledColor = new Color(0.49f, 0.55f, 1f, 1f);

        readonly List<Image> _dots = new List<Image>(32);

        /// <summary>점 개수를 맞추고 전부 비운 상태로 초기화한다.</summary>
        public void Setup(int count)
        {
            EnsureCapacity(count);

            for (int i = 0; i < _dots.Count; i++)
            {
                bool active = i < count;
                _dots[i].gameObject.SetActive(active);
                if (active) _dots[i].color = emptyColor;
            }
        }

        /// <summary>앞에서부터 <paramref name="filled"/> 개를 채운 상태로 표시한다.</summary>
        public void SetFilled(int filled)
        {
            for (int i = 0; i < _dots.Count; i++)
            {
                if (!_dots[i].gameObject.activeSelf) continue;
                _dots[i].color = i < filled ? filledColor : emptyColor;
            }
        }

        public void Clear() => Setup(0);

        void EnsureCapacity(int count)
        {
            while (_dots.Count < count)
            {
                var go = new GameObject("Dot", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(dotSize, dotSize);

                var image = go.AddComponent<Image>();
                image.color = emptyColor;
                image.raycastTarget = false;

                _dots.Add(image);
            }
        }
    }
}
