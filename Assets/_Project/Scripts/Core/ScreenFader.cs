using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace CampanhaRio.Core
{
    /// <summary>
    /// Full-screen fade (UI Toolkit): fade out, run an action while the screen is covered, fade back in.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScreenFader : MonoBehaviour
    {
        public Color color = new Color(0.97f, 0.96f, 0.92f);
        public float fadeOutTime = 0.6f;
        public float holdTime = 0.3f;
        public float fadeInTime = 0.6f;

        VisualElement overlay;

        public bool IsFading { get; private set; }

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            overlay = new VisualElement { name = "screen-fade", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
            overlay.style.backgroundColor = color;
            overlay.style.opacity = 0f;
            root.Add(overlay);
        }

        void OnDisable()
        {
            overlay?.RemoveFromHierarchy();
            IsFading = false;
        }

        /// <summary>Fades out, calls <paramref name="whileHidden"/>, fades in. Ignored if already fading.</summary>
        public void FadeThrough(Action whileHidden) => FadeThrough(whileHidden, 1f);

        /// <summary>The same with every duration scaled (the instant run restart uses ~0.3: under a second in all).</summary>
        public void FadeThrough(Action whileHidden, float timeScale)
        {
            if (IsFading) return;
            if (!Application.isPlaying || !isActiveAndEnabled || overlay == null) { whileHidden?.Invoke(); return; }
            StartCoroutine(FadeRoutine(whileHidden, Mathf.Max(0.05f, timeScale)));
        }

        IEnumerator FadeRoutine(Action whileHidden, float scale)
        {
            IsFading = true;
            yield return Fade(0f, 1f, fadeOutTime * scale);
            whileHidden?.Invoke();
            yield return new WaitForSecondsRealtime(holdTime * scale);
            yield return Fade(1f, 0f, fadeInTime * scale);
            IsFading = false;
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                overlay.style.opacity = Mathf.SmoothStep(from, to, t / duration);
                yield return null;
            }
            overlay.style.opacity = to;
        }
    }
}
