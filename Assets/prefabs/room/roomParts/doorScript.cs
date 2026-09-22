using System;
using UnityEngine;
using UnityEngine.Serialization;

public class doorScript : MonoBehaviour
{
    [SerializeField] private new Animation animation;
    private bool _isOpen = false;
    
    private void Open()
    {
        animation.Play("doorOpen");
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
