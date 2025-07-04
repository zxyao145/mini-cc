(function() {
  "use strict";
  chrome.runtime.onInstalled.addListener(() => {
    console.log("MiniCC Extension installed");
  });
  chrome.action.onClicked.addListener((tab) => {
    if (tab.id) {
      chrome.tabs.sendMessage(tab.id, { action: "extractContent" });
    }
  });
})();
