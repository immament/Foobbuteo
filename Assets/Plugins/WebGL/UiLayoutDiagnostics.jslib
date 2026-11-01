// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

mergeInto(LibraryManager.library, {
  GetUiLayoutBrowserMetrics: function (panelWidth, panelHeight, screenMapped) {
    function number(value) {
      return typeof value === "number" && Number.isFinite(value) ? value : "unavailable";
    }
    function ratio(numerator, denominator, available) {
      return available && Number.isFinite(numerator) && Number.isFinite(denominator)
        && numerator > 0 && denominator > 0
        ? number(numerator / denominator)
        : "unavailable";
    }
    function mediaValue(feature, values) {
      if (typeof window.matchMedia !== "function") return "unavailable";
      return values.filter(function (value) {
        return window.matchMedia("(" + feature + ": " + value + ")").matches;
      });
    }

    var snapshot;
    try {
      var canvas = Module.canvas;
      var bounds = canvas ? canvas.getBoundingClientRect() : null;
      var viewport = window.visualViewport;
      var viewportMeta = document.querySelector('meta[name="viewport"]');
      snapshot = {
        devicePixelRatio: number(window.devicePixelRatio),
        windowInnerCssPixels: { width: number(window.innerWidth), height: number(window.innerHeight) },
        documentClientCssPixels: {
          width: number(document.documentElement.clientWidth),
          height: number(document.documentElement.clientHeight),
        },
        visualViewport: viewport ? {
          widthCssPixels: number(viewport.width), heightCssPixels: number(viewport.height),
          offsetLeftCssPixels: number(viewport.offsetLeft), offsetTopCssPixels: number(viewport.offsetTop),
          scale: number(viewport.scale),
        } : "unavailable",
        screenCssPixels: { width: number(window.screen.width), height: number(window.screen.height) },
        fullscreen: typeof document.fullscreenElement !== "undefined"
          ? document.fullscreenElement !== null : "unavailable",
        viewportMeta: viewportMeta ? viewportMeta.content : "unavailable",
        maxTouchPoints: number(window.navigator.maxTouchPoints),
        pointer: mediaValue("pointer", ["none", "coarse", "fine"]),
        anyPointer: mediaValue("any-pointer", ["none", "coarse", "fine"]),
        hover: mediaValue("hover", ["none", "hover"]),
        anyHover: mediaValue("any-hover", ["none", "hover"]),
        canvas: bounds ? {
          boundsCssPixels: {
            x: number(bounds.left), y: number(bounds.top), width: number(bounds.width), height: number(bounds.height),
          },
          clientCssPixels: { width: number(canvas.clientWidth), height: number(canvas.clientHeight) },
          bufferPixels: { width: number(canvas.width), height: number(canvas.height) },
        } : "unavailable",
        ratios: {
          canvasBufferPixelsPerCssPixelX: bounds ? ratio(canvas.width, bounds.width, true) : "unavailable",
          canvasBufferPixelsPerCssPixelY: bounds ? ratio(canvas.height, bounds.height, true) : "unavailable",
          cssPixelsPerPanelUnitX: bounds ? ratio(bounds.width, panelWidth, screenMapped) : "unavailable",
          cssPixelsPerPanelUnitY: bounds ? ratio(bounds.height, panelHeight, screenMapped) : "unavailable",
        },
      };
    } catch (error) {
      snapshot = { status: "unavailable", reason: "browser measurement failed", error: error.name };
    }
    var json = JSON.stringify(snapshot);
    var size = lengthBytesUTF8(json) + 1;
    var pointer = _malloc(size);
    stringToUTF8(json, pointer, size);
    return pointer;
  },
});
