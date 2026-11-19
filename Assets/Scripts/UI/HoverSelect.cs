using UnityEngine;
using UnityEngine.EventSystems;

namespace LLTacticalWar
{
    // Mouse hover moves a KeyMenu's selection, so mouse and keyboard agree on what is selected.
    public class HoverSelect : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public MenuView view;
        public int index;

        public void OnPointerEnter(PointerEventData e)
        {
            if (view == null || view.menu == null || !view.IsTop) return;
            if (view.menu.IsEnabled(index) && view.menu.selected != index)
            {
                view.menu.selected = index;
                AudioBank.I?.Play(Sfx.UiMove, 0.3f);
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (view == null || view.menu == null || !view.IsTop) return;
            view.menu.Activate(index);
        }
    }
}
