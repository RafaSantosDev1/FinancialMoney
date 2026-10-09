# FinSight — frontend (Angular)

This is the Angular single-page application for FinSight.
See the [root README](../../README.md) for full setup, architecture and API documentation.

## Commands

```bash
npm install        # install dependencies
npm start          # dev server on http://localhost:4200
npm run build      # production build
npm test           # unit tests (needs Chrome/Chromium; set CHROME_BIN for Edge)
```

The app expects the ASP.NET Core API on `http://localhost:5155` by default
(see `src/environments/environment.ts`).
