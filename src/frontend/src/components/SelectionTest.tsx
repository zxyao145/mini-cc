'use client';

import { useState, useEffect } from 'react';

export default function SelectionTest() {
  const [selectionInfo, setSelectionInfo] = useState('');

  useEffect(() => {
    const handleSelectionChange = () => {
      const selection = window.getSelection();
      if (selection) {
        setSelectionInfo(`Selected: "${selection.toString()}" | Range count: ${selection.rangeCount}`);
      }
    };

    document.addEventListener('selectionchange', handleSelectionChange);
    
    return () => {
      document.removeEventListener('selectionchange', handleSelectionChange);
    };
  }, []);

  return (
    <div style={{ 
      position: 'fixed', 
      top: 10, 
      right: 10, 
      background: 'white', 
      border: '1px solid black', 
      padding: '10px',
      maxWidth: '300px',
      fontSize: '12px',
      zIndex: 9999
    }}>
      <div>Selection Debug:</div>
      <div>{selectionInfo}</div>
    </div>
  );
}