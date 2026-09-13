mergeInto(LibraryManager.library, {
  AstraCoachPublish: function (jsonPointer) {
    if (window.astraCoach) window.astraCoach.receive(JSON.parse(UTF8ToString(jsonPointer)));
  },
  AstraCoachScreenshot: function (requestPointer, jpegPointer) {
    if (window.astraCoach) window.astraCoach.screenReady(UTF8ToString(requestPointer), UTF8ToString(jpegPointer));
  }
});
