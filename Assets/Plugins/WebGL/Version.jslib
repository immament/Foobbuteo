mergeInto(LibraryManager.library, {
  SetVersion: function (versionPtr) {
    const version = UTF8ToString(versionPtr);

    window.unityBuildVersion = version;

    console.log("jslib SetVersion - Unity build version:", version);
  },
});
