### Task 1: Scaffold `Journeys.UX`

**Files:**
- Create: `Journeys.UX/package.json`
- Create: `Journeys.UX/tsconfig.json`
- Create: `Journeys.UX/next.config.ts`
- Create: `Journeys.UX/vitest.config.ts`
- Create: `Journeys.UX/.env.example`
- Create: `Journeys.UX/src/app/layout.tsx`
- Create: `Journeys.UX/src/app/page.tsx`
- Create: `Journeys.UX/src/app/globals.css`
- Modify: `.gitignore` (append `Journeys.UX/.env.local` if missing)

**Interfaces:**
- Consumes: spec §3 placement
- Produces: `npm run dev` and `npm test` scripts later tasks use from `Journeys.UX/`

- [ ] **Step 1: Create `Journeys.UX/package.json`**

```json
{
  "name": "journeys-ux",
  "version": "0.0.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "next dev --port 3000",
    "build": "next build",
    "start": "next start",
    "test": "vitest run"
  },
  "dependencies": {
    "next": "^16.0.7",
    "next-auth": "5.0.0-beta.30",
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "zod": "^4.0.0"
  },
  "devDependencies": {
    "@types/node": "^22.0.0",
    "@types/react": "^19.0.0",
    "@types/react-dom": "^19.0.0",
    "typescript": "^5.6.0",
    "vitest": "^3.0.0"
  }
}
```

- [ ] **Step 2: Create `Journeys.UX/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["dom", "dom.iterable", "es2022"],
    "allowJs": false,
    "skipLibCheck": true,
    "strict": true,
    "noEmit": true,
    "esModuleInterop": true,
    "module": "esnext",
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "jsx": "preserve",
    "incremental": true,
    "plugins": [{ "name": "next" }],
    "paths": { "@/*": ["./src/*"] }
  },
  "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
  "exclude": ["node_modules"]
}
```

- [ ] **Step 3: Create `Journeys.UX/next.config.ts`**

```ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true
};

export default nextConfig;
```

- [ ] **Step 4: Create `Journeys.UX/vitest.config.ts`**

```ts
import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  test: { environment: "node" },
  resolve: { alias: { "@": resolve(root, "src") } }
});
```

- [ ] **Step 5: Create `Journeys.UX/.env.example`** (empty values only)

```
AUTH_SECRET=
AUTH_TRUST_HOST=true
AUTH0_CLIENT_ID=
AUTH0_CLIENT_SECRET=
AUTH0_ISSUER=
JOURNEYS_API_BASE_URL=https://localhost:7001
JOURNEYS_TENANT_ID=
JOURNEYS_UX_ALLOW_API_KEY_LOGIN=false
JOURNEYS_UX_TLS_REJECT_UNAUTHORIZED=true
```

- [ ] **Step 6: Create `Journeys.UX/src/app/globals.css`**

```css
:root { font-family: system-ui, sans-serif; color: #111; }
body { margin: 0; }
a { color: inherit; }
button { cursor: pointer; }
.layout { display: flex; min-height: 100vh; }
nav.loyalty-nav { width: 240px; padding: 1rem; background: #f4f4f5; }
nav.loyalty-nav h1 { font-size: 1rem; margin: 0 0 1rem; }
nav.loyalty-nav a, nav.loyalty-nav span { display: block; padding: 0.35rem 0; }
nav.loyalty-nav .disabled { color: #888; }
main { padding: 1.5rem; flex: 1; }
.card-row { display: flex; gap: 1rem; }
.card { border: 1px solid #ddd; padding: 1rem; min-width: 12rem; }
.error { color: #b91c1c; }
.empty { color: #555; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #ddd; padding: 0.4rem 0.6rem; text-align: left; }
```

- [ ] **Step 7: Create `Journeys.UX/src/app/layout.tsx` and `src/app/page.tsx`**

```tsx
import type { ReactNode } from "react";
import "./globals.css";

export const metadata = { title: "Journeys" };

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
```

```tsx
import { redirect } from "next/navigation";

export default function Home() {
  redirect("/loyalty");
}
```

- [ ] **Step 8: Append to `.gitignore` if not already present**

```
Journeys.UX/.env.local
Journeys.UX/.next
```

- [ ] **Step 9: Install and typecheck**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm install
npx tsc --noEmit
```

Expected: exit 0 (or only missing `next-env.d.ts` until first `next dev`; if so run `npx next build` is not required yet — `npx next dev` for one second is enough to emit it, or add an empty `Journeys.UX/next-env.d.ts` with the Next reference comment).

- [ ] **Step 10: Do not commit** unless the user asks in that message.

---

