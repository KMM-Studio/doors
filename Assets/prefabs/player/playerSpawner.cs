using UnityEngine;

namespace prefabs.player
{
    public class PlayerSpawner : MonoBehaviour
    {
        public GameObject playerPrefab;

        private void Start()
        {
            if (playerPrefab != null)
            {
                var player = Instantiate(playerPrefab, transform.position, transform.rotation);
                player.transform.SetParent(null);
            }
        }
    }
}
