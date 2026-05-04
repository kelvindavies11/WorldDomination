export function emptyLobbyMarkup(createRoute) {
  return `
    <section class="card empty-lobby">
      <h2>No games available</h2>
      <p class="muted">Create a game to open the lobby.</p>
      <a class="button" href="${createRoute}" data-link>Create Game</a>
    </section>
  `;
}
