# OSM Base Map Layout Design

## Purpose

Create the first active match screen around an OpenStreetMap base map. The screen should feel like the game map is already the main product surface, even before generated territory overlays, army movement, and combat commands are fully implemented.

## Scope

The first slice replaces the `/games/cardiff` placeholder with a command-map layout in the existing dependency-free static frontend served by `Game.Api`.

The screen includes:

- an OSM/MapLibre map centered on Cardiff
- a full-screen map-first active match surface
- collapsible floating selected-territory and leaderboard widgets
- a Cardiff-specific irregular playable-area polygon
- a grey out-of-bounds mask outside the playable polygon
- responsive layout behavior for tablet and mobile widths
- graceful fallback content if the external map script or tiles cannot load

The slice excludes:

- generated territory polygons
- live SignalR updates
- issuing movement orders
- NPC simulation controls
- React/Vite migration

## Architecture

The implementation stays inside the current static UI boundary:

- `src/Game.Api/wwwroot/index.html` loads MapLibre CSS and JavaScript from a CDN.
- `src/Game.Api/wwwroot/app.js` renders the `/games/cardiff` match screen and initializes the map after render.
- `src/Game.Api/wwwroot/styles.css` owns the dark command-map theme, full-screen map sizing, floating widgets, collapsed states, and responsive behavior.

The map is a client-only visual shell. The authoritative backend remains unchanged. Future territory overlays should be added as MapLibre sources and layers inside the map initializer rather than as DOM nodes.

The Cardiff map details are stored in `MAP_DETAILS.cardiff` so the map center, camera bounds, and playable-area polygon remain map-specific data rather than generic rendering logic.

## Data Flow

The match screen fetches `/api/matches/cardiff` after render. Match data populates:

- map area name
- territory count
- sample selected territory details
- leaderboard rows
- faction colors and control percentages

If the API request fails, the screen still renders the base layout with a readable status message.

## Testing

Automated tests should verify that the client fallback route for `/games/cardiff` returns the static frontend shell and that the shell contains the expected map layout hooks. Manual verification should run the API and open `/games/cardiff` to confirm the layout loads.
