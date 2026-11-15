"use client";

import * as DialogPrimitive from "@radix-ui/react-dialog";
import { useRef } from "react";
import type { ComponentPropsWithoutRef, ReactNode } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { X } from "lucide-react";
import { Tooltip } from "../tooltip/tooltip";
import { motionTokens } from "../motion-tokens";
import styles from "./dialog.module.css";

export const Dialog = DialogPrimitive.Root;

export interface DialogContentProps extends ComponentPropsWithoutRef<typeof DialogPrimitive.Content> {
  title: string;
  description?: string;
  children?: ReactNode;
  footer?: ReactNode;
  closeDisabled?: boolean;
  closeLabel?: string;
}

/** When the title or description changes while open, the new copy rises in and the old copy leaves upward. */
function SwapText({ text }: { text: string }) {
  const reduced = useReducedMotion();
  return <AnimatePresence mode="popLayout" initial={false}>
    <motion.span key={text} className={styles.swap} initial={reduced ? false : { opacity: 0, y: "0.3em", filter: `blur(${motionTokens.blur.soft}px)` }} animate={{ opacity: 1, y: 0, filter: "blur(0px)" }} exit={reduced ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: "-0.3em", filter: `blur(${motionTokens.blur.subtle}px)`, transition: { duration: motionTokens.duration.fast, ease: [...motionTokens.ease.standard] } }} transition={{ duration: motionTokens.duration.standard, ease: [...motionTokens.ease.enter] }}>{text}</motion.span>
  </AnimatePresence>;
}

export function DialogContent({
  title, description, children, footer, closeDisabled, closeLabel = "关闭", className,
  onOpenAutoFocus, onCloseAutoFocus, ...props
}: DialogContentProps) {
  const opener = useRef<HTMLElement>(null);
  const classes = [styles.content, className].filter(Boolean).join(" ");
  const descriptionProps = description ? {} : { "aria-describedby": undefined };
  const inner = <>
    <header className={styles.header}>
      <div>
        <DialogPrimitive.Title className={styles.title} aria-label={title}><SwapText text={title}/></DialogPrimitive.Title>
        {description && <DialogPrimitive.Description className={styles.description}>{description}</DialogPrimitive.Description>}
      </div>
      <Tooltip content={closeLabel} side="bottom">
        <DialogPrimitive.Close className={styles.close} aria-label={closeLabel} disabled={closeDisabled}>
          <X size={16} strokeWidth={1.75} aria-hidden="true"/>
        </DialogPrimitive.Close>
      </Tooltip>
    </header>
    {children != null && <div className={styles.body}>{children}</div>}
    {footer && <footer className={styles.footer}>{footer}</footer>}
  </>;
  return <DialogPrimitive.Portal>
    <DialogPrimitive.Overlay className={styles.overlay}/>
    <DialogPrimitive.Content {...descriptionProps} {...props} className={classes}
      onOpenAutoFocus={event => {
        opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        onOpenAutoFocus?.(event);
      }}
      onCloseAutoFocus={event => {
        onCloseAutoFocus?.(event);
        if (!event.defaultPrevented && opener.current?.isConnected) {
          event.preventDefault();
          opener.current.focus({ preventScroll: true });
        }
      }}
    >{inner}</DialogPrimitive.Content>
  </DialogPrimitive.Portal>;
}
