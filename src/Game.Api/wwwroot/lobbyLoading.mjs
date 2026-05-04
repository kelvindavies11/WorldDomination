export function shouldLoadGames({ loading, gamesLoaded, gameCount, error }) {
  return !loading && !gamesLoaded && gameCount === 0 && !error;
}
