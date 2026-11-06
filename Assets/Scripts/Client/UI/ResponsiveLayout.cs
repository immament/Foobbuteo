// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using UnityEngine;
using UnityEngine.UIElements;
using Screen = UnityEngine.Device.Screen;

namespace Imma.Foobbuteo.Client.UI
{
    public class ResponsiveLayout : MonoBehaviour
    {
        private const float k_StackedLayoutAspectThreshold = 1.3f;
        private const float k_CompactHeaderAspectThreshold = 2f;

        private VisualElement m_RootElement;
        private VisualElement m_SafeAreaRoot;
        private VisualElement m_UnsafeAreaTop;
        private VisualElement m_UnsafeAreaRight;
        private VisualElement m_UnsafeAreaBottom;
        private VisualElement m_UnsafeAreaLeft;

        private VisualElement m_MainLayout;
        private VisualElement m_PitchArea;
        private float m_PitchAspectRatio;

        [SerializeField]
        private float WideThreshold = 2000f;

        [SerializeField]
        private int m_LastWidth;

        private int m_LastHeight;
        private Rect m_LastSafeArea;
        private Vector2 m_LastRootSize;
        private bool m_HasLayout;

        void OnEnable()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
            m_RootElement?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_PitchArea?.UnregisterCallback<GeometryChangedEvent>(OnPitchGeometryChanged);
            m_RootElement = m_MainLayout = m_SafeAreaRoot = m_PitchArea = null;
            m_UnsafeAreaTop = m_UnsafeAreaRight = m_UnsafeAreaBottom = m_UnsafeAreaLeft = null;
            m_HasLayout = false;
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (m_RootElement?.panel != null)
                OnGeometryChanged(null);
        }
#endif

        void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            // this.LogDebug($"+++ {rootElement.name}, {version}/{m_UiVersion}");
            m_RootElement?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_PitchArea?.UnregisterCallback<GeometryChangedEvent>(OnPitchGeometryChanged);
            m_RootElement = rootElement;
            m_SafeAreaRoot = m_RootElement.Q("SafeAreaRoot");
            m_UnsafeAreaTop = m_RootElement.Q("UnsafeAreaTop");
            m_UnsafeAreaRight = m_RootElement.Q("UnsafeAreaRight");
            m_UnsafeAreaBottom = m_RootElement.Q("UnsafeAreaBottom");
            m_UnsafeAreaLeft = m_RootElement.Q("UnsafeAreaLeft");
            m_MainLayout = m_RootElement.Q("MainLayout");
            m_PitchArea = m_MainLayout?.Q("PitchArea");
            m_HasLayout = false;

            m_RootElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_PitchArea?.RegisterCallback<GeometryChangedEvent>(OnPitchGeometryChanged);
            OnGeometryChanged(null);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (m_RootElement?.panel == null)
                return;
            Rect rootRect = m_RootElement.contentRect;
            Rect safeArea = Screen.safeArea;
            Vector2 rootSize = rootRect.size;

            if (
                m_HasLayout
                && Screen.width == m_LastWidth
                && Screen.height == m_LastHeight
                && safeArea == m_LastSafeArea
                && rootSize == m_LastRootSize
            )
                return;

            m_LastWidth = Screen.width;
            m_LastHeight = Screen.height;
            m_LastSafeArea = safeArea;
            m_LastRootSize = rootSize;
            m_HasLayout = true;

            if (
                rootRect.width <= 0f
                || rootRect.height <= 0f
                || float.IsNaN(rootRect.width)
                || float.IsNaN(rootRect.height)
                || float.IsInfinity(rootRect.width)
                || float.IsInfinity(rootRect.height)
                || Screen.width <= 0
                || Screen.height <= 0
                || safeArea.height <= 0f
            )
                return;

            float panelScaleX = rootRect.width / Screen.width;
            float panelScaleY = rootRect.height / Screen.height;
            float safeTop = (Screen.height - safeArea.yMax) * panelScaleY;
            float safeRight = (Screen.width - safeArea.xMax) * panelScaleX;
            float safeBottom = safeArea.yMin * panelScaleY;
            float safeLeft = safeArea.xMin * panelScaleX;
            float safeWidth = safeArea.width * panelScaleX;
            float safeAspect = safeArea.width / safeArea.height;
            ApplyLayout(m_MainLayout, safeWidth, safeArea.height * panelScaleY, safeAspect, WideThreshold);
            UpdatePitchLayout();

            if (m_SafeAreaRoot == null)
                return;

            m_SafeAreaRoot.style.left = safeLeft;
            m_SafeAreaRoot.style.top = safeTop;
            m_SafeAreaRoot.style.right = safeRight;
            m_SafeAreaRoot.style.bottom = safeBottom;
            foreach (string name in new[] { "modal-safe-area", "notifications-safe-area" })
            {
                var surface = m_RootElement.Q(name);
                if (surface == null)
                    continue;
                surface.style.left = safeLeft;
                surface.style.top = safeTop;
                surface.style.right = safeRight;
                surface.style.bottom = safeBottom;
            }

