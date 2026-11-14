import { memo, RefObject } from 'react';

// Progress/status renders must preserve the DOM nodes wrapped by HighlightManager.
export default memo(function ReadableContent({ html, contentRef }: {html:string;contentRef:RefObject<HTMLDivElement | null>}) {
  return <div ref={contentRef} className="readable-content" dangerouslySetInnerHTML={{__html:html}} />;
});
