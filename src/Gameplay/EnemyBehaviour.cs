using MyGame.Entities;

namespace MyGame;

public abstract class EnemyBehaviour
{
    public abstract void Initialize(Enemy parent);
    public abstract void Update(float deltaSeconds);
}
