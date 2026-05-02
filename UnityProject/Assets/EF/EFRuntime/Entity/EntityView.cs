using UnityEngine;

namespace EF.Entity
{
    [DisallowMultipleComponent]
    public class EntityView : MonoBehaviour
    {
        public EntityBase Entity { get; private set; }

        public void SetEntity(EntityBase entity)
        {
            Entity = entity;
        }

        public void ClearEntity()
        {
            Entity = null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Entity is ICollisionHandler handler)
            {
                handler.HandleTriggerEnter2D(other);
            }
        }
    }
}
