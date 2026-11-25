using UnityEngine;

namespace NSFGrant.Docent
{
    /// <summary>
    /// Lets the docent stand on either side of its room. CounterbalanceManager
    /// picks the side per room and participant (derived from the participant
    /// ID, so it is reproducible, and written to the event log) and calls
    /// Apply; the speech card and its pointer swap so the card always sits on
    /// the room-centre side of the figure. "Left" and "right" are the visitor's
    /// view when entering the room.
    /// </summary>
    public class DocentSideSlot : MonoBehaviour
    {
        [SerializeField] private Transform speechCard;
        [SerializeField] private Transform cardPointer;
        [SerializeField] private float sideOffset = 5.2f;
        [SerializeField] private float cardOffset = 1.35f;
        [SerializeField] private float pointerOffset = 0.99f;
        [SerializeField] private float depth = 4.9f;

        public bool VisitorLeft { get; private set; } = true;

        public void Apply(bool visitorLeft)
        {
            VisitorLeft = visitorLeft;
            // Rooms face the hub along local +Z, so the visitor's left is local +X.
            float sign = visitorLeft ? 1f : -1f;
            transform.localPosition = new Vector3(sign * sideOffset, transform.localPosition.y, depth);
            if (speechCard != null)
            {
                var p = speechCard.localPosition;
                speechCard.localPosition = new Vector3(-sign * cardOffset, p.y, p.z);
            }
            if (cardPointer != null)
            {
                var p = cardPointer.localPosition;
                cardPointer.localPosition = new Vector3(sign * pointerOffset, p.y, p.z);
            }
        }
    }
}
