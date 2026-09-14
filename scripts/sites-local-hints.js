(() => {
  "use strict";
  const message = "This public edition includes local tutorial hints only. AI chat, vision, and agent plans are unavailable; no API key is needed.";
  const unavailable = async () => { throw new Error(message); };
  window.astraBotAPI = Object.freeze({
    config: unavailable,
    request: unavailable,
    connect: unavailable,
    forget() {},
    cancelConnection() {},
    hasTabKey: () => false
  });
})();
