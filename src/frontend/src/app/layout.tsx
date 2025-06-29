import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "OmeReader - Read Later App",
  description: "A read-later application similar to Omnivore",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}