# Website

The SGL SyntheticAI website is a React + Vite + TypeScript + Tailwind CSS single-page application served by the SyntheticAI server.

## Live Site

[syntheticgamelabs.com](https://syntheticgamelabs.com)

## Pages

| Route | Description |
|-------|-------------|
| `/` | Landing page with product overview and CTA |
| `/download` | Platform-specific download pages |
| `/features` | Feature showcase |
| `/about` | Company information |

## SEO

- `sitemap.xml` — Search engine sitemap
- `robots.txt` — Crawler directives with sitemap reference

## Technology

- React 18 + TypeScript
- Vite build tool
- Tailwind CSS
- React Router (client-side routing)
- SPA fallback routing via ASP.NET Core server

## Hosting

The website is served from the `data/website/` directory by the SyntheticAI server with SPA fallback routing. No separate web server is needed.
