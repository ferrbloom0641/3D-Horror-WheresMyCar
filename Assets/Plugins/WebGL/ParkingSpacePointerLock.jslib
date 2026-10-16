mergeInto(LibraryManager.library, {
  ParkingInstallSpacePointerLock: function () {
    if (window.parkingSpacePointerLockHandler) return;

    window.parkingSpacePointerLockHandler = function (event) {
      if (event.code !== "Space") return;

      var canvas = Module["canvas"];
      if (!canvas) return;

      event.preventDefault();
      if (document.pointerLockElement !== canvas && canvas.requestPointerLock) {
        canvas.requestPointerLock();
      }
    };

    document.addEventListener("keydown", window.parkingSpacePointerLockHandler, true);
  },

  ParkingRemoveSpacePointerLock: function () {
    if (!window.parkingSpacePointerLockHandler) return;

    document.removeEventListener("keydown", window.parkingSpacePointerLockHandler, true);
    window.parkingSpacePointerLockHandler = null;
  }
});
