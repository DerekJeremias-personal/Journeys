### Task 5: Loyalty nav + Overview

**Files:**
- Create: `Journeys.UX/src/app/loyalty/layout.tsx`
- Create: `Journeys.UX/src/app/loyalty/page.tsx`
- Create: `Journeys.UX/src/components/loyalty-nav.tsx`

**Interfaces:**
- Consumes: session (layout may call `auth()` and redirect to `/signin` if missing)
- Produces: nav items listed below

Nav items (exact labels/hrefs). Live: Overview `/loyalty`, Accounts `/loyalty/accounts`, Campaigns `/loyalty/campaigns`. Disabled (`<span className="disabled">`, not a link): Promotions, Analytics, Action Log, Notifications, File Ingestion, Settings, Data Explorer, Model Builder.

- [ ] **Step 1: Implement `loyalty-nav.tsx` as a server component with those items.** First heading text: `Loyalty`.

- [ ] **Step 2: `loyalty/layout.tsx` wraps children with `.layout` > nav + `<main>`.**

- [ ] **Step 3: `loyalty/page.tsx` title `Loyalty`. Two `.card` links only: Accounts and Campaigns.**

- [ ] **Step 4: Do not add routes for disabled items.** Visiting `/loyalty/promotions` should 404.

- [ ] **Step 5: Do not commit** unless the user asks in that message.
