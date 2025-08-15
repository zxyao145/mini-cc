import type { Metadata } from "next";
import "./globals.scss";
import 'simpledotcss/simple.min.css';

import ClientLayout from "./layout/ClientLayout";

export const metadata: Metadata = {
  title: "MiniCc - Read Later App",
  description: "A read-later application similar to Omnivore",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <ClientLayout>{children}</ClientLayout>
      </body>
    </html>
  );
}