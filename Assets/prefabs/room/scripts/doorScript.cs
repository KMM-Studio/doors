using System;
using UnityEngine;

namespace prefabs.room.scripts
{
    public class DoorScript : MonoBehaviour
    {
        [SerializeField] private new Animation animation;
        private bool _isOpen = false;
    
        private void Open()
        {
            animation.Play("doorOpen");
        }

        public void Reset()
        {
            _isOpen = false;
            animation.Play("doorClosed");
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.CompareTag("Player") && !_isOpen)
            {
                _isOpen = true;
                Open();
            }
        }
    }
}
