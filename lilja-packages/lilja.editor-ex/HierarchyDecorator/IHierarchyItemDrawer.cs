namespace Lilja.HierarchyDecorator
{
    using UnityEngine;

    public interface IHierarchyItemDrawer
    {
        /// <summary>
        /// Gets whether this drawer is currently enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets the required drawing width for this item.
        /// </summary>
        float GetWidth(GameObject gameObject);

        /// <summary>
        /// Draws the item at the specified rect.
        /// </summary>
        void Draw(GameObject gameObject, Rect rect);
    }
}
