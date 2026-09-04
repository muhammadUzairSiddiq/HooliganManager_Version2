using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pastoral{
    public class UIConsole : MonoBehaviour
    {
        public static UIConsole Instance;
        private void Awake()
        {
            textDisplay = transform.GetChild(0).GetComponent<Text>();
            if (Instance != null)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }
        public static Text textDisplay;
        public static void LogData(string msg){
            if(textDisplay != null)
                textDisplay.text += "\n"+msg;
            Debug.Log(msg);
        }
    }
}
