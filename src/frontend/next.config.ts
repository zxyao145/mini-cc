import path from 'path';
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  webpack(config:any, options:any) {
    config.resolve.alias = {
      ...(config.resolve.alias || {}),
      '@': path.resolve(__dirname, 'src'),
    };
    return config;
  },
  turbopack: {
    resolveAlias: {
       '@': path.resolve(__dirname, 'src'),
    },
  },
  // Also enable for webpack fallback
  // productionBrowserSourceMaps: true,
};

export default nextConfig;
