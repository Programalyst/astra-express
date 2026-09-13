mergeInto(LibraryManager.library, {
  AstraCoachPublish: function (jsonPointer) {
    var state = JSON.parse(UTF8ToString(jsonPointer));
    if (window.astraCoach) window.astraCoach.receive(state);
    if (window.astraBotControl) window.astraBotControl.receive(state);
  },
  AstraCoachScreenshot: function (requestPointer, jpegPointer) {
    if (window.astraCoach) window.astraCoach.screenReady(UTF8ToString(requestPointer), UTF8ToString(jpegPointer));
    if (window.astraBotControl) window.astraBotControl.screenReady(UTF8ToString(requestPointer), UTF8ToString(jpegPointer));
  }
});
