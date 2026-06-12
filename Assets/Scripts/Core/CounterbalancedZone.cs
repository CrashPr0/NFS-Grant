using UnityEngine;

namespace NSFGrant.Core
{
    /// <summary>
    /// Marks a format zone as participating in within-station
    /// counterbalancing (see CounterbalanceManager). SlotIndex orders the
    /// zones deterministically; the zone's authored localPosition defines
    /// the slot the manager permutes over.
    /// </summary>
    public class CounterbalancedZone : MonoBehaviour
    {
        [SerializeField] private int slotIndex;

        public int SlotIndex
        {
            get => slotIndex;
            set => slotIndex = value;
        }
    }
}
