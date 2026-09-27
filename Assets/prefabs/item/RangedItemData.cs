using UnityEngine;

namespace prefabs.item
{
    [CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "Items/Ranged Data")]
    public class RangedItemData : ItemData
    {
        [Header("Ranged Specific Settings")]
        [Tooltip("Maksymalny zasięg lotu kuli.")]
        public float shootRange = 100f;

        [Tooltip("Warstwy, w które kula może trafić (np. wrogowie, ściany).")]
        public LayerMask hitMask;

        public override void ExecutePrimary(Transform origin, ref int currentMag)
        {
            // 1. Sprawdzenie amunicji
            if (currentMag <= 0)
            {
                Debug.Log($"<color=yellow><b>[Pistolet]</b></color> Pusty magazynek! (Klik-Klik)");
                // Tutaj docelowo można dodać dźwięk pustego magazynka
                return;
            }

            // 2. Pobranie kuli z opakowania w ekwipunku gracza
            currentMag--;
            Debug.Log($"<color=orange><b>[Pistolet]</b></color> Pif-Paf! Zostało kul: {currentMag}");

            // 3. Wystrzelenie wirtualnego promienia z kamery gracza (Raycast)
            if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, shootRange, hitMask))
            {
                Debug.Log($"<color=red><b>[Pistolet]</b></color> Trafiono w: <b>{hit.transform.name}</b> za {power} obrażeń.");

                // Docelowy kod zadający obrażenia wrogom:
                // if (hit.transform.TryGetComponent(out Health targetHealth))
                // {
                //     targetHealth.TakeDamage(power);
                // }
            }
        }
    }
}