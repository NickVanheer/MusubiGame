using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LocalizedText : MonoBehaviour
{

    public string key;

    // Use this for initialization
    void Start()
    {
        if (LocalizationManager.Instance.GetIsReady() == false)
            return;
        Debug.Log("Trying to get key: " + key);

        Text text = GetComponent<Text>();

        if(text == null)
        {
            Debug.LogWarning("Can't find a text property when assigning localization key ' " + key + "' to GameObject " + this.name);
            return;
        }

        text.text = LocalizationManager.Instance.GetLocalizedValue(key);
    }

}