using UnityEngine;
using System.Collections;

public class SimpleWord
{
    public string Word;
    public int ID;

    public SimpleWord(string word, int id)
    {
        this.Word = word;
        this.ID = id;

    }
}

public class Word : MonoBehaviour {

    public string Hiragana;
    public string Kanji;
    public string Meaning;

    public Word(string kanji, string hiragana, string meaning)
    {
        this.Kanji = kanji;
        this.Hiragana = hiragana;
        this.Meaning = meaning;
    }
}
