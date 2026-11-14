# MiniCc Web UI Refresh

Goal: modernize the existing reading library and shared application shell without changing backend contracts or adding product features.

Design: neutral off-white surfaces, graphite text, restrained teal accents, consistent Lucide outline icons, 4/8px spacing, subtle borders, and visible keyboard focus. Keep existing English interface labels and user content. Use the installed system font stack and existing dependencies. The home page, shared navigation, search, cards, and tags are the main deliverables; keep reading and account pages working.

- [x] Move Simple.css defaults into a lower cascade layer and define shared surface, text, border, accent, and interaction tokens. Verify form and reading styles still render.
- [x] Restyle the sidebar and responsive shell. Keep navigation available on phones and in the collapsed desktop state. Verify keyboard names and active routes.
- [x] Restyle the home heading, search, explicit grid/list controls, article cards, and tag controls. Replace emoji icons with Lucide SVGs. Preserve search submission and API mutations; show pending and error feedback.
- [x] Verify with TypeScript, ESLint on changed files, production build, and browser checks at desktop, phone, and landscape widths. Use isolated fixture data for browser mutations so existing user content is untouched.

No commits or package changes are authorized or needed. A new test framework is unnecessary for these presentation changes; browser checks cover the existing interactions and responsive layout.

Validation:
- TypeScript: `node node_modules/typescript/bin/tsc --noEmit` passed.
- Changed TypeScript files: ESLint passed with two existing `no-img-element` warnings. The installed pnpm layout needs `NODE_PATH=node_modules/.pnpm/node_modules` when invoking ESLint directly.
- Full `next build` is blocked by pre-existing unused variables and explicit `any` errors in unchanged `pages/account/page.tsx`. An isolated `next build --no-lint` completed and generated all nine pages.
- Browser fixture checks: search by Enter, clear search, grid/list controls, favorite/archive, tag add/remove, deletion cancel/confirm, empty results, failed mutation feedback, keyboard focus, and sidebar collapse passed.
- Concurrency: delayed favorite and tag responses were verified in both completion orders; each mutation updates only its own fields.
- Responsive: 320px and 375px portrait, and 812px landscape passed without horizontal scrolling; phone tag list, tag detail, and reader also passed. Main action targets are 44px; reduced-motion disables transitions.
- Reader keeps its own gutters and prevents legacy list styling from adding bullets to navigation.

Compact library follow-up:
- Removed the large library title and subtitle; the search toolbar is now the first content row. Kept All articles/Search results as the accessible page heading.
- Referenced Omnivore library toolbar/card sizing: 40px desktop toolbar, 16px card title, denser metadata and smaller gutters. No new controls or data behavior.
- At 1512px, the first card moved from y=291px to y=125px; the grid now fits four columns. The existing card shrank from 269px to 215px.
- TypeScript and targeted ESLint passed (one existing img-element warning). Search submit/clear and grid/list switching passed browser checks.
- Responsive follow-up passed at 320px, 375px and 812px without horizontal overflow; touch article actions remain 44px. Sidebar collapse/expand passed.

Highlights and Tags follow-up:
- Replaced the Highlights hero heading/subtitle with a compact count row and removed the Tags hero heading/subtitle. Loading/error states retain screen-reader headings without large title blocks.
- Matched library gutters, compact card spacing, theme colors and input sizing. Removed nested panel padding around both lists. Tag detail and article summaries use the same density.
- TypeScript and targeted ESLint passed. Browser checks covered tag search/clear, tag detail navigation, and all three pages at 320px, 375px and 812px without horizontal overflow.
- Highlight note editor expansion/cancel passed at 320px with no overflow and 44px touch buttons; no user data was changed.
