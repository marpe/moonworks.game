using MyGame.Entities;

namespace MyGame;

public class EntityList
{
    private List<Entity> _entities = new();
    private List<Entity> _entitiesToRemove = new();
    private List<Entity> _tempList = new();
    private List<Entity> _entitiesToAdd = new();

    public void Add(Entity entity)
    {
        _entitiesToAdd.Add(entity);
    }

    public void AddRange(IEnumerable<Entity> entities)
    {
        _entitiesToAdd.AddRange(entities);
    }

    public void Remove(Entity entity)
    {
        _entitiesToRemove.Add(entity);
    }

    public T First<T>() where T : Entity
    {
        return FirstOrDefault<T>() ?? throw new Exception($"No entity of type \"{typeof(T).Name}\" found");
    }

    public T? FirstOrDefault<T>() where T : Entity
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            if (_entities[i] is T entity)
                return entity;
        }

        return null;
    }

    public void Update(World world, float deltaSeconds)
    {
        UpdateLists(world);

        for (var i = 0; i < _entities.Count; i++)
        {
            _entities[i].Update(deltaSeconds);
        }
    }

    public void Clear()
    {
        _entities.Clear();
        _entitiesToAdd.Clear();
        _entitiesToRemove.Clear();
    }

    private void UpdateLists(World world)
    {
        for (var i = 0; i < _entitiesToRemove.Count; i++)
        {
            var entity = _entitiesToRemove[i];
            entity.OnEntityRemoved();
            _entities.Remove(entity);
        }

        _entitiesToRemove.Clear();

        _tempList.Clear();
        for (var i = 0; i < _entitiesToAdd.Count; i++)
        {
            var entity = _entitiesToAdd[i];
            _entities.Add(entity);
            _tempList.Add(entity);
        }

        _entitiesToAdd.Clear();

        for (var i = 0; i < _tempList.Count; i++)
        {
            _tempList[i].OnEntityAdded(world);
        }

        _tempList.Clear();
    }

    public void Draw(Renderer renderer, double alpha, bool usePointFiltering)
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            entity.Draw.Draw(renderer, alpha, usePointFiltering);
        }
    }

    public void ForEach(Action<Entity> callback)
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            callback(_entities[i]);
        }
    }

    public Entity? FirstOrDefault(Predicate<Entity> func)
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            if (func(_entities[i]))
                return _entities[i];
        }

        return null;
    }
}
