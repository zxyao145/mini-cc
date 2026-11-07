import './popup.scss';
import { DEFAULT_API_URL, type ExtractedContent, type SaveArticleResponse } from './types';

class PopupManager {
  private statusEl: HTMLElement;
  private saveBtn: HTMLButtonElement;
  private settingsBtn: HTMLButtonElement;
  private settingsPanel: HTMLElement;
  private contentPreview: HTMLElement;
  private previewTitle: HTMLElement;
  private previewAuthor: HTMLElement;
  private previewUrl: HTMLElement;
  private apiUrlInput: HTMLInputElement;
  private apiAkInput: HTMLInputElement;
  private saveSettingsBtn: HTMLButtonElement;

  private apiUrl: string = DEFAULT_API_URL;
  private extractedContent: ExtractedContent | null = null;
  private ak: string = '';

  constructor() {
    this.statusEl = document.getElementById('status')!;
    this.saveBtn = document.getElementById('save-btn') as HTMLButtonElement;
    this.settingsBtn = document.getElementById('settings-btn') as HTMLButtonElement;
    this.settingsPanel = document.getElementById('settings-panel')!;
    this.contentPreview = document.getElementById('content-preview')!;
    this.previewTitle = document.getElementById('preview-title')!;
    this.previewAuthor = document.getElementById('preview-author')!;
    this.previewUrl = document.getElementById('preview-url')!;
    this.apiUrlInput = document.getElementById('api-url') as HTMLInputElement;
    this.apiAkInput = document.getElementById('api-ak') as HTMLInputElement;
    this.saveSettingsBtn = document.getElementById('save-settings-btn') as HTMLButtonElement;

    this.init();
  }

  private async init() {
    await this.loadSettings();
    this.bindEvents();
    this.saveBtn.disabled = true;
    await this.extractContent();
  }

  private async loadSettings() {
    const result = await chrome.storage.sync.get(['apiUrl', 'ak']);
    if (result.apiUrl) {
      this.apiUrl = result.apiUrl;
    }
    if (result.ak) {
      this.ak = result.ak;
    }
    this.apiUrlInput.value = this.apiUrl;
    this.apiAkInput.value = this.ak;
  }

  private bindEvents() {
    this.saveBtn.addEventListener('click', () => this.saveArticle());
    this.settingsBtn.addEventListener('click', () => this.toggleSettings());
    this.saveSettingsBtn.addEventListener('click', () => this.saveSettings());
  }

  private toggleSettings() {
    this.settingsPanel.classList.toggle('hidden');
  }

  private async saveSettings() {
    const newApiUrl = this.apiUrlInput.value.trim();
    const newAk = this.apiAkInput.value.trim();
    
    if (newApiUrl && newAk) {
      this.apiUrl = newApiUrl;
      await chrome.storage.sync.set({ apiUrl: newApiUrl, ak: newAk });
      this.showStatus('Settings saved', 'success');
      this.settingsPanel.classList.add('hidden');
    }
  }

  private showStatus(message: string, type: 'success' | 'error' | 'loading' = 'loading') {
    this.statusEl.textContent = message;
    this.statusEl.className = `status ${type}`;
    this.statusEl.classList.remove('hidden');
  }

  private hideStatus() {
    this.statusEl.classList.add('hidden');
  }

  private async extractContent(): Promise<ExtractedContent | null> {
    try {
      this.showStatus('Extracting content...', 'loading');
      
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!tab.id) {
        throw new Error('No active tab found');
      }

      const response = await chrome.tabs.sendMessage(tab.id, { action: 'extractContent' });
      if (response.success) {
        this.extractedContent = response.content;
        this.displayPreview(response.content);
        this.hideStatus();
        return response.content;
      } else {
        throw new Error(response.error || 'Failed to extract content');
      }
    } catch (error) {
      this.showStatus(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`, 'error');
      return null;
    }
  }

  private displayPreview(content: ExtractedContent) {
    this.previewTitle.textContent = content.title;
    this.previewAuthor.textContent = content.author || 'Unknown author';
    this.previewUrl.textContent = content.url;
    this.contentPreview.classList.remove('hidden');
    this.saveBtn.disabled = false;
  }

  private async saveArticle() {
    try {
      const content = this.extractedContent ?? await this.extractContent();
      if (!content) {
        return;
      }

      this.showStatus('Saving article...', 'loading');
      this.saveBtn.disabled = true;

      const response: SaveArticleResponse = await chrome.runtime.sendMessage({
        action: 'saveArticle',
        content,
      });

      if (response.success) {
        this.showStatus('Article saved successfully!', 'success');
        setTimeout(() => {
          window.close();
        }, 2000);
      } else {
        throw new Error(response.error);
      }
    } catch (error) {
      this.showStatus(`Error saving article: ${error instanceof Error ? error.message : 'Unknown error'}`, 'error');
      this.saveBtn.disabled = false;
    }
  }
}

document.addEventListener('DOMContentLoaded', () => {
  new PopupManager();
});