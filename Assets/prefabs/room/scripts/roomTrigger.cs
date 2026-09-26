using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoomTrigger : MonoBehaviour
{
    [HideInInspector] public int roomIndex;
    
    public event Action<int> OnPlayerEnteredRoom;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerEnteredRoom?.Invoke(roomIndex);
        }
    }
}