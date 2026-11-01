// SPDX-License-Identifier: AGPL-3.0-only
mergeInto(LibraryManager.library, {
  $uiCanvasObservers: {},

  ObserveUiCanvas__deps: ['$uiCanvasObservers'],
  ObserveUiCanvas: function (id, callback) {
    var canvas = Module.canvas;
    if (!canvas) return;
    var state = { frame: 0, disposed: false };
    uiCanvasObservers[id] = state;

    function publish() {
      state.frame = 0;
      if (state.disposed) return;
      var bounds = canvas.getBoundingClientRect();
      var sizes = [canvas.width, canvas.height, bounds.width, bounds.height];
      if (!sizes.every(function (v) { return Number.isFinite(v) && v > 0; })) return;
      if (state.last && sizes.every(function (v, i) { return v === state.last[i]; })) return;
      state.last = sizes;
      // Canvas attributes are the actual render resolution, including Unity's render scaling.
      {{{ makeDynCall('viffff', 'callback') }}}(id, sizes[0], sizes[1], sizes[2], sizes[3]);
    }

    function schedule() {
      if (state.disposed || state.frame) return;
      // Two one-shot frames let Unity's resize handler update the render target first.
      state.frame = requestAnimationFrame(function () {
        if (state.disposed) return;
        state.frame = requestAnimationFrame(publish);
      });
    }

    var resize = new ResizeObserver(schedule);
    resize.observe(canvas);
    var mutation = new MutationObserver(schedule);
    mutation.observe(canvas, { attributes: true, attributeFilter: ['width', 'height'] });
    window.addEventListener('resize', schedule);
    document.addEventListener('fullscreenchange', schedule);
    document.addEventListener('visibilitychange', schedule);
    if (window.visualViewport) window.visualViewport.addEventListener('resize', schedule);

    state.dispose = function () {
      state.disposed = true;
      cancelAnimationFrame(state.frame);
      resize.disconnect();
      mutation.disconnect();
      window.removeEventListener('resize', schedule);
      document.removeEventListener('fullscreenchange', schedule);
      document.removeEventListener('visibilitychange', schedule);
      if (window.visualViewport) window.visualViewport.removeEventListener('resize', schedule);
    };
    publish();
  },

  UnobserveUiCanvas__deps: ['$uiCanvasObservers'],
  UnobserveUiCanvas: function (id) {
    var state = uiCanvasObservers[id];
    if (!state) return;
    state.dispose();
    delete uiCanvasObservers[id];
  },

  GetUiCanvasCssSize: function (widthPointer, heightPointer) {
    var canvas = Module.canvas;
    if (!canvas) return 0;
    var bounds = canvas.getBoundingClientRect();
    if (!Number.isFinite(bounds.width) || !Number.isFinite(bounds.height)
        || bounds.width <= 0 || bounds.height <= 0) return 0;
    HEAPF32[widthPointer >> 2] = bounds.width;
    HEAPF32[heightPointer >> 2] = bounds.height;
    return 1;
  },
});
