using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Cell : MonoBehaviour {

    public bool HasMerged = false; 
    private Text CellText;
    private Image CellImage;

    public int RowIndex;
    public int ColumnIndex;

    //The text on the cell.
    private SimpleWord word;
    public SimpleWord Word
    {
        get { return word; }
        set
        {
            word = value;

            if (word.Word == "")
                SetHidden();
            else
            {
                CellText.text = this.Word.Word.ToString();
                SetVisible();
            }
        }
    }

    //The style of the cell.
    private int cellStyle;
    public int CellStyle
    {
        get { return cellStyle; }
        set
        {
            cellStyle = value;

            if (cellStyle == 0)
                SetHidden();
            else
            {
                ApplyStyle(cellStyle);
                SetVisible();
            }
        }
    }

    private Animator animator;

	void Awake () {
        CellText = GetComponentInChildren<Text>();
        CellImage = transform.Find("Panel").GetComponent<Image>();
        animator = GetComponent<Animator>();
	}

    public void SetText(SimpleWord word, int style)
    {
        cellStyle = style;
        this.Word = word;

        if (cellStyle == 0)
            SetHidden();
        else
        {
            ApplyStyle(cellStyle);
            SetVisible();
        }
    }

    public void PlayMergeAnimation()
    {
        animator.SetTrigger("Merge");
    }

    public void PlayAppearAnimation()
    {
        animator.SetTrigger("Appear");
    }

    void SetVisible()
    {
        CellImage.enabled = true;
        CellText.enabled = true;
    }
    
    void SetHidden()
    {
        CellImage.enabled = false;
        CellText.enabled = false;
    }

    void ApplyStyleFromHolder(int index)
    {
        CellText.color = CellStyleHolder.Instance.CellStyles[index].TextColor;
        CellImage.color = CellStyleHolder.Instance.CellStyles[index].CellColor;
    }

    void ApplyStyle(int num)
    {
        switch (num)
        {
            case 2:
                ApplyStyleFromHolder(0);
                break;
            case 4:
                ApplyStyleFromHolder(1);
                break;
            case 8:
                ApplyStyleFromHolder(2);
                break;
            case 16:
                ApplyStyleFromHolder(3);
                break;
            case 32:
                ApplyStyleFromHolder(4);
                break;
            case 64:
                ApplyStyleFromHolder(5);
                break;
            case 128:
                ApplyStyleFromHolder(6);
                break;
            default:
                Debug.LogError("Trying to apply style for a cell with an unregistered number of " + num);
                break;
        }
    }
}
