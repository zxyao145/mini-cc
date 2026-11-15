import { vi } from 'vitest';

Object.defineProperty(window, 'matchMedia', {
  configurable: true,
  writable: true,
  value: (query: string) => {
    const media = new EventTarget();
    Object.defineProperty(media, 'matches', { get: () => {
      const width = query.match(/\((min|max)-width:\s*(\d+)px\)/);
      if (!width) return false;
      return width[1] === 'min' ? window.innerWidth >= Number(width[2]) : window.innerWidth <= Number(width[2]);
    } });
    return Object.assign(media, {
      media: query,
      onchange: null,
      addListener: media.addEventListener.bind(media, 'change'),
      removeListener: media.removeEventListener.bind(media, 'change'),
    });
  },
});

if (typeof ResizeObserver === 'undefined') {
  vi.stubGlobal('ResizeObserver', class {
    observe() {}
    unobserve() {}
    disconnect() {}
  });
}

HTMLElement.prototype.hasPointerCapture ??= () => false;
HTMLElement.prototype.releasePointerCapture ??= () => {};
HTMLElement.prototype.scrollIntoView ??= () => {};

// Happy DOM cannot complete or cancel native animations reliably. Let Motion use its JS driver.
Reflect.deleteProperty(Element.prototype, 'animate');