            if (m_UnsafeAreaTop != null)
                m_UnsafeAreaTop.style.height = safeTop;
            if (m_UnsafeAreaRight != null)
                m_UnsafeAreaRight.style.width = safeRight;
            if (m_UnsafeAreaBottom != null)
                m_UnsafeAreaBottom.style.height = safeBottom;
            if (m_UnsafeAreaLeft != null)
                m_UnsafeAreaLeft.style.width = safeLeft;
        }

        internal void SetPitchAspectRatio(float aspectRatio)
        {
            m_PitchAspectRatio = aspectRatio;
            UpdatePitchLayout();
        }

        private void OnPitchGeometryChanged(GeometryChangedEvent evt) => UpdatePitchLayout();

        private void UpdatePitchLayout()
        {
            if (m_RootElement?.panel == null)
                return;
            float aspectRatio = m_PitchAspectRatio;
            if (m_RootElement != null && Screen.width > 0 && Screen.height > 0)
            {
                var rootSize = m_RootElement.contentRect.size;
                if (rootSize.x > 0f && rootSize.y > 0f)
                    aspectRatio *= (rootSize.x / Screen.width) / (rootSize.y / Screen.height);
            }
            ApplyPitchLayout(m_MainLayout, aspectRatio);
        }

        internal static void ApplyPitchLayout(VisualElement mainLayout, float aspectRatio)
        {
            var pitchArea = mainLayout?.Q("PitchArea");
            var leftSide = mainLayout?.Q("LeftSide");
            if (pitchArea == null || leftSide == null)
                return;

            bool validAspect = aspectRatio > 0f && !float.IsNaN(aspectRatio) && !float.IsInfinity(aspectRatio);
            pitchArea.style.aspectRatio =
                mainLayout.ClassListContains("portrait-screen") && validAspect
                    ? new StyleRatio(aspectRatio)
                    : new StyleRatio(StyleKeyword.Null);
            bool fixedAspect = !mainLayout.ClassListContains("stacked-layout") && validAspect;
            if (!fixedAspect)
            {
                mainLayout.RemoveFromClassList("fixed-pitch-layout");
                leftSide.style.width = StyleKeyword.Null;
                return;
            }

            var available = pitchArea.contentRect.size;
            if (available.y <= 0f || float.IsNaN(available.y) || float.IsInfinity(available.y))
                return;

            float desiredWidth = available.y * aspectRatio;
            if (float.IsNaN(desiredWidth) || float.IsInfinity(desiredWidth))
                return;

            leftSide.style.width = desiredWidth;
            mainLayout.AddToClassList("fixed-pitch-layout");
        }

        internal static void ApplyLayout(
            VisualElement mainLayout,
            float safeWidth,
            float safeHeight,
            float safeAspect,
            float wideThreshold = 2000f
        )
        {
            bool stacked = safeAspect <= k_StackedLayoutAspectThreshold;
            mainLayout.EnableInClassList("stacked-layout", stacked);
            mainLayout.EnableInClassList("compact-landscape", safeWidth <= wideThreshold && !stacked);
            mainLayout.EnableInClassList("compact-header", safeAspect >= k_CompactHeaderAspectThreshold);
            mainLayout.EnableInClassList("portrait-screen", safeAspect < 1f);
            mainLayout.EnableInClassList("short-landscape", !stacked && safeHeight < 500f);
            // ImmLogger.LDebug<ResponsiveLayout>(
            //     new
            //     {
            //         safeWidth,
            //         safeHeight,
            //         safeAspect,
            //         wideThreshold,
            //         stacked,
            //         compactLandscape = safeWidth <= wideThreshold && !stacked,
            //         compactHeader = safeAspect >= k_CompactHeaderAspectThreshold,
            //         portraitScreen = safeAspect < 1f,
            //         shortLandscape = !stacked && safeHeight < 500f,
            //     }
            // );
            // PlaceMatchReport(mainLayout, stacked, safeHeight);
        }

        // public static void PlaceMatchReport(VisualElement mainLayout, bool stacked, float safeHeight)
        // {
        //     var report = mainLayout.Q("MessagePanel");
        //     var belowPitch = mainLayout.Q("MatchReportBelowPitch");
        //     var sidebar = mainLayout.Q("SidePanel");
        //     if (report == null || belowPitch == null || sidebar == null)
        //         return;
        //     var parent = stacked ? belowPitch : sidebar;
        //     if (report.parent != parent)
        //         parent.Add(report);
        //     belowPitch.style.height = stacked ? Mathf.Max(140f, safeHeight * 0.25f) : 0f;
        // }
    }
}
