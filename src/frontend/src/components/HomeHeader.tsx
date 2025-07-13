"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import "./HomeHeader.scss";

import {
  useFloating,
  autoUpdate,
  offset,
  flip,
  shift,
  useInteractions,
  useHover,
  FloatingPortal,
  FloatingOverlay,
  FloatingFocusManager,
  useClick,
  useDismiss,
} from "@floating-ui/react";
import AddArticleForm from "./AddArticleForm";
import {  LayoutGrid, List, Plus, RotateCw, Search, X } from "lucide-react";

const Modal = ({ isOpen, onClose, children }: any) => {
  const { refs, context } = useFloating({
    open: isOpen,
    onOpenChange: onClose,
  });

  const click = useClick(context);
  const dismiss = useDismiss(context, { outsidePressEvent: "mousedown" });

  const { getReferenceProps, getFloatingProps } = useInteractions([
    click,
    dismiss,
  ]);

  return (
    <FloatingPortal>
      {isOpen && (
        <FloatingOverlay
          className="bg-gray-100 flex justify-center items-start pt-25"
          style={{ zIndex: 2000 }}
          lockScroll
        >
          <FloatingFocusManager context={context}>
            <div
              ref={refs.setFloating}
              className="bg-white rounded-lg shadow-lg p-4 max-w-md w-full"
              {...getFloatingProps()}
            >
              <section className="mb-3 flex items-center font-bold">
                <div className="flex flex-grow-1 text-xl">Add Article</div>
                <button
                  onClick={onClose}
                  className="p-3 text-gray-500 hover:text-black"
                >
                  <X size={20} />
                </button>
              </section>
              <section className="mb-3">{children}</section>
            </div>
          </FloatingFocusManager>
        </FloatingOverlay>
      )}
    </FloatingPortal>
  );
};


type ArticleStyle = "grid" | "list";

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onAdd: (url: string) => Promise<void>;
  onStyleChange: (value: ArticleStyle) => void;
  placeholder?: string;
}

export default function SearchBar({
  value,
  onChange,
  onAdd,
  onStyleChange,
  placeholder = "Search...",
}: SearchBarProps) {
    const [isGrid, setGrid] = useState(true);

  const [localValue, setLocalValue] = useState(value);

  const onSearch = useCallback(() => {
    onChange(localValue);
  }, [localValue]);

  // useEffect(() => {
  //   const timer = setTimeout(() => {
  //     onChange(localValue);
  //   }, 300);

  //   return () => clearTimeout(timer);
  // }, [localValue, onChange]);

  useEffect(() => {
    setLocalValue(value);
  }, [value]);
  
  useEffect(() => {
    onStyleChange(isGrid ? "grid" : "list");
  }, [isGrid]);

  const handleClear = () => {
    setLocalValue("");
    onChange("");
  };
  // modal
  const [open, setOpen] = useState(false);

  const buttonRef = useRef<HTMLButtonElement>(null);
  const handleKeyDown = (e: any) => {
    if (e.key === "Enter") {
      buttonRef.current?.click();
    }
  };

  return (
    <div className="container rounded-lg space-x-4">
      <div className="flex items-center flex-1 bg-white shadow-sm rounded-lg">
        <div className="relative flex-1">
          <input
            onKeyDown={handleKeyDown}
            id="search-input"
            type="text"
            className="text-sm w-full rounded-lg rounded-e-none p-2 pr-5 focus:outline-none focus:ring-1 focus:ring-blue-300"
            style={{
              height: "36px",
            }}
            value={localValue}
            onChange={(e) => setLocalValue(e.target.value)}
            placeholder={placeholder}
          />
          {localValue && (
            <button
              id="clear-btn"
              onClick={handleClear}
              className="text-sm absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
            >
              &times;
            </button>
          )}
        </div>
        <button
          ref={buttonRef}
          onClick={onSearch}
          // className="px-4 py-2 text-white bg-blue-600 hover:bg-blue-700 rounded-lg rounded-s-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
          className="px-4 py-2 text-sm font-medium text-gray-900 border-s border-gray-300 hover:bg-gray-100 hover:text-blue-700 focus:z-10 focus:ring-0 focus:ring-blue-500"
        >
          <Search size={18} />
        </button>
      </div>

      {/* <div className="border border-gray-300 border-y-0 border-x-1 mx-3 h-6"></div> */}

      <div
        className="inline-flex items-center rounded-md shadow-sm btnGroup btn-group"
        role="group"
      >
        <button
          onClick={() => setOpen(true)}
          type="button"
          // className="rounded-s-lg px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:z-10 focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
          className="rounded-s-lg btn btn-primary"
        >
          <Plus size={18} />
        </button>
        <button
          type="button"
          // className="rounded-e-lg px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:z-10 focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
          onClick={() => setGrid(!isGrid)}
          className="btn btn-secondary"
        >
          {isGrid ? <LayoutGrid size={18} /> : <List size={18} />}
        </button>
      </div>
      <Modal isOpen={open} onClose={() => setOpen(false)}>
        <AddArticleForm
          onAdd={async (url: string) => {
            await onAdd(url);
            setOpen(false);
          }}
        />
      </Modal>
    </div>
  );
}
