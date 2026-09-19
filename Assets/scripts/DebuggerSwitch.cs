using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DefaultNamespace
{
    public class DebuggerSwitch : MonoBehaviour
    {   
        public bool shouldLogIntoConsole= false;

        private void Awake()
        {
            Debug.unityLogger.logEnabled =  shouldLogIntoConsole;
        }
    }
}