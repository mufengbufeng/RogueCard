using UnityEngine;

namespace EF.Entity
{
    public interface ICollisionHandler
    {
        void HandleTriggerEnter2D(Collider2D other);
    }
}
