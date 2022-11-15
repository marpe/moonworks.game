namespace MyGame.Gameplay;

public class SlugBehaviour : EnemyBehaviour
{
    private Enemy? _parent;
    private ICoroutine? _turnRputine;
    public Enemy Parent => _parent ?? throw new InvalidOperationException();

    public override void Initialize(Enemy parent)
    {
        _parent = parent;
        _turnRputine = Parent.CoroutineManager.StartCoroutine(SlugTurn(), 1.0f / 120f, "SlugTurn");
    }

    public override void Update(float deltaSeconds)
    {
    }

    private IEnumerator SlugTurn()
    {
        while (true)
        {
            // update grid cell
            Entity.GetGridCoords(Parent);

            var isGrounded = Parent.World.IsGrounded(Parent, Parent.Velocity);

            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            var turnDistanceFromEdge = 0.1f;


            var rightCollision = Parent.Velocity.X > 0 && !Parent.Collider.HasCollision(Parent.Cell.X + 1, Parent.Cell.Y + 1) &&
                                 Parent.CellPos.X > (1.0f - turnDistanceFromEdge);
            var leftCollision = Parent.Velocity.X < 0 && !Parent.Collider.HasCollision(Parent.Cell.X - 1, Parent.Cell.Y + 1) &&
                                Parent.CellPos.X < turnDistanceFromEdge;

            if (rightCollision || leftCollision)
            {
                Parent.Velocity.X *= -1;
                Parent.FreezeMovement(1.0f);
                while (!Parent.CanMove)
                    yield return null;
            }

            yield return null;
        }
    }
}
