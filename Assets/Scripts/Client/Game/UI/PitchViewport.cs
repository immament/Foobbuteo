// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Client.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Screen = UnityEngine.Device.Screen;

namespace Imma.Foobbuteo.Client.Game.UI
{
    // [RequireComponent(typeof(PanelRenderer))]
    // Server builds strip PanelRenderer while processing the scene.
#if !UNITY_SERVER
    [RequireComponent(typeof(PanelRenderer))]
#endif
    public class PitchViewport : MonoBehaviour
    {
        // [SerializeField]
        // private UIDocument _document;
        private VisualElement m_RootElement;

        [SerializeField]
        private Camera _pitchCamera;

        internal Camera PitchCamera => _pitchCamera;
        internal event System.Action ViewportChanged;

        [SerializeField]
        private Renderer _pitchRenderer;

        private VisualElement _pitchArea;
        private VisualElement _pitchParent;
        private IVisualElementScheduledItem _viewportUpdate;

        [SerializeField]
        private Margins _pitchMargin = Margins.Zero;

        private int m_UiVersion = 0;

        void OnEnable()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
            UnregisterGeometryCallbacks();
            m_UiVersion = 0;
        }

        void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            if (version == m_UiVersion)
                return; // UI already up-to-date, no need to reload
            m_UiVersion = version;
            UnregisterGeometryCallbacks();
            m_RootElement = rootElement;

            _pitchArea = m_RootElement.Q("PitchContainer");
            _pitchParent = m_RootElement.Q("PitchArea");
            _pitchArea?.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _pitchParent?.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_RootElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            var bounds = _pitchRenderer.bounds;
            float width = bounds.size.x + _pitchMargin.Horizontal;
            float height = bounds.size.y + _pitchMargin.Vertical;
            GetComponent<ResponsiveLayout>()?.SetPitchAspectRatio(height > 0f ? width / height : 0f);
            ScheduleViewportUpdate();
        }

        private void UnregisterGeometryCallbacks()
        {
            _viewportUpdate?.Pause();
            _viewportUpdate = null;
            _pitchArea?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _pitchParent?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_RootElement?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ScheduleViewportUpdate();

        private void ScheduleViewportUpdate()
        {
            if (_pitchArea == null)
                return;
            _viewportUpdate?.Pause();
            _viewportUpdate = _pitchArea.schedule.Execute(() =>
            {
                if (UpdateViewport())
                    FitCameraToPitch();
            });
        }

        bool UpdateViewport()
        {
            var pitch = _pitchArea.worldBound;

            this.LogTrace(() =>
                $"_pitchArea.worldBound: {pitch}, xMax: {pitch.xMax},  yMax: {pitch.yMax}, xMin: {pitch.xMin},  xMin: {pitch.yMin}"
            );

            if (
                pitch.width <= 0f
                || pitch.height <= 0f
                || float.IsNaN(pitch.width)
                || float.IsNaN(pitch.height)
                || float.IsInfinity(pitch.width)
                || float.IsInfinity(pitch.height)
            )
            {
                return false;
            }

            var rootRect = m_RootElement.contentRect;
            if (
                rootRect.width <= 0f
                || rootRect.height <= 0f
                || float.IsNaN(rootRect.width)
                || float.IsNaN(rootRect.height)
                || float.IsInfinity(rootRect.width)
                || float.IsInfinity(rootRect.height)
                || Screen.width <= 0
                || Screen.height <= 0
            )
            {
                return false;
            }
            this.LogTrace(() => $"Screen: {Screen.width}x{Screen.height}, {Screen.currentResolution}");
            this.LogTrace(() => $"_rootVisualElement: {rootRect}");

            Vector2 ratio = new(Screen.width / rootRect.width, Screen.height / rootRect.height);
            this.LogTrace(() => $"ratio: {ratio}");

            var pixelRect = new Rect(
                pitch.x * ratio.x,
                Screen.height - (pitch.yMax * ratio.y),
                pitch.width * ratio.x,
                pitch.height * ratio.y
            );
            // var r2 = new Rect(0, 50, 800, 600);

            this.LogTrace(() => $"New pixelRect {pixelRect}");
            _pitchCamera.pixelRect = pixelRect;
            this.LogTrace(() => $"New _pitchCamera.pixelRect: {_pitchCamera.pixelRect}");
            return true;
        }

        void FitCameraToPitch()
        {
            // if (!_pitchCamera.isActiveAndEnabled)
            //     return;
            Bounds b = _pitchRenderer.bounds;
            // this.LogTrace(b);

            float width = b.size.x + _pitchMargin.Horizontal;
            float height = b.size.y + _pitchMargin.Vertical;

            // 1. fit whole pitch
            _pitchCamera.orthographicSize = Mathf.Max(height * 0.5f, width / (2f * _pitchCamera.aspect));

            float halfH = _pitchCamera.orthographicSize;
            float halfW = halfH * _pitchCamera.aspect;

            // 2. anchor TOP-LEFT of pitch
            _pitchCamera.transform.position = new Vector3(
                b.min.x - _pitchMargin.Left + halfW,
                b.max.y + _pitchMargin.Bottom - halfH,
                _pitchCamera.transform.position.z
            );
            ViewportChanged?.Invoke();
        }
    }
}
