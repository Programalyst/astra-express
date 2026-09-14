(() => {
  "use strict";
  const button = document.createElement("button");
  button.id = "astrabot-settings-open";
  button.type = "button";
  button.textContent = "ⓘ";
  button.setAttribute("aria-label", "Local hints edition information");
  button.title = "Local hints only · no API key needed";
  button.addEventListener("click", () => window.alert("This public edition includes local tutorial hints only. Choose Start tutorial for guidance. AI chat, vision, and agent plans are unavailable; no API key is needed."));
  document.body.append(button);
  const style = document.createElement("style");
  style.textContent = "#coach-task-quick, #astrabot-commandbar, #bot-embodied { display: none !important; }";
  document.head.append(style);
  const notice = document.createElement("div");
  notice.textContent = "Local tutorial edition · AI unavailable";
  notice.style.cssText = "position:fixed;right:16px;top:76px;color:#c9dee5;font:12px sans-serif;pointer-events:none;z-index:5";
  document.body.append(notice);
})();
