namespace Game.Application;

public interface INpcTickRepository
{
    int GetTickCount(string gameId, string factionId);

    void SetTickCount(string gameId, string factionId, int count);
}
