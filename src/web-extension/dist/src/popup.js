var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
(function polyfill() {
  const relList = document.createElement("link").relList;
  if (relList && relList.supports && relList.supports("modulepreload")) {
    return;
  }
  for (const link of document.querySelectorAll('link[rel="modulepreload"]')) {
    processPreload(link);
  }
  new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      if (mutation.type !== "childList") {
        continue;
      }
      for (const node of mutation.addedNodes) {
        if (node.tagName === "LINK" && node.rel === "modulepreload")
          processPreload(node);
      }
    }
  }).observe(document, { childList: true, subtree: true });
  function getFetchOpts(link) {
    const fetchOpts = {};
    if (link.integrity) fetchOpts.integrity = link.integrity;
    if (link.referrerPolicy) fetchOpts.referrerPolicy = link.referrerPolicy;
    if (link.crossOrigin === "use-credentials")
      fetchOpts.credentials = "include";
    else if (link.crossOrigin === "anonymous") fetchOpts.credentials = "omit";
    else fetchOpts.credentials = "same-origin";
    return fetchOpts;
  }
  function processPreload(link) {
    if (link.ep)
      return;
    link.ep = true;
    const fetchOpts = getFetchOpts(link);
    fetch(link.href, fetchOpts);
  }
})();
class PopupManager {
  constructor() {
    __publicField(this, "statusEl");
    __publicField(this, "saveBtn");
    __publicField(this, "settingsBtn");
    __publicField(this, "settingsPanel");
    __publicField(this, "contentPreview");
    __publicField(this, "previewTitle");
    __publicField(this, "previewAuthor");
    __publicField(this, "previewUrl");
    __publicField(this, "apiUrlInput");
    __publicField(this, "saveSettingsBtn");
    __publicField(this, "apiUrl", "https://localhost:5001");
    this.statusEl = document.getElementById("status");
    this.saveBtn = document.getElementById("save-btn");
    this.settingsBtn = document.getElementById("settings-btn");
    this.settingsPanel = document.getElementById("settings-panel");
    this.contentPreview = document.getElementById("content-preview");
    this.previewTitle = document.getElementById("preview-title");
    this.previewAuthor = document.getElementById("preview-author");
    this.previewUrl = document.getElementById("preview-url");
    this.apiUrlInput = document.getElementById("api-url");
    this.saveSettingsBtn = document.getElementById("save-settings-btn");
    this.init();
  }
  async init() {
    await this.loadSettings();
    this.bindEvents();
    await this.extractContent();
  }
  async loadSettings() {
    const result = await chrome.storage.sync.get(["apiUrl"]);
    if (result.apiUrl) {
      this.apiUrl = result.apiUrl;
    }
    this.apiUrlInput.value = this.apiUrl;
  }
  bindEvents() {
    this.saveBtn.addEventListener("click", () => this.saveArticle());
    this.settingsBtn.addEventListener("click", () => this.toggleSettings());
    this.saveSettingsBtn.addEventListener("click", () => this.saveSettings());
  }
  toggleSettings() {
    this.settingsPanel.classList.toggle("hidden");
  }
  async saveSettings() {
    const newApiUrl = this.apiUrlInput.value.trim();
    if (newApiUrl) {
      this.apiUrl = newApiUrl;
      await chrome.storage.sync.set({ apiUrl: newApiUrl });
      this.showStatus("Settings saved", "success");
      this.settingsPanel.classList.add("hidden");
    }
  }
  showStatus(message, type = "loading") {
    this.statusEl.textContent = message;
    this.statusEl.className = `status ${type}`;
    this.statusEl.classList.remove("hidden");
  }
  hideStatus() {
    this.statusEl.classList.add("hidden");
  }
  async extractContent() {
    try {
      this.showStatus("Extracting content...", "loading");
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!tab.id) {
        throw new Error("No active tab found");
      }
      const response = await chrome.tabs.sendMessage(tab.id, { action: "extractContent" });
      if (response.success) {
        this.displayPreview(response.content);
        this.hideStatus();
        return response.content;
      } else {
        throw new Error(response.error || "Failed to extract content");
      }
    } catch (error) {
      this.showStatus(`Error: ${error instanceof Error ? error.message : "Unknown error"}`, "error");
      return null;
    }
  }
  displayPreview(content) {
    this.previewTitle.textContent = content.title;
    this.previewAuthor.textContent = content.author || "Unknown author";
    this.previewUrl.textContent = content.url;
    this.contentPreview.classList.remove("hidden");
    this.saveBtn.disabled = false;
  }
  async saveArticle() {
    try {
      const content = await this.extractContent();
      if (!content) {
        return;
      }
      this.showStatus("Saving article...", "loading");
      this.saveBtn.disabled = true;
      const payload = {
        url: content.url,
        title: content.title,
        originContent: content.content,
        author: content.author,
        contentType: "text/html"
      };
      const response = await fetch(`${this.apiUrl}/api/Articles/content`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
      });
      if (response.ok) {
        this.showStatus("Article saved successfully!", "success");
        setTimeout(() => {
          window.close();
        }, 2e3);
      } else {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }
    } catch (error) {
      this.showStatus(`Error saving article: ${error instanceof Error ? error.message : "Unknown error"}`, "error");
      this.saveBtn.disabled = false;
    }
  }
}
document.addEventListener("DOMContentLoaded", () => {
  new PopupManager();
});
