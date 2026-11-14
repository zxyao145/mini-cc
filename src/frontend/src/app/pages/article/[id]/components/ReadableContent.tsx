import { memo, RefObject } from 'react';
import styles from './ReadableContent.module.css';

// Progress/status renders must preserve the DOM nodes wrapped by HighlightManager.
export default memo(function ReadableContent({ html, contentRef }: {html:string;contentRef:RefObject<HTMLDivElement | null>}) {
  return <div ref={contentRef} className={`${styles.content} readable-content`} dangerouslySetInnerHTML={{__html:html}} />;
});
