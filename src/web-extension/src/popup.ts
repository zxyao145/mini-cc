import { copyText } from './clipboard';
import { assertHttpPageUrl } from './page-meta';
import { DEFAULT_API_URL, type ExtractedPage, type PageChoices, type SavePageResponse } from './types';

type PageKind = ExtractedPage['kind'];
const PAGE_KINDS: PageKind[] = ['article', 'link'];
const SAVE_LABEL = { article: 'Article', link: 'Link' };

class PopupManager {
  private statusEl: HTMLElement;
  private saveButtons: Record<PageKind, HTMLButtonElement>;
  private copyMarkdownBtn: HTMLButtonElement;
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
  private choices: PageChoices | null = null;
  private ak: string = '';

  constructor() {
    this.statusEl = document.getElementById('status')!;
    this.saveButtons = {
      article: document.getElementById('save-article-btn') as HTMLButtonElement,
      link: document.getElementById('save-link-btn') as HTMLButtonElement,
    };
    this.copyMarkdownBtn = document.getElementById('copy-md-btn') as HTMLButtonElement;
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
    this.updateSaveButtons();
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
    for (const kind of PAGE_KINDS) {
      this.saveButtons[kind].addEventListener('click', () => this.savePage(kind));
    }
    this.copyMarkdownBtn.addEventListener('click', () => this.copyMarkdown());
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

  private async getActiveTab() {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.id) throw new Error('No active tab found');
    return { id: tab.id, url: tab.url };
  }

  private async extractContent() {
    try {
      this.showStatus('Extracting content...', 'loading');
      
      const tab = await this.getActiveTab();
      assertHttpPageUrl(tab.url);

      const response = await chrome.tabs.sendMessage(tab.id, { action: 'extractContent' });
      if (response.success) {
        this.choices = response.content;
        this.displayPreview(response.content);
        this.hideStatus();
      } else {
        throw new Error(response.error || 'Failed to extract content');
      }
    } catch (error) {
      this.showStatus(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`, 'error');
    }
  }

  private displayPreview(choices: PageChoices) {
    const preview = choices.suggested === 'article' ? choices.article : choices.link;
    this.previewTitle.textContent = preview.title;
    this.previewAuthor.textContent = preview.author || 'Unknown author';
    this.previewUrl.textContent = preview.url;
    this.contentPreview.classList.remove('hidden');
    for (const kind of PAGE_KINDS) {
      this.saveButtons[kind].classList.toggle('btn-primary', kind === choices.suggested);
      this.saveButtons[kind].classList.toggle('btn-secondary', kind !== choices.suggested);
    }
    this.updateSaveButtons();
  }

  // A page without readable content can only be saved as a link.
  private updateSaveButtons() {
    this.saveButtons.article.disabled = !this.choices?.article;
    this.saveButtons.link.disabled = !this.choices;
  }

  private async savePage(kind: PageKind) {
    const content = this.choices?.[kind];
    if (!content) return;

    const label = SAVE_LABEL[kind];
    const name = label.toLowerCase();
    try {
      this.showStatus(`Saving ${name}...`, 'loading');
      for (const button of Object.values(this.saveButtons)) button.disabled = true;

      const response: SavePageResponse = await chrome.runtime.sendMessage({
        action: 'savePage',
        content,
      });
      if (!response.success) throw new Error(response.error);

      this.showStatus(`${label} saved successfully!`, 'success');
      setTimeout(() => {
        window.close();
      }, 2000);
    } catch (error) {
      this.showStatus(`Error saving ${name}: ${error instanceof Error ? error.message : 'Unknown error'}`, 'error');
      this.updateSaveButtons();
    }
  }

  private async copyMarkdown() {
    try {
      const tab = await this.getActiveTab();

      const response = await chrome.tabs.sendMessage(tab.id, { action: 'extractMarkdown' });
      if (!response.success) throw new Error(response.error || 'Failed to extract Markdown');
      await copyText(response.markdown);
      this.showStatus('Markdown copied', 'success');
    } catch (error) {
      this.showStatus(`Error copying Markdown: ${error instanceof Error ? error.message : 'Unknown error'}`, 'error');
    }
  }
}

document.addEventListener('DOMContentLoaded', () => {
  new PopupManager();
});