import type { NextConfig } from 'next';
const exporting = process.env.STATIC_EXPORT === '1';
const config: NextConfig = {
  ...(exporting ? { output: 'export' } : { async rewrites() { return [{ source:'/api/:path*', destination:'http://127.0.0.1:5080/api/:path*' }]; } }),
  trailingSlash: true,
  images: { unoptimized: true },
};
export default config;
