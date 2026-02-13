namespace Runtime.Core.Entity
{
    public interface IEcsSystem
    {
        void Update(EcsWorld world, float dt);
    }
}
