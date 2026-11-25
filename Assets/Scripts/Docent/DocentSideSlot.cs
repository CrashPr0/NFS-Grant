using UnityEngine;

namespace NSFGrant.Docent
{
    /// <summary>
    /// Lets the docent stand on either side of its room, in the front corner,
    /// with its back to that side wall and its face toward the room's centre.
    /// CounterbalanceManager picks the side per room and participant (derived
    /// from the participant ID, so it is reproducible, and written to the
    /// event log) and calls Apply. The speech card and its pointer stay
    /// room-aligned (facing the door) and sit on the room-centre side of the
    /// figure. "Left" and "right" are the visitor's view when entering the room.
    /// </summary>
    public class DocentSideSlot : MonoBehaviour
    {
        [SerializeField] private Transform speechCard;
        [SerializeField] private Transform cardPointer;
        [SerializeField] private float sideOffset = 5.5f;
        [SerializeField] private float cardOffset = 1.65f;
        [SerializeField] private float pointerOffset = 0.99f;
        [SerializeField] private float depth = 4.9f;

        public bool VisitorLeft { get; private set; } = true;

        public void Apply(bool visitorLeft)
        {
            VisitorLeft = visitorLeft;
            // Rooms face the hub along local +Z, so the visitor's left is local +X.
            float sign = visitorLeft ? 1f : -1f;
            transform.localPosition = new Vector3(sign * sideOffset, transform.localPosition.y, depth);
            // Back to the wall it stands against: face -X from the +X side, +X from the -X side.
            transform.localRotation = Quaternion.Euler(0f, -sign * 90f, 0f);

            // The card is a child of the docent, so undo the turn to keep it
            // facing the door, and place it toward the room centre in room space.
            Quaternion undo = Quaternion.Inverse(transform.localRotation);
            if (speechCard != null)
            {
                speechCard.localRotation = undo;
                speechCard.localPosition = undo * new Vector3(-sign * cardOffset, speechCard.localPosition.y, 0f);
            }
            if (cardPointer != null)
            {
                var p = cardPointer.localPosition;
                cardPointer.localPosition = new Vector3(sign * pointerOffset, p.y, p.z);
            }
        }
    }
}
