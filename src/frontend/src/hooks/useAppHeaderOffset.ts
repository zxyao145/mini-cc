import { RefObject, useLayoutEffect } from 'react';
// Both sticky action bars measure the actual mobile application header.
export function useAppHeaderOffset(ref: RefObject<HTMLElement | null>, measureExtra?: (element: HTMLElement) => void) {
  useLayoutEffect(() => {
    const element = ref.current; if (!element) return;
    const header = document.querySelector<HTMLElement>('aside[aria-label="Sidebar"]');
    const measure = () => {
      element.style.setProperty('--app-header-offset', `${window.innerWidth <= 767 ? header?.getBoundingClientRect().height || 0 : 0}px`);
      measureExtra?.(element);
    };
    measure(); const observer = new ResizeObserver(measure);
    observer.observe(element); if (header) observer.observe(header);
    window.addEventListener('resize', measure);
    return () => { observer.disconnect(); window.removeEventListener('resize', measure); };
  }, [ref, measureExtra]);
}
