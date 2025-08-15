import path from 'path';
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  webpack(config:any, options:any) {
    config.resolve.alias = {
      ...(config.resolve.alias || {}),
      '@': path.resolve(__dirname, 'src'),
    };
    
    // Optimize Prism.js imports for tree shaking
    if (config.optimization) {
      config.optimization.splitChunks = {
        ...config.optimization.splitChunks,
        cacheGroups: {
          ...config.optimization.splitChunks?.cacheGroups,
          prismjs: {
            test: /[\\/]node_modules[\\/]prismjs[\\/]/,
            name: 'prismjs',
            chunks: 'all',
            priority: 20,
          },
        },
      };
    }
    
    return config;
  },
  turbopack: {
    resolveAlias: {
       '@': path.resolve(__dirname, 'src'),
    },
  },
  experimental: {
    optimizePackageImports: ['prismjs'],
  },
  // Also enable for webpack fallback
  // productionBrowserSourceMaps: true,
};

export default nextConfig;
