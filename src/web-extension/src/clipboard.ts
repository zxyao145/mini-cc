export async function copyText(text: string): Promise<void> {
  const clipboard = navigator.clipboard;
  if (clipboard) {
    try {
      await clipboard.writeText(text);
      return;
    } catch {
      // Try the compatibility path when the clipboard API is unavailable or denied.
    }
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', '');
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  let copied = false;
  try {
    textarea.select();
    copied = document.execCommand('copy');
  } catch {
    // execCommand can throw when copying is unavailable.
  } finally {
    textarea.remove();
  }
  if (!copied) throw new Error('Clipboard write was blocked.');
}
